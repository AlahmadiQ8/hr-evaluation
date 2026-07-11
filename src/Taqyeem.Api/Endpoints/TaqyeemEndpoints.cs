using Taqyeem.Application.Abstractions;
using Taqyeem.Application.Contracts;
using Taqyeem.Application.Services;

namespace Taqyeem.Api.Endpoints;

public static class TaqyeemEndpoints
{
    public static void MapTaqyeemEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder api = app.MapGroup("/api").WithTags("Taqyeem");

        // Anonymous — the demo login screen lists the personas to sign in as.
        api.MapGet("/personas", (PersonaService personas, CancellationToken ct) => personas.GetPersonasAsync(ct))
            .AllowAnonymous()
            .WithName("GetPersonas");

        RouteGroupBuilder secured = api.MapGroup(string.Empty).RequireAuthorization();

        secured.MapGet("/org/tree", (OrganizationService org, CancellationToken ct) => org.GetTreeAsync(ct));

        secured.MapGet("/cycles", (OrganizationService org, CancellationToken ct) => org.GetCyclesAsync(ct));

        secured.MapGet("/cycles/{cycleId:guid}/evaluations",
            (Guid cycleId, EvaluationService evaluations, CancellationToken ct) => evaluations.GetByCycleAsync(cycleId, ct));

        secured.MapGet("/evaluations/inbox",
            (EvaluationService evaluations, ICurrentUser user, CancellationToken ct) =>
                evaluations.GetInboxAsync(RequireUser(user), ct));

        secured.MapGet("/evaluations/{id:guid}",
            async (Guid id, EvaluationService evaluations, ICurrentUser user, CancellationToken ct) =>
            {
                EvaluationDetailDto? detail = await evaluations.GetDetailAsync(id, user.EmployeeId, ct);
                return detail is null ? Results.NotFound() : Results.Ok(detail);
            });

        secured.MapPost("/evaluations/{id:guid}/manager-submit",
            async (Guid id, ManagerSubmitRequest body, ApprovalService approvals, ICurrentUser user, CancellationToken ct) =>
            {
                await approvals.ManagerSubmitAsync(id, RequireUser(user), body, ct);
                return Results.NoContent();
            });

        secured.MapPost("/evaluations/{id:guid}/decision",
            async (Guid id, DecisionRequest body, ApprovalService approvals, ICurrentUser user, CancellationToken ct) =>
            {
                await approvals.DecideAsync(id, RequireUser(user), body, ct);
                return Results.NoContent();
            });

        secured.MapGet("/calibration",
            (CalibrationService calibration, CancellationToken ct) => calibration.GetSectorCalibrationsAsync(ct))
            .RequireAuthorization("HrOnly");
    }

    private static Guid RequireUser(ICurrentUser user) =>
        user.EmployeeId ?? throw new UnauthorizedAccessException("No authenticated persona.");
}
