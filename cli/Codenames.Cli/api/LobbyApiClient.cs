using Codenames.Cli.Models;
using System.Net;

namespace Codenames.Cli.Api;

public class LobbyApiClient(ApiClient api)
{
    public async Task<LobbyStateResponse?> GetMyLobbyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await api.GetAsync<LobbyStateResponse>("/api/lobbies/me", cancellationToken);
        }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<LobbyStateResponse> CreateAsync(CancellationToken cancellationToken = default) =>
        api.PostAsync<LobbyStateResponse>(
            "/api/lobbies",
            new { },
            cancellationToken);

    public Task<LobbyStateResponse> GetAsync(string code, CancellationToken cancellationToken = default) =>
        api.GetAsync<LobbyStateResponse>($"/api/lobbies/{code}", cancellationToken);

    public Task<LobbyStateResponse> JoinAsync(string code, CancellationToken cancellationToken = default) =>
        api.PostAsync<LobbyStateResponse>($"/api/lobbies/{code}/join", new { }, cancellationToken);

    public Task LeaveAsync(string lobbyId, CancellationToken cancellationToken = default) =>
        api.PostAsync($"/api/lobbies/{lobbyId}/leave", new { }, cancellationToken);

    public Task<StartGameResponse> StartAsync(string lobbyId, CancellationToken cancellationToken = default) =>
        api.PostAsync<StartGameResponse>($"/api/lobbies/{lobbyId}/start", new { }, cancellationToken);
}