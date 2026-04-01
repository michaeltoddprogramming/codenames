using Codenames.Cli.Api;
using Codenames.Cli.Enums;
using Codenames.Cli.Lobby;
using Codenames.Cli.Models;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Screens;

public class BoardScreen(
    GameApiClient gameApiClient,
    TerminalRenderer renderer,
    INavigator navigator,
    LobbySession lobbySession,
    ILogger<BoardScreen> logger) : IScreen
{
    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        renderer.Clear();

        try
        {
            if (lobbySession.CurrentGameId is not { } gameId)
            {
                renderer.RenderError("No active game found.");
                renderer.RenderStatus("Press any key to return...");
                Console.ReadKey(intercept: true);
                await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
                return;
            }

            renderer.RenderStatus($"Fetching game state for game {gameId}...");

            var gameStateTask = gameApiClient.GetStateAsync(gameId, cancellationToken);
            var identityTask = gameApiClient.GetMyIdentityAsync(gameId, cancellationToken);
            await Task.WhenAll(gameStateTask, identityTask);

            var gameState = gameStateTask.Result;
            var identity = identityTask.Result;

            if (gameState.Words == null || gameState.Words.Count == 0)
            {
                renderer.RenderError("No board found. Please create a game first.");
                renderer.RenderBlankLine();
                renderer.RenderStatus("Press any key to return...");
                Console.ReadKey(intercept: true);
                await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
                return;
            }

            var cards = gameState.Words.Select(w => new WordCard(
                w.Word,
                w.Category is not null
                    ? Enum.Parse<WordCategory>(w.Category, ignoreCase: true)
                    : WordCategory.NEUTRAL,
                w.Revealed
            )).ToList();

            var role = identity.RoleName;
            var isSpymaster = role?.Equals("SPYMASTER", StringComparison.OrdinalIgnoreCase) ?? false;
            renderer.RenderStatus($"Your role: {role ?? "unknown"} (Team: {identity.TeamName})");

            var selectedCard = new Board(cards, renderer, showColors: isSpymaster).Run();

            if (selectedCard != null)
                renderer.RenderStatus($"You selected: {selectedCard.Word}");

            renderer.RenderStatus("Press any key to continue...");
            Console.ReadKey(intercept: true);
            await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load board");
            renderer.RenderBlankLine();
            renderer.RenderError($"Failed to load board: {ex.Message}");
            renderer.RenderBlankLine();
            renderer.RenderStatus("Press any key to return...");
            Console.ReadKey(intercept: true);
            await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
        }
    }
}
