using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Taqyeem.Web.Api;

/// <summary>
/// Typed client for the Taqyeem API. It attaches the current persona (from the auth cookie) as the
/// <c>X-Demo-Persona</c> header so the API's demo authentication can identify the caller.
/// </summary>
public sealed class TaqyeemApiClient(HttpClient http, AuthenticationStateProvider authState)
{
    public async Task<List<PersonaDto>> GetPersonasAsync(CancellationToken ct = default) =>
        await GetAsync<List<PersonaDto>>("/api/personas", ct) ?? [];

    public async Task<List<OrgSectorDto>> GetOrgTreeAsync(CancellationToken ct = default) =>
        await GetAsync<List<OrgSectorDto>>("/api/org/tree", ct) ?? [];

    public async Task<List<EvaluationSummaryDto>> GetInboxAsync(CancellationToken ct = default) =>
        await GetAsync<List<EvaluationSummaryDto>>("/api/evaluations/inbox", ct) ?? [];

    public Task<EvaluationDetailDto?> GetEvaluationAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<EvaluationDetailDto>($"/api/evaluations/{id}", ct);

    public async Task<List<CalibrationDto>> GetCalibrationAsync(CancellationToken ct = default) =>
        await GetAsync<List<CalibrationDto>>("/api/calibration", ct) ?? [];

    public async Task ManagerSubmitAsync(Guid id, ManagerSubmitRequest body, CancellationToken ct = default)
    {
        using HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Post, $"/api/evaluations/{id}/manager-submit");
        request.Content = JsonContent.Create(body);
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DecideAsync(Guid id, DecisionRequest body, CancellationToken ct = default)
    {
        using HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Post, $"/api/evaluations/{id}/decision");
        request.Content = JsonContent.Create(body);
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Get, url);
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        AuthenticationState state = await authState.GetAuthenticationStateAsync();
        string? personaId = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(personaId))
        {
            request.Headers.Add("X-Demo-Persona", personaId);
        }

        return request;
    }
}
