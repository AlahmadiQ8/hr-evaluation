namespace Taqyeem.Application.Abstractions;

/// <summary>The employee acting in the current request (resolved from auth claims).</summary>
public interface ICurrentUser
{
    Guid? EmployeeId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
