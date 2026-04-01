using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Lobby;
using Codenames.Cli.Models;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Text.Json;

namespace Codenames.Cli.Screens;

public class LobbyRoomScreen(
    AuthSession authSession,
    LobbySession lobbySession,
    LobbyApiClient lobbyApiClient,
    GameApiClient gameApiClient,
    SseClient sseClient,
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator,
    ILogger<LobbyRoomScreen> logger) : IScreen
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _sync = new();
    private LobbyStateResponse? _lobby;
    private int? _startedGameId;
    private bool _startRequested;
    private bool _connected;
    private bool _hasConnected;
    private bool _dirty;
    private int _userId;
    private CancellationTokenSource? _streamCts;

    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        _lobby = lobbySession.CurrentLobby;
        if (_lobby is null)
        {
            await ShowErrorAndWaitAsync("No active lobby found.", cancellationToken);
            return;
        }

        if (authSession.UserId is not { } userId)
        {
            await ShowErrorAndWaitAsync("User identity unavailable. Please login again.", cancellationToken);
            return;
        }

        _userId = userId;
        _startRequested = false;
        _startedGameId = null;
        _connected = false;
        _hasConnected = false;
        _dirty = true;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _streamCts = linkedCts;
        sseClient.EventReceived += OnEventReceived;
        sseClient.ConnectionStateChanged += OnConnectionStateChanged;
        var streamTask = sseClient.ConnectAsync($"/api/lobbies/{_lobby.LobbyId}/events", linkedCts.Token);

        try
        {
            var result = await RunLobbyLoopAsync(cancellationToken);

            if (result == LobbyExitReason.GameStarted && _startedGameId.HasValue)
            {
                lobbySession.Clear();
                await ShowGameIdentityAsync(_startedGameId.Value, cancellationToken);
            }
            else if (result == LobbyExitReason.UserLeft)
            {
                await LeaveLobbyAsync(cancellationToken);
            }
        }
        finally
        {
            await linkedCts.CancelAsync();
            _streamCts = null;
            sseClient.EventReceived -= OnEventReceived;
            sseClient.ConnectionStateChanged -= OnConnectionStateChanged;
            try { await streamTask; }
            catch (Exception ex) { logger.LogDebug(ex, "SSE stream shutdown"); }
        }
    }

    private async Task<LobbyExitReason> RunLobbyLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            LobbyStateResponse currentLobby;
            bool connected;
            bool dirty;
            lock (_sync)
            {
                if (_startedGameId.HasValue)
                    return LobbyExitReason.GameStarted;
                currentLobby = _lobby!;
                connected = _connected;
                dirty = _dirty;
                _dirty = false;
            }

            if (dirty)
                DrawLobby(currentLobby, lobbySession.IsHost, connected);

            if (lobbySession.IsHost && !_startRequested)
            {
                var hostResult = await PromptAndStartGameAsync(currentLobby, cancellationToken);
                if (hostResult == LobbyExitReason.GameStarted)
                    return LobbyExitReason.GameStarted;
                if (hostResult == LobbyExitReason.UserLeft)
                    return LobbyExitReason.UserLeft;
            }

            if (await WaitOrEscapeAsync(750, cancellationToken))
                return LobbyExitReason.UserLeft;
        }

        return LobbyExitReason.Cancelled;
    }

    private async Task<LobbyExitReason> PromptAndStartGameAsync(
        LobbyStateResponse currentLobby, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            LobbyStateResponse latestLobby;
            bool connected;
            bool dirty;
            lock (_sync)
            {
                if (_startedGameId.HasValue)
                    return LobbyExitReason.GameStarted;
                latestLobby = _lobby ?? currentLobby;
                connected = _connected;
                dirty = _dirty;
                _dirty = false;
            }

            if (dirty)
            {
                DrawLobby(latestLobby, lobbySession.IsHost, connected);
                renderer.RenderBlankLine();
                renderer.RenderStatus("Press Enter to start the game when ready (Esc to leave)...");
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                else if (key.Key == ConsoleKey.Escape)
                    return LobbyExitReason.UserLeft;
            }

            await Task.Delay(50, cancellationToken);
        }

        lock (_sync)
        {
            if (_startedGameId.HasValue)
                return LobbyExitReason.GameStarted;
            currentLobby = _lobby ?? currentLobby;
        }

        if (currentLobby.Participants.Count < 4)
        {
            renderer.RenderError("At least 4 players are required to start the game.");
            return LobbyExitReason.Continue;
        }

        _startRequested = true;
        renderer.RenderStatus("Starting game...");

        try
        {
            var startResponse = await lobbyApiClient.StartAsync(currentLobby.LobbyId, cancellationToken);
            lock (_sync) { _startedGameId ??= startResponse.GameId; }
            return LobbyExitReason.GameStarted;
        }
        catch (Exception ex)
        {
            _startRequested = false;
            logger.LogWarning(ex, "Failed to start game for lobby {LobbyId}", currentLobby.LobbyId);
            renderer.RenderError($"Failed to start game: {ex.Message}");
            return LobbyExitReason.Continue;
        }
    }

    private async Task<bool> WaitOrEscapeAsync(int delayMs, CancellationToken cancellationToken)
    {
        var elapsed = 0;
        while (elapsed < delayMs && !cancellationToken.IsCancellationRequested)
        {
            lock (_sync)
            {
                if (_startedGameId.HasValue)
                    return false;
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape)
                    return true;
            }

            await Task.Delay(50, cancellationToken);
            elapsed += 50;
        }
        return false;
    }

    private async Task LeaveLobbyAsync(CancellationToken cancellationToken)
    {
        var lobbyId = lobbySession.CurrentLobby?.LobbyId;
        lobbySession.Clear();

        if (lobbyId is null) return;

        try
        {
            await lobbyApiClient.LeaveAsync(lobbyId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to leave lobby {LobbyId}", lobbyId);
        }
    }

    private void OnEventReceived(object? _, SseEventArgs args)
    {
        if (args.EventType.Equals("LOBBY_SNAPSHOT", StringComparison.OrdinalIgnoreCase))
            HandleLobbySnapshot(args.Data);
        else if (args.EventType.Equals("GAME_STARTED", StringComparison.OrdinalIgnoreCase))
            HandleGameStarted(args.Data);
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        lock (_sync)
        {
            _connected = connected;
            if (connected) _hasConnected = true;
            _dirty = true;
        }
    }

    private void HandleLobbySnapshot(string data)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<LobbyStateResponse>(data, JsonOptions);
            if (snapshot is null) return;
            lock (_sync)
            {
                _lobby = snapshot;
                _dirty = true;
                lobbySession.SetLobby(snapshot, _userId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize LOBBY_SNAPSHOT");
        }
    }

    private void HandleGameStarted(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("gameId", out var el) && el.TryGetInt32(out var gameId))
            {
                lock (_sync) { _startedGameId = gameId; }
                _streamCts?.Cancel();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GAME_STARTED event");
        }
    }

    private async Task ShowGameIdentityAsync(int gameId, CancellationToken cancellationToken)
    {
        List<GamePlayerInfo>? players = null;
        string? fetchError = null;

        try
        {
            players = await gameApiClient.GetPlayersAsync(gameId, cancellationToken);
            lobbySession.SetGameId(gameId);
        }
        catch (Exception ex)
        {
            fetchError = ex.Message;
        }

        const int countdownSeconds = 5;
        for (var remaining = countdownSeconds; remaining >= 0; remaining--)
        {
            TerminalRenderer.StartFrame();
            renderer.RenderHeader("Game Starting");
            renderer.RenderBlankLine();

            if (fetchError is not null)
            {
                renderer.RenderError($"Could not load participants: {fetchError}");
            }
            else if (players is not null)
            {
                var byTeam = players
                    .GroupBy(p => p.TeamName, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key);

                foreach (var team in byTeam)
                {
                    AnsiConsole.Write(new Rule($"[bold]{team.Key} Team[/]").RuleStyle("grey"));
                    foreach (var player in team.OrderBy(p => p.RoleName))
                    {
                        var spymasterTag = player.RoleName.Equals("spymaster", StringComparison.OrdinalIgnoreCase)
                            ? " [yellow](Spymaster)[/]"
                            : "";
                        AnsiConsole.MarkupLine($"  {player.Username}{spymasterTag}");
                    }
                    renderer.RenderBlankLine();
                }
            }

            if (remaining > 0)
                renderer.RenderStatus($"Game begins in {remaining}...");
            else
                renderer.RenderStatus("Starting!");

            TerminalRenderer.EndFrame();

            if (remaining > 0)
                await Task.Delay(1000, cancellationToken);
        }

        await navigator.GoToAsync(ScreenName.Board, cancellationToken);
    }

    private async Task ShowErrorAndWaitAsync(string message, CancellationToken cancellationToken)
    {
        renderer.Clear();
        renderer.RenderHeader("Lobby");
        renderer.RenderBlankLine();
        renderer.RenderError(message);
        renderer.RenderStatus("Press any key to continue...");
        await keyboard.ReadKeyAsync(cancellationToken);
    }

    private void DrawLobby(LobbyStateResponse lobby, bool isHost, bool connected)
    {
        renderer.Clear();
        renderer.RenderHeader("Lobby Room");
        renderer.RenderBlankLine();
        renderer.RenderStatus($"Join Code: {lobby.Code}");
        renderer.RenderStatus($"Match duration: {lobby.MatchDurationMinutes} minutes");
        renderer.RenderStatus($"Players in lobby: {lobby.Participants.Count}");
        renderer.RenderStatus(isHost
            ? "You are the host. Press Enter to start when ready."
            : "Waiting for host to start.");

        if (_hasConnected && !connected)
            renderer.RenderError("Connection lost - reconnecting...");

        renderer.RenderBlankLine();
        AnsiConsole.Write(new Rule("Players").RuleStyle("grey"));

        foreach (var participant in lobby.Participants)
        {
            var hostTag = participant.IsHost ? " (host)" : "";
            renderer.RenderStatus($"- {participant.Username}{hostTag}");
        }

        renderer.RenderBlankLine();
        renderer.RenderStatus("Press Esc to leave lobby.");
    }

    private enum LobbyExitReason { Continue, GameStarted, UserLeft, Cancelled }
}
