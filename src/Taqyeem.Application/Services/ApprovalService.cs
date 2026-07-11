using Microsoft.EntityFrameworkCore;
using Taqyeem.Application.Abstractions;
using Taqyeem.Application.Contracts;
using Taqyeem.Application.Routing;
using Taqyeem.Application.Scoring;
using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Services;

/// <summary>Commands that move an evaluation through its approval chain.</summary>
public sealed class ApprovalService(ITaqyeemDbContext db, IScoringEngine scoring, IRoutingEngine routing)
{
    /// <summary>The manager records item ratings, the evaluation is scored, and it advances past the manager stage.</summary>
    public async Task ManagerSubmitAsync(Guid evaluationId, Guid userId, ManagerSubmitRequest request, CancellationToken cancellationToken = default)
    {
        Evaluation evaluation = await LoadAsync(evaluationId, cancellationToken);

        if (evaluation.Stage != EvaluationStage.ManagerEvaluation)
        {
            throw new InvalidOperationException("This evaluation is not awaiting a manager evaluation.");
        }

        bool isEvaluator = evaluation.ApprovalSteps
            .Any(s => s.Stage == EvaluationStage.ManagerEvaluation && s.ApproverId == userId && s.Decision is null);
        if (!isEvaluator)
        {
            throw new UnauthorizedAccessException("You are not the evaluating manager for this evaluation.");
        }

        foreach (ItemRatingInput input in request.Items)
        {
            EvaluationItem? item = evaluation.Items.FirstOrDefault(i => i.Id == input.ItemId);
            if (item is null)
            {
                throw new KeyNotFoundException($"Item {input.ItemId} is not part of this evaluation.");
            }

            if (input.Rating is < ScoredItem.MinRating or > ScoredItem.MaxRating)
            {
                throw new InvalidOperationException($"Rating must be between {ScoredItem.MinRating} and {ScoredItem.MaxRating}.");
            }

            item.Rating = input.Rating;
        }

        ScoreResult result = scoring.Score(
            ScoredItemsOf(evaluation, EvaluationItemKind.Competency),
            ScoredItemsOf(evaluation, EvaluationItemKind.Objective));
        evaluation.ScorePercent = result.Percent;
        evaluation.WeightedRating = result.WeightedRating;
        evaluation.Band = result.Band;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (ApprovalStep step in evaluation.ApprovalSteps.Where(s => s.Stage == EvaluationStage.ManagerEvaluation))
        {
            step.Decision = ApprovalDecision.Approve;
            step.Comment ??= request.Comment;
            step.DecidedAt = now;
        }

        evaluation.Stage = NextStage(evaluation, ApprovalDecision.Approve);
        evaluation.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>An approver approves (advance) or returns (send back to the manager) an evaluation.</summary>
    public async Task DecideAsync(Guid evaluationId, Guid userId, DecisionRequest request, CancellationToken cancellationToken = default)
    {
        Evaluation evaluation = await LoadAsync(evaluationId, cancellationToken);

        if (evaluation.Stage is EvaluationStage.ManagerEvaluation or EvaluationStage.Finalized or EvaluationStage.Draft)
        {
            throw new InvalidOperationException("This evaluation is not awaiting an approval decision.");
        }

        ApprovalStep? step = evaluation.ApprovalSteps
            .FirstOrDefault(s => s.Stage == evaluation.Stage && s.ApproverId == userId && s.Decision is null);
        if (step is null)
        {
            throw new UnauthorizedAccessException("You are not the current approver for this evaluation.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        step.Decision = request.Decision;
        step.Comment = request.Comment;
        step.DecidedAt = now;

        if (request.Decision == ApprovalDecision.Return)
        {
            // Reopen the manager stage for rework.
            foreach (ApprovalStep managerStep in evaluation.ApprovalSteps.Where(s => s.Stage == EvaluationStage.ManagerEvaluation))
            {
                managerStep.Decision = null;
                managerStep.DecidedAt = null;
            }
        }

        evaluation.Stage = NextStage(evaluation, request.Decision);
        evaluation.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Evaluation> LoadAsync(Guid evaluationId, CancellationToken cancellationToken)
    {
        return await db.Evaluations
            .Include(e => e.Items)
            .Include(e => e.ApprovalSteps)
            .FirstOrDefaultAsync(e => e.Id == evaluationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Evaluation {evaluationId} was not found.");
    }

    private EvaluationStage NextStage(Evaluation evaluation, ApprovalDecision decision)
    {
        List<EvaluationStage> stages =
        [
            .. evaluation.ApprovalSteps.OrderBy(s => s.Order).Select(s => s.Stage).Distinct(),
            EvaluationStage.Finalized,
        ];

        var chain = stages
            .Select((stage, index) => new ApprovalStepDefinition(index + 1, stage, []))
            .ToList();

        return routing.NextStage(chain, evaluation.Stage, decision);
    }

    private static List<ScoredItem> ScoredItemsOf(Evaluation evaluation, EvaluationItemKind kind) =>
        [.. evaluation.Items
            .Where(i => i.Kind == kind && i.Rating is not null)
            .Select(i => new ScoredItem { Name = i.Name, Weight = i.Weight, Rating = i.Rating!.Value })];
}
