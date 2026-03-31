using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Lobby;
using Codenames.Cli.Models;
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
    ILogger<LobbyRoomScreen> logger) : IScreen
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _sync = new();
    private LobbyStateResponse? _lobby;
    private int? _startedGameId;
    private bool _startRequested;
    private bool _connected;
    private int _userId;

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

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
            sseClient.EventReceived -= OnEventReceived;
            sseClient.ConnectionStateChanged -= OnConnectionStateChanged;
            try { await streamTask; }
            catch (Exception ex) { logger.LogDebug(ex, "SSE stream shutdown"); }
        }
    }

    private async Task<LobbyExitReason> RunLobbyLoopAsync(CancellationToken cancellationToken)
    {
        renderer.Clear();
        while (!cancellationToken.IsCancellationRequested)
        {
            LobbyStateResponse currentLobby;
            bool connected;
            lock (_sync)
            {
                if (_startedGameId.HasValue)
                    return LobbyExitReason.GameStarted;
                currentLobby = _lobby!;
                connected = _connected;
            }

            DrawLobby(currentLobby, lobbySession.IsHost, connected);

            if (lobbySession.IsHost && IsLobbyFull(currentLobby) && !_startRequested)
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
        renderer.RenderBlankLine();
        renderer.RenderStatus("Lobby is full! Press Enter to start the game (Esc to leave)...");

        while (!cancellationToken.IsCancellationRequested)
        {
            lock (_sync)
            {
                if (_startedGameId.HasValue)
                    return LobbyExitReason.GameStarted;
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                if (key.Key == ConsoleKey.Escape)
                    return LobbyExitReason.UserLeft;
            }

            await Task.Delay(50, cancellationToken);
        }

        lock (_sync)
        {
            if (_startedGameId.HasValue)
                return LobbyExitReason.GameStarted;
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
        lock (_sync) { _connected = connected; }
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
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GAME_STARTED event");
        }
    }

    private async Task ShowGameIdentityAsync(int gameId, CancellationToken cancellationToken)
    {
        renderer.Clear();
        renderer.RenderHeader("Game Starting");
        renderer.RenderBlankLine();

        try
        {
            var identity = await gameApiClient.GetMyIdentityAsync(gameId, cancellationToken);
            renderer.RenderStatus($"Game ID: {identity.GameId}");
            renderer.RenderStatus($"Team: {identity.TeamName}");
            renderer.RenderStatus($"Role: {identity.RoleName}");
        }
        catch (Exception ex)
        {
            renderer.RenderError($"Game started, but failed to load your team/role: {ex.Message}");
        }

        renderer.RenderBlankLine();
        renderer.RenderStatus("Press any key to continue...");
        await keyboard.ReadKeyAsync(cancellationToken);
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

    private static bool IsLobbyFull(LobbyStateResponse lobby) =>
        lobby.Participants.Count >= lobby.PlayersPerTeam * 2;

    private void DrawLobby(LobbyStateResponse lobby, bool isHost, bool connected)
    {
        renderer.Clear();
        renderer.RenderHeader("Lobby Room");
        renderer.RenderBlankLine();
        renderer.RenderStatus($"Join Code: {lobby.Code}");
        renderer.RenderStatus($"Players per team: {lobby.PlayersPerTeam}");
        renderer.RenderStatus($"Match duration: {lobby.MatchDurationMinutes} minutes");
        renderer.RenderStatus($"Players: {lobby.Participants.Count}/{lobby.PlayersPerTeam * 2}");
        renderer.RenderStatus(isHost ? "You are the host." : "Waiting for host to start.");

        if (!connected)
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
