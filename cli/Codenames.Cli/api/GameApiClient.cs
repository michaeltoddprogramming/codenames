using Codenames.Cli.Models;
using System.Net;

namespace Codenames.Cli.Api;

public class GameApiClient(ApiClient api)
{
    public Task<List<GamePlayerInfo>> GetPlayersAsync(int gameId, CancellationToken cancellationToken = default) =>
        api.GetAsync<List<GamePlayerInfo>>($"/api/games/{gameId}/players", cancellationToken);

    public Task<GameParticipantIdentityResponse> GetMyIdentityAsync(int gameId, CancellationToken cancellationToken = default) =>
        api.GetAsync<GameParticipantIdentityResponse>($"/api/games/{gameId}/me", cancellationToken);

    public Task<GameStateResponse> GetStateAsync(int gameId, CancellationToken cancellationToken = default) =>
        api.GetAsync<GameStateResponse>($"/api/games/{gameId}", cancellationToken);

    public Task<GameStateDetailResponse> GetDetailedStateAsync(int gameId, CancellationToken cancellationToken = default) =>
        api.GetAsync<GameStateDetailResponse>($"/api/games/{gameId}/state", cancellationToken);

    public Task SubmitClueAsync(int gameId, string clueWord, int clueNumber, CancellationToken cancellationToken = default) =>
        api.PostAsync($"/api/games/{gameId}/clue", new { clueWord, clueNumber }, cancellationToken);

    public Task SubmitVoteAsync(int gameId, string word, CancellationToken cancellationToken = default) =>
        api.PostAsync($"/api/games/{gameId}/vote", new { word }, cancellationToken);

    public async Task<int?> GetActiveGameAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await api.GetAsync<ActiveGameResponse>($"/api/players/me/active-game", cancellationToken);
            return result?.GameId;
        }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound || ex.Status == HttpStatusCode.NoContent)
        {
            return null;
        }
    }

    private record ActiveGameResponse(int GameId);
}
