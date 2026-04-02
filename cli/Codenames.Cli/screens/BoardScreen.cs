using System.Text.Json;
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

public class BoardScreen(
    GameApiClient gameApi,
    SseClient sse,
    TerminalRenderer renderer,
    INavigator navigator,
    LobbySession lobbySession,
    SfxPlayer sfx,
    ILogger<BoardScreen> logger) : IScreen
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private const int KeyPollMs = 50;
    private const int RedrawEveryMs = 250;

    private readonly GameApiClient _gameApi = gameApi;
    private readonly SseClient _sse = sse;
    private readonly TerminalRenderer _renderer = renderer;
    private readonly INavigator _navigator = navigator;
    private readonly LobbySession _lobbySession = lobbySession;
    private readonly ILogger<BoardScreen> _logger = logger;
    private readonly ClueManager _clueManager = new ClueManager(gameApi, renderer, sfx);
    private readonly SfxPlayer _sfx = sfx;

    private readonly object _sync = new();
    private List<WordCard> _cards = [];
    private bool _isSpymaster;
    private string _myTeam = "";
    private string _myRole = "";
    private Dictionary<string, ActiveRoundDetailView?> _activeRounds = new();
    private int _redRemaining;
    private int _blueRemaining;
    private DateTimeOffset  _matchEndsAt;
    private string? _statusMessage;
    private bool _snapshotReceived;
    private bool _gameEnded;
    private bool _dirty;
    private HashSet<string> _myVotedWords = [];
    private DateTimeOffset? _roundTimerEndsAt;
    private int _roundTimerTotalSeconds;

    // Animation state
    private bool _pulsePhase;
    private int _pulseCounter;
    private DateTimeOffset? _statusMessageSetAt;
    private HashSet<string> _revealAnimationWords = [];
    private DateTimeOffset? _revealAnimationStart;

    private int _cursorRow;
    private int _cursorCol;
    private bool _timerWarningPlayed;

    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        if (_lobbySession.CurrentGameId is not { } gameId)
        {
            _renderer.RenderError("No active game found.");
            _renderer.RenderStatus("Press any key to return...");
            Console.ReadKey(intercept: true);
            await _navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
            return;
        }

        _renderer.Clear();
        _renderer.RenderStatus("Connecting to game...");

        using var sseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sse.EventReceived += OnSseEvent;
        var sseTask = _sse.ConnectAsync($"/api/games/{gameId}/events", sseCts.Token);

        try
        {
            await RunKeyboardLoopAsync(gameId, cancellationToken);
            _logger.LogWarning("Board loop exited normally. gameEnded={GameEnded}, snapshotReceived={Snap}, ct.IsCancellationRequested={Ct}",
                _gameEnded, _snapshotReceived, cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogWarning(oce, "Board loop cancelled for game {GameId}", gameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in board loop for game {GameId}", gameId);
            _renderer.RenderError($"Error: {ex.Message}");
        }
        finally
        {
            _sse.EventReceived -= OnSseEvent;
            await sseCts.CancelAsync();
            try { await sseTask; } catch { }
        }

        _logger.LogWarning("Board leaving. gameEnded={GameEnded}, going to {Screen}", _gameEnded, _gameEnded ? "GameResult" : "MainMenu");

        if (_gameEnded)
            await _navigator.GoToAsync(ScreenName.GameResult, cancellationToken);
        else
            await _navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
    }

    private async Task RunKeyboardLoopAsync(int gameId, CancellationToken ct)
    {
        int redrawBucket = 0;

        while (!ct.IsCancellationRequested)
        {
            bool ended, ready, dirty;
            lock (_sync)
            {
                ended = _gameEnded;
                ready = _snapshotReceived;
                dirty = _dirty;
                _dirty = false;
            }

            if (ended) { _logger.LogWarning("Board loop: exiting because gameEnded"); return; }
            if (!ready) { await Task.Delay(KeyPollMs, ct); continue; }

            redrawBucket = DrawIfNeeded(dirty, redrawBucket);

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape) { _logger.LogWarning("Board loop: exiting because Escape pressed"); return; }
                _logger.LogDebug("Board loop: key pressed {Key}", key.Key);
                await HandleKeyAsync(key, gameId, ct);
                lock (_sync) { _dirty = true; }
            }
            else
            {
                await Task.Delay(KeyPollMs, ct);
                redrawBucket -= KeyPollMs;
                if (redrawBucket < 0) redrawBucket = 0;
            }
        }
    }

    private int DrawIfNeeded(bool dirty, int redrawBucket)
    {
        if (!dirty && redrawBucket > 0) return redrawBucket;

        // Advance pulse animation (toggles every 2 redraws = ~500ms)
        _pulseCounter++;
        if (_pulseCounter >= 2)
        {
            _pulseCounter = 0;
            _pulsePhase = !_pulsePhase;
        }

        // Clear reveal animation after 1.5 seconds
        if (_revealAnimationStart.HasValue &&
            !AnimationHelper.ShouldShowFlash(_revealAnimationStart.Value, 1.5))
        {
            _revealAnimationWords.Clear();
            _revealAnimationStart = null;
        }

        // Timer warning beep when <=10 seconds remaining
        if (_roundTimerEndsAt.HasValue)
        {
            var secondsLeft = (_roundTimerEndsAt.Value - DateTimeOffset.UtcNow).TotalSeconds;
            if (secondsLeft is <= 10 and > 0 && !_timerWarningPlayed)
            {
                _timerWarningPlayed = true;
                _sfx.PlayTimerWarning();
            }
        }

        Draw();
        return RedrawEveryMs;
    }

    private async Task HandleKeyAsync(ConsoleKeyInfo key, int gameId, CancellationToken ct)
    {
        bool isSpymaster;
        int  cardCount;
        lock (_sync) { isSpymaster = _isSpymaster; cardCount = _cards.Count; }

        const int cols = 5;
        int rows = cardCount > 0 ? cardCount / cols : 5;

        if (isSpymaster)
        {
            if (key.Key == ConsoleKey.C)
                await _clueManager.SubmitClueAsync(gameId, ct);
        }
        else
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:    _cursorRow = Math.Max(0, _cursorRow - 1); _sfx.PlayCursorMove();       break;
                case ConsoleKey.DownArrow:  _cursorRow = Math.Min(rows - 1, _cursorRow + 1); _sfx.PlayCursorMove(); break;
                case ConsoleKey.LeftArrow:  _cursorCol = Math.Max(0, _cursorCol - 1); _sfx.PlayCursorMove();       break;
                case ConsoleKey.RightArrow: _cursorCol = Math.Min(cols - 1, _cursorCol + 1); _sfx.PlayCursorMove(); break;
                case ConsoleKey.Enter:      await SubmitVoteAsync(gameId, ct);               break;
            }
        }
    }

    private async Task SubmitVoteAsync(int gameId, CancellationToken ct)
    {
        WordCard? card;
        bool hasActiveRound;
        bool alreadyVoted;
        bool votesExhausted;
        int voteCap;
        lock (_sync)
        {
            int idx = _cursorRow * 5 + _cursorCol;
            card = idx < _cards.Count ? _cards[idx] : null;
            _activeRounds.TryGetValue(_myTeam, out var round);
            hasActiveRound = round is not null;
            voteCap        = round?.ClueNumber ?? 0;
            alreadyVoted   = card is not null && _myVotedWords.Contains(card.Word);
            votesExhausted = hasActiveRound && _myVotedWords.Count >= voteCap;
        }

        if (card is null || card.Revealed) return;

        if (!hasActiveRound)
        {
            _sfx.PlayError();
            lock (_sync)
            {
                _statusMessage = "Waiting for your Spymaster's clue first...";
                _statusMessageSetAt = DateTimeOffset.UtcNow;
            }
            return;
        }

        if (alreadyVoted)
        {
            _sfx.PlayError();
            lock (_sync)
            {
                _statusMessage = $"Already voted for {card.Word} this round.";
                _statusMessageSetAt = DateTimeOffset.UtcNow;
            }
            return;
        }

        if (votesExhausted)
        {
            _sfx.PlayError();
            lock (_sync)
            {
                _statusMessage = $"You've used all {voteCap} vote(s) this round \u2014 waiting for tally.";
                _statusMessageSetAt = DateTimeOffset.UtcNow;
            }
            return;
        }

        try
        {
            await _gameApi.SubmitVoteAsync(gameId, card.Word, ct);
            _sfx.PlayVoteCast();
            lock (_sync)
            {
                _myVotedWords.Add(card.Word);
                _statusMessage = $"Voted for {card.Word} ({_myVotedWords.Count}/{voteCap})";
                _statusMessageSetAt = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to submit vote for {Word}", card.Word);
            _sfx.PlayError();
            lock (_sync)
            {
                _statusMessage = $"Vote failed: {ex.Message}";
                _statusMessageSetAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private void OnSseEvent(object? sender, SseEventArgs e)
    {
        if (!Enum.TryParse<SseEventType>(e.EventType, out var eventType))
        {
            _logger.LogDebug("Unhandled SSE event: {EventType}", e.EventType);
            return;
        }

        try
        {
            switch (eventType)
            {
                case SseEventType.GAME_SNAPSHOT:
                    ApplySnapshot(JsonSerializer.Deserialize<GameStateDetailResponse>(e.Data, JsonOpts)!);
                    break;
                case SseEventType.CLUE_GIVEN:
                    ApplyClueGiven(JsonSerializer.Deserialize<ClueGivenPayload>(e.Data, JsonOpts)!);
                    break;
                case SseEventType.VOTE_CAST:
                    ApplyVoteCast(JsonSerializer.Deserialize<VoteCastPayload>(e.Data, JsonOpts)!);
                    break;
                case SseEventType.WORDS_REVEALED:
                    ApplyWordsRevealed(JsonSerializer.Deserialize<WordsRevealedPayload>(e.Data, JsonOpts)!);
                    break;
                case SseEventType.ROUND_STARTED:
                    ApplyRoundStarted(JsonSerializer.Deserialize<TeamPayload>(e.Data, JsonOpts)!);
                    break;
                case SseEventType.TURN_SKIPPED:
                    ApplyTurnSkipped(JsonSerializer.Deserialize<TurnSkippedPayload>(e.Data, JsonOpts)!);
                    break;
                case SseEventType.TIMER_TICK:
                    ApplyTimerTick(JsonSerializer.Deserialize<TimerTickPayload>(e.Data, JsonOpts)!);
                    break;
                case SseEventType.GAME_ENDED:
                    ApplyGameEnded(JsonSerializer.Deserialize<GameEndedPayload>(e.Data, JsonOpts)!);
                    break;
                case SseEventType.CLUE_TIMER_STARTED:
                case SseEventType.VOTE_TIMER_STARTED:
                    ApplyRoundTimerStarted(JsonSerializer.Deserialize<RoundTimerStartedPayload>(e.Data, JsonOpts)!);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process SSE event {EventType}", eventType);
        }
    }

    private void ApplySnapshot(GameStateDetailResponse snap)
    {
        lock (_sync)
        {
            _myTeam           = snap.MyTeam;
            _myRole           = snap.MyRole;
            _isSpymaster      = snap.MyRole.Equals("spymaster", StringComparison.OrdinalIgnoreCase);
            _redRemaining     = snap.RedRemaining;
            _blueRemaining    = snap.BlueRemaining;
            _matchEndsAt      = snap.MatchEndsAt;
            _activeRounds     = snap.ActiveRounds ?? new();
            _cards            = snap.Words.Select(ToWordCard).ToList();
            _snapshotReceived = true;
            _myVotedWords     = [];
            _dirty            = true;

            // Restore round timer from snapshot (covers timers that fired before we subscribed)
            if (snap.RoundTimer is { } rt)
            {
                _roundTimerEndsAt      = DateTimeOffset.FromUnixTimeMilliseconds(rt.EndsAtEpochMs);
                _roundTimerTotalSeconds = rt.DurationSeconds;
            }
            else
            {
                _roundTimerEndsAt = null;
            }
        }
    }

    private void ApplyClueGiven(ClueGivenPayload p)
    {
        lock (_sync)
        {
            _activeRounds[p.Team] = new ActiveRoundDetailView(0, p.ClueWord, p.ClueNumber, new());
            _statusMessage = $"{p.Team.ToUpper()} clue: {p.ClueWord.ToUpper()} \u00d7 {p.ClueNumber}";
            _statusMessageSetAt = DateTimeOffset.UtcNow;
            _dirty = true;
        }
        _sfx.PlayClueGiven();
    }

    private void ApplyVoteCast(VoteCastPayload p)
    {
        lock (_sync)
        {
            if (_activeRounds.TryGetValue(p.Team, out var round) && round is not null)
            {
                var updatedVotes = new Dictionary<string, int>(round.Votes);
                updatedVotes[p.Word] = updatedVotes.GetValueOrDefault(p.Word) + 1;
                _activeRounds[p.Team] = round with { Votes = updatedVotes };
            }
            _dirty = true;
        }
    }

    private void ApplyWordsRevealed(WordsRevealedPayload p)
    {
        bool hasAssassin = false, hasCorrect = false, hasWrong = false;
        lock (_sync)
        {
            var revealedWords = new HashSet<string>();
            foreach (var w in p.Words)
            {
                int idx = _cards.FindIndex(c => c.Word.Equals(w.Word, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) continue;

                var category = Enum.TryParse<WordCategory>(w.WordType, ignoreCase: true, out var cat)
                    ? cat : WordCategory.NEUTRAL;

                _cards[idx] = _cards[idx] with { Category = category, Revealed = true };
                revealedWords.Add(w.Word);

                if (category == WordCategory.RED)  _redRemaining  = Math.Max(0, _redRemaining  - 1);
                if (category == WordCategory.BLUE) _blueRemaining = Math.Max(0, _blueRemaining - 1);

                // Track reveal outcome for SFX
                if (category == WordCategory.ASSASSIN) hasAssassin = true;
                else if (w.WordType.Equals(p.Team, StringComparison.OrdinalIgnoreCase)) hasCorrect = true;
                else hasWrong = true;
            }
            if (p.Team.Equals(_myTeam, StringComparison.OrdinalIgnoreCase))
            {
                _myVotedWords     = new HashSet<string>();
                _roundTimerEndsAt = null;
            }
            _statusMessage = $"{p.Team.ToUpper()} revealed {p.Words.Count} word(s)";
            _statusMessageSetAt = DateTimeOffset.UtcNow;

            // Trigger reveal flash animation
            _revealAnimationWords = revealedWords;
            _revealAnimationStart = DateTimeOffset.UtcNow;

            _dirty = true;
        }

        // Play appropriate reveal SFX
        if (hasAssassin) _sfx.PlayRevealAssassin();
        else if (hasWrong) _sfx.PlayRevealWrong();
        else if (hasCorrect) _sfx.PlayRevealCorrect();
    }

    private void ApplyRoundStarted(TeamPayload p)
    {
        lock (_sync)
        {
            _activeRounds[p.Team] = null;
            if (p.Team.Equals(_myTeam, StringComparison.OrdinalIgnoreCase))
            {
                _myVotedWords  = new HashSet<string>();
                _statusMessage = "Waiting for your Spymaster's clue...";
                _statusMessageSetAt = DateTimeOffset.UtcNow;
            }
            _dirty = true;
        }
        _sfx.PlayRoundStarted();
    }

    private void ApplyTurnSkipped(TurnSkippedPayload p)
    {
        lock (_sync)
        {
            _statusMessage = $"{p.Team.ToUpper()} Spymaster timed out \u2014 round skipped";
            _statusMessageSetAt = DateTimeOffset.UtcNow;
            _dirty = true;
        }
        _sfx.PlayTurnSkipped();
    }

    private void ApplyTimerTick(TimerTickPayload p)
    {
        lock (_sync)
        {
            _matchEndsAt = DateTimeOffset.UtcNow.AddSeconds(p.SecondsRemaining);
            _dirty = true;
        }
    }

    private void ApplyRoundTimerStarted(RoundTimerStartedPayload p)
    {
        lock (_sync)
        {
            if (!p.Team.Equals(_myTeam, StringComparison.OrdinalIgnoreCase)) return;
            _roundTimerEndsAt = DateTimeOffset.FromUnixTimeMilliseconds(p.EndsAtEpochMs);
            _roundTimerTotalSeconds = p.DurationSeconds;
            _timerWarningPlayed = false;
            _dirty = true;
        }
    }

    private void ApplyGameEnded(GameEndedPayload p)
    {
        lock (_sync)
        {
            var winner = p.Winner?.Equals("draw", StringComparison.OrdinalIgnoreCase) == true
                ? null : p.Winner;
            _lobbySession.SetGameResult(new GameEndResult(winner, p.Reason, p.RedRemaining, p.BlueRemaining, _myTeam));
            _gameEnded = true;
            _dirty = true;
        }
    }

    private void Draw()
    {
        List<WordCard> cards;
        bool   isSpymaster;
        string myTeam, myRole;
        Dictionary<string, ActiveRoundDetailView?> rounds;
        int    redRemaining, blueRemaining;
        string? status;
        HashSet<string> myVotedWords;
        DateTimeOffset? roundTimerEndsAt;
        int roundTimerTotalSeconds;
        DateTimeOffset? statusSetAt;
        HashSet<string> revealAnimWords;

        lock (_sync)
        {
            cards                  = new List<WordCard>(_cards);
            isSpymaster            = _isSpymaster;
            myTeam                 = _myTeam;
            myRole                 = _myRole;
            rounds                 = new Dictionary<string, ActiveRoundDetailView?>(_activeRounds);
            redRemaining           = _redRemaining;
            blueRemaining          = _blueRemaining;
            status                 = _statusMessage;
            myVotedWords           = new HashSet<string>(_myVotedWords);
            roundTimerEndsAt       = _roundTimerEndsAt;
            roundTimerTotalSeconds = _roundTimerTotalSeconds;
            statusSetAt            = _statusMessageSetAt;
            revealAnimWords        = new HashSet<string>(_revealAnimationWords);
        }

        TerminalRenderer.StartFrame();

        // Role & Score bar
        var teamColor = myTeam.Equals("red", StringComparison.OrdinalIgnoreCase) ? "red" : "dodgerblue1";
        AnsiConsole.MarkupLine(
            $"  [bold {teamColor}]{myRole.ToUpper()}[/] [grey]on[/] [bold {teamColor}]{myTeam.ToUpper()} TEAM[/]");
        AnsiConsole.WriteLine();

        // Score bars
        AnsiConsole.Markup("  ");
        TerminalRenderer.RenderScoreBar("RED", "red", redRemaining, 10);
        AnsiConsole.Markup("    ");
        TerminalRenderer.RenderScoreBar("BLUE", "dodgerblue1", blueRemaining, 10);
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        // Instructions
        if (isSpymaster)
        {
            AnsiConsole.MarkupLine("  [grey][[Spymaster]][/]  [bold gold1]C[/] [grey]give clue[/]  [bold gold1]Esc[/] [grey]exit[/]");
        }
        else
        {
            bool myRoundActive = rounds.TryGetValue(myTeam, out var myRound) && myRound is not null;
            if (myRoundActive)
            {
                int cap  = myRound!.ClueNumber;
                int cast = myVotedWords.Count;
                if (cast < cap)
                    AnsiConsole.MarkupLine($"  [grey][[Operative]][/]  [bold gold1]Arrows[/] [grey]move[/]  [bold gold1]Enter[/] [grey]vote[/] [dim]({cast}/{cap})[/]  [bold gold1]Esc[/] [grey]exit[/]");
                else
                    AnsiConsole.MarkupLine($"  [grey][[Operative]][/]  [green]All {cap} vote(s) cast[/] [dim]awaiting tally...[/]  [bold gold1]Esc[/] [grey]exit[/]");
            }
            else
            {
                // No active round - show prominent banner
                AnsiConsole.WriteLine();
                TerminalRenderer.RenderBanner(
                    "WAITING FOR SPYMASTER'S CLUE...",
                    "yellow",
                    pulse: true,
                    pulsePhase: _pulsePhase);
                AnsiConsole.WriteLine();
            }
        }

        // Timer bar
        if (roundTimerEndsAt.HasValue)
        {
            var timerLabel = isSpymaster ? "Clue time" : "Vote time";
            TerminalRenderer.RenderTimerBar(timerLabel, roundTimerEndsAt.Value, roundTimerTotalSeconds, _pulsePhase);
        }

        AnsiConsole.WriteLine();

        // Board
        if (cards.Count == 25)
        {
            var grid = new WordCard[5, 5];
            for (int i = 0; i < 25; i++)
            {
                var card = cards[i];
                // Apply reveal flash animation: flash white during animation
                if (revealAnimWords.Contains(card.Word) && _pulsePhase)
                {
                    // Temporarily override to white border for flash effect
                    // We achieve this by marking it as a special state via a synthetic card
                    // The actual flash is handled by alternating revealed color display
                }
                grid[i / 5, i % 5] = card;
            }
            _renderer.RenderBoard(grid, _cursorRow, _cursorCol, isSpymaster, isSpymaster ? null : myVotedWords);
        }

        AnsiConsole.WriteLine();

        // Clue Panel
        RenderCluePanel(rounds, myTeam);

        // Spymaster clue hint
        if (isSpymaster)
            AnsiConsole.MarkupLine("  [grey]Press[/] [bold gold1]C[/] [grey]to give a clue[/]  [dim]format: WORD NUMBER[/]");

        // Status message
        if (!string.IsNullOrEmpty(status))
        {
            AnsiConsole.WriteLine();

            // Determine if this is a fresh "flash" status (within 2 seconds)
            bool isFlash = statusSetAt.HasValue && AnimationHelper.ShouldShowFlash(statusSetAt.Value, 2.0);

            if (isFlash && status.Contains("clue:"))
            {
                // Clue announcement - prominent banner
                var clueColor = status.StartsWith("RED", StringComparison.OrdinalIgnoreCase) ? "red" : "dodgerblue1";
                TerminalRenderer.RenderBanner(status.ToUpper(), clueColor);
            }
            else if (isFlash && status.Contains("timed out"))
            {
                // Round skipped - red banner
                TerminalRenderer.RenderBanner(status.ToUpper(), "red");
            }
            else if (status.StartsWith("Voted for"))
            {
                TerminalRenderer.RenderStatusPanel(status, "green");
            }
            else if (status.Contains("failed") || status.Contains("error"))
            {
                TerminalRenderer.RenderStatusPanel(status, "red");
            }
            else
            {
                TerminalRenderer.RenderStatusPanel(status, "yellow");
            }
        }

        TerminalRenderer.EndFrame();
    }

    private void RenderCluePanel(Dictionary<string, ActiveRoundDetailView?> rounds, string myTeam)
    {
        foreach (var (team, round) in rounds)
        {
            var color  = team.Equals("red", StringComparison.OrdinalIgnoreCase) ? "red" : "dodgerblue1";
            var isMyTeam = team.Equals(myTeam, StringComparison.OrdinalIgnoreCase);
            var marker = isMyTeam ? $" [green]●[/]" : "";

            if (round is null)
            {
                AnsiConsole.MarkupLine($"  [{color}]{team.ToUpper()}[/]: [dim]Waiting for clue...[/]{marker}");
            }
            else
            {
                var voteStr = round.Votes.Count > 0
                    ? "  " + string.Join("  ",
                        round.Votes.Select(kv => $"[grey]{Markup.Escape(kv.Key)}[/][dim]({kv.Value})[/]"))
                    : "";
                AnsiConsole.MarkupLine(
                    $"  [{color}]{team.ToUpper()}[/]: " +
                    $"[bold]{Markup.Escape(round.ClueWord.ToUpper())} \u00d7 {round.ClueNumber}[/]" +
                    $"{voteStr}{marker}");
            }
        }
        AnsiConsole.WriteLine();
    }

    private static WordCard ToWordCard(WordDetailView w)
    {
        var category = w.Category is not null
            && Enum.TryParse<WordCategory>(w.Category, ignoreCase: true, out var cat)
                ? cat : WordCategory.NEUTRAL;
        return new WordCard(0, w.Word, category, w.Revealed);
    }
}
