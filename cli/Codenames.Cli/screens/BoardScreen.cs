using Codenames.Cli.Api;
using Codenames.Cli.Enums;
using Codenames.Cli.Lobby;
using Codenames.Cli.Models;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Codenames.Cli.Screens;

public enum BoardAction { None, Escape, GiveClue, CardSelected }
public record BoardResult(BoardAction Action, WordCard? SelectedCard = null);

public class BoardScreen : IScreen
{
    private readonly GameApiClient _gameApiClient;
    private readonly TerminalRenderer _renderer;
    private readonly INavigator _navigator;
    private readonly LobbySession _lobbySession;
    private readonly ILogger<BoardScreen> _logger;
    private readonly ClueManager _clueManager;

    public BoardScreen(
        GameApiClient gameApiClient,
        TerminalRenderer renderer,
        INavigator navigator,
        LobbySession lobbySession,
        ILogger<BoardScreen> logger)
    {
        _gameApiClient = gameApiClient;
        _renderer = renderer;
        _navigator = navigator;
        _lobbySession = lobbySession;
        _logger = logger;
        _clueManager = new ClueManager(gameApiClient, renderer);
    }

    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        _renderer.Clear();

        try
        {
            if (_lobbySession.CurrentGameId is not { } gameId)
            {
                _renderer.RenderError("No active game found.");
                _renderer.RenderStatus("Press any key to return...");
                Console.ReadKey(intercept: true);
                await _navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
                return;
            }

            _renderer.RenderStatus($"Fetching game state for game {gameId}...");

            var gameStateTask = _gameApiClient.GetStateAsync(gameId, cancellationToken);
            var identityTask = _gameApiClient.GetMyIdentityAsync(gameId, cancellationToken);
            await Task.WhenAll(gameStateTask, identityTask);

            var gameState = gameStateTask.Result;
            var identity = identityTask.Result;

            if (gameState.Words == null || gameState.Words.Count == 0)
            {
                _renderer.RenderError("No board found. Please create a game first.");
                _renderer.RenderBlankLine();
                _renderer.RenderStatus("Press any key to return...");
                Console.ReadKey(intercept: true);
                await _navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
                return;
            }

            var cards = gameState.Words.Select(w => new WordCard(
                w.Id,
                w.Word,
                w.Category is not null
                    ? Enum.Parse<WordCategory>(w.Category, ignoreCase: true)
                    : WordCategory.NEUTRAL,
                w.Revealed
            )).ToList();

            var role = identity.RoleName;
            var isSpymaster = role?.Equals("SPYMASTER", StringComparison.OrdinalIgnoreCase) ?? false;
            var teamName = identity.TeamName ?? "unknown";

            await RunBoardLoopAsync(
                gameId, cards,
                gameState.Clues,
                gameState.CurrentClueWord,
                gameState.CurrentClueNumber,
                isSpymaster, teamName, role,
                cancellationToken);

            await _navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load board");
            _renderer.RenderBlankLine();
            _renderer.RenderError($"Failed to load board: {ex.Message}");
            _renderer.RenderBlankLine();
            _renderer.RenderStatus("Press any key to return...");
            Console.ReadKey(intercept: true);
            await _navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
        }
    }

    private async Task RunBoardLoopAsync(
        int gameId,
        List<WordCard> cards,
        List<ClueResponse>? clues,
        string? currentClueWord,
        int? currentClueNumber,
        bool isSpymaster,
        string teamName,
        string? role,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            _renderer.Clear();
            AnsiConsole.Write(new FigletText("Codenames").Color(Color.Blue));
            _renderer.RenderBlankLine();
            AnsiConsole.Write(new Markup($"[yellow]Your role:[/] {role ?? "unknown"} ([bold]{teamName}[/] team)"));
            _renderer.RenderBlankLine();

            if (isSpymaster)
                AnsiConsole.Write(new Markup("[yellow]Spymaster mode - press C to give clue, Esc to exit[/]"));
            else
                AnsiConsole.Write(new Markup("[yellow]Operative mode - arrows to select, Enter to vote, Esc to exit[/]"));

            _renderer.RenderBlankLine();
            AnsiConsole.Write(new Markup(
                "Key: [red]Red[/] = Red team  [blue]Blue[/] = Blue team  " +
                "[sandybrown]Brown[/] = Neutral  [white]White[/] = Assassin  [grey]Grey[/] = Unrevealed"));
            _renderer.RenderBlankLine();

            _clueManager.RenderClueHistory(clues);

            var board = new Board(cards, _renderer, showColors: isSpymaster, currentClueWord, currentClueNumber);
            var result = board.Run();

            switch (result.Action)
            {
                case BoardAction.Escape:
                    return;

                case BoardAction.GiveClue when isSpymaster:
                    await _clueManager.SubmitClueAsync(gameId, cancellationToken);
                    break;

                case BoardAction.CardSelected when !isSpymaster && result.SelectedCard != null:
                    try
                    {
                        await _gameApiClient.SubmitVoteAsync(gameId, result.SelectedCard.Id, cancellationToken);
                        _renderer.RenderStatus($"Voted for: {result.SelectedCard.Word}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to submit vote");
                        _renderer.RenderError($"Failed to vote: {ex.Message}");
                    }
                    _renderer.RenderStatus("Press any key to continue...");
                    Console.ReadKey(intercept: true);
                    break;
            }

            await Task.Delay(100, cancellationToken);
        }
    }
}