using Codenames.Cli.Models;

namespace Codenames.Cli.Api;

public class LobbyApiClient(ApiClient api)
{
    public Task<LobbyResponse> CreateAsync(string name, CancellationToken cancellationToken = default) =>
        api.PostAsync<LobbyResponse>("/api/lobbies", new { name }, cancellationToken);

    public Task<LobbyResponse> JoinAsync(string code, CancellationToken cancellationToken = default) =>
        api.PostAsync<LobbyResponse>($"/api/lobbies/{code}/join", new { }, cancellationToken);

    public Task SelectTeamAsync(int lobbyId, string team, CancellationToken cancellationToken = default) =>
        api.PostAsync($"/api/lobbies/{lobbyId}/team", new { team }, cancellationToken);

    public Task SelectRoleAsync(int lobbyId, string role, CancellationToken cancellationToken = default) =>
        api.PostAsync($"/api/lobbies/{lobbyId}/role", new { role }, cancellationToken);

    public Task StartAsync(int lobbyId, CancellationToken cancellationToken = default) =>
        api.PostAsync($"/api/lobbies/{lobbyId}/start", new { }, cancellationToken);
}
