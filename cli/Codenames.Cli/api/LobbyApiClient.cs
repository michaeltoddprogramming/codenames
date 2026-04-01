using Codenames.Cli.Models;

namespace Codenames.Cli.Api;

public class LobbyApiClient(ApiClient api)
{
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
