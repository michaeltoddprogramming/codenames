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
    ILogger<BoardScreen> logger) : IScreen
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private const int KeyPollMs = 50;
    private const int RedrawEveryMs = 1000;

    private readonly GameApiClient _gameApi = gameApi;
    private readonly SseClient _sse = sse;
    private readonly TerminalRenderer _renderer = renderer;
    private readonly INavigator _navigator = navigator;
    private readonly LobbySession _lobbySession = lobbySession;
    private readonly ILogger<BoardScreen> _logger = logger;
    private readonly ClueManager _clueManager = new ClueManager(gameApi, renderer);

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

    private int _cursorRow;
    private int _cursorCol;

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
        }
        catch (OperationCanceledException) { }
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

            if (ended) return;
            if (!ready) { await Task.Delay(KeyPollMs, ct); continue; }

            redrawBucket = DrawIfNeeded(dirty, redrawBucket);

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape) return;
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
                case ConsoleKey.UpArrow:    _cursorRow = Math.Max(0, _cursorRow - 1);        break;
                case ConsoleKey.DownArrow:  _cursorRow = Math.Min(rows - 1, _cursorRow + 1); break;
                case ConsoleKey.LeftArrow:  _cursorCol = Math.Max(0, _cursorCol - 1);        break;
                case ConsoleKey.RightArrow: _cursorCol = Math.Min(cols - 1, _cursorCol + 1); break;
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
            lock (_sync) { _statusMessage = "Waiting for your Spymaster's clue first..."; }
            return;
        }

        if (alreadyVoted)
        {
            lock (_sync) { _statusMessage = $"Already voted for {card.Word} this round."; }
            return;
        }

        if (votesExhausted)
        {
            lock (_sync) { _statusMessage = $"You've used all {voteCap} vote(s) this round \u2014 waiting for tally."; }
            return;
        }

        try
        {
            await _gameApi.SubmitVoteAsync(gameId, card.Word, ct);
            lock (_sync)
            {
                _myVotedWords.Add(card.Word);
                _statusMessage = $"Voted for {card.Word} ({_myVotedWords.Count}/{voteCap})";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to submit vote for {Word}", card.Word);
            lock (_sync) { _statusMessage = $"Vote failed: {ex.Message}"; }
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
        }
    }

    private void ApplyClueGiven(ClueGivenPayload p)
    {
        lock (_sync)
        {
            _activeRounds[p.Team] = new ActiveRoundDetailView(0, p.ClueWord, p.ClueNumber, new());
            _statusMessage = $"{p.Team.ToUpper()} clue: {p.ClueWord.ToUpper()} \u00d7 {p.ClueNumber}";
            _dirty = true;
        }
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
        lock (_sync)
        {
            foreach (var w in p.Words)
            {
                int idx = _cards.FindIndex(c => c.Word.Equals(w.Word, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) continue;

                var category = Enum.TryParse<WordCategory>(w.WordType, ignoreCase: true, out var cat)
                    ? cat : WordCategory.NEUTRAL;

                _cards[idx] = _cards[idx] with { Category = category, Revealed = true };

                if (category == WordCategory.RED)  _redRemaining  = Math.Max(0, _redRemaining  - 1);
                if (category == WordCategory.BLUE) _blueRemaining = Math.Max(0, _blueRemaining - 1);
            }
            if (p.Team.Equals(_myTeam, StringComparison.OrdinalIgnoreCase))
                _myVotedWords = new HashSet<string>();
            _statusMessage = $"{p.Team.ToUpper()} revealed {p.Words.Count} word(s)";
            _dirty = true;
        }
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
            }
            _dirty = true;
        }
    }

    private void ApplyTurnSkipped(TurnSkippedPayload p)
    {
        lock (_sync)
        {
            _statusMessage = $"{p.Team.ToUpper()} Spymaster timed out \u2014 round skipped";
            _dirty = true;
        }
    }

    private void ApplyTimerTick(TimerTickPayload p)
    {
        lock (_sync)
        {
            _matchEndsAt = DateTimeOffset.UtcNow.AddSeconds(p.SecondsRemaining);
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
        DateTimeOffset matchEndsAt;
        string? status;
        HashSet<string> myVotedWords;

        lock (_sync)
        {
            cards         = new List<WordCard>(_cards);
            isSpymaster   = _isSpymaster;
            myTeam        = _myTeam;
            myRole        = _myRole;
            rounds        = new Dictionary<string, ActiveRoundDetailView?>(_activeRounds);
            redRemaining  = _redRemaining;
            blueRemaining = _blueRemaining;
            matchEndsAt   = _matchEndsAt;
            status        = _statusMessage;
            myVotedWords  = new HashSet<string>(_myVotedWords);
        }

        TerminalRenderer.StartFrame();
        AnsiConsole.Write(new FigletText("Codenames").Color(Color.Blue));
        _renderer.RenderBlankLine();

        var timeLeft = matchEndsAt - DateTimeOffset.UtcNow;
        var timeStr  = timeLeft.TotalSeconds > 0
            ? $"{(int)timeLeft.TotalMinutes}:{timeLeft.Seconds:D2}"
            : "0:00";

        AnsiConsole.MarkupLine(
            $"[yellow]Role:[/] {myRole.ToUpper()} ([bold]{myTeam.ToUpper()}[/])   " +
            $"[red]Red {redRemaining}[/] / [blue]Blue {blueRemaining}[/]   [grey]\u23f1 {timeStr}[/]");
        _renderer.RenderBlankLine();

        if (isSpymaster)
        {
            AnsiConsole.MarkupLine("[yellow]Spymaster:[/] [bold]C[/] give clue \u00b7 [bold]Esc[/] exit");
        }
        else
        {
            bool myRoundActive = rounds.TryGetValue(myTeam, out var myRound) && myRound is not null;
            if (myRoundActive)
            {
                int cap  = myRound!.ClueNumber;
                int cast = myVotedWords.Count;
                if (cast < cap)
                    AnsiConsole.MarkupLine($"[yellow]Operative:[/] arrows move \u00b7 [bold]Enter[/] vote [grey]({cast}/{cap} cast)[/] \u00b7 [bold]Esc[/] exit");
                else
                    AnsiConsole.MarkupLine($"[yellow]Operative:[/] arrows move \u00b7 [grey]all {cap} vote(s) cast \u2014 awaiting tally[/] \u00b7 [bold]Esc[/] exit");
            }
            else
                AnsiConsole.MarkupLine("[yellow]Operative:[/] arrows move \u00b7 [grey]waiting for Spymaster's clue...[/] \u00b7 [bold]Esc[/] exit");
        }
        _renderer.RenderBlankLine();

        if (cards.Count == 25)
        {
            var grid = new WordCard[5, 5];
            for (int i = 0; i < 25; i++)
                grid[i / 5, i % 5] = cards[i];
            _renderer.RenderBoard(grid, _cursorRow, _cursorCol, isSpymaster, isSpymaster ? null : myVotedWords);
        }

        _renderer.RenderBlankLine();
        RenderCluePanel(rounds, myTeam);

        if (isSpymaster)
            AnsiConsole.MarkupLine("Press [bold]C[/] to give a clue  \u00b7  format: [bold]{word} {number}[/]");

        if (!string.IsNullOrEmpty(status))
        {
            _renderer.RenderBlankLine();
            _renderer.RenderStatus(status);
        }

        TerminalRenderer.EndFrame();
    }

    private void RenderCluePanel(Dictionary<string, ActiveRoundDetailView?> rounds, string myTeam)
    {
        foreach (var (team, round) in rounds)
        {
            var color  = team.Equals("red", StringComparison.OrdinalIgnoreCase) ? "red" : "blue";
            var marker = team.Equals(myTeam, StringComparison.OrdinalIgnoreCase) ? " \u25c4" : "";

            if (round is null)
            {
                AnsiConsole.MarkupLine($"[{color}]{team.ToUpper()}[/]: [dim]Waiting for clue...[/]{marker}");
            }
            else
            {
                var voteStr = round.Votes.Count > 0
                    ? "  votes: " + string.Join("  ",
                        round.Votes.Select(kv => $"{Markup.Escape(kv.Key)}({kv.Value})"))
                    : "";
                AnsiConsole.MarkupLine(
                    $"[{color}]{team.ToUpper()}[/]: " +
                    $"[bold]{Markup.Escape(round.ClueWord.ToUpper())} \u00d7 {round.ClueNumber}[/]" +
                    $"{voteStr}{marker}");
            }
        }
        _renderer.RenderBlankLine();
    }

    private static WordCard ToWordCard(WordDetailView w)
    {
        var category = w.Category is not null
            && Enum.TryParse<WordCategory>(w.Category, ignoreCase: true, out var cat)
                ? cat : WordCategory.NEUTRAL;
        return new WordCard(0, w.Word, category, w.Revealed);
    }
}
