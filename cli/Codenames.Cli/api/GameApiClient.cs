using Codenames.Cli.Models;

namespace Codenames.Cli.Api;

public class GameApiClient(ApiClient api)
{
    public Task<GameParticipantIdentityResponse> GetMyIdentityAsync(int gameId, CancellationToken cancellationToken = default) =>
        api.GetAsync<GameParticipantIdentityResponse>($"/api/games/{gameId}/me", cancellationToken);

    public Task<GameStateResponse> GetStateAsync(int gameId, CancellationToken cancellationToken = default) =>
        api.GetAsync<GameStateResponse>($"/api/games/{gameId}", cancellationToken);

    public Task SubmitClueAsync(int gameId, string word, int number, CancellationToken cancellationToken = default) =>
        api.PostAsync($"/api/games/{gameId}/clue", new { word, number }, cancellationToken);

    public Task SubmitVoteAsync(int gameId, int wordId, CancellationToken cancellationToken = default) =>
        api.PostAsync($"/api/games/{gameId}/vote", new { wordId }, cancellationToken);
}
