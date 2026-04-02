namespace Codenames.Cli.Tui;

/// <summary>
/// Fire-and-forget sound effects using Console.Beep.
/// Singleton — only one sound plays at a time (new sounds cancel in-flight ones).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class SfxPlayer
{
    private static readonly bool IsSupported = OperatingSystem.IsWindows();
    private CancellationTokenSource _cts = new();
    private readonly object _lock = new();

    private void Play(Action<CancellationToken> sequence)
    {
        if (!IsSupported) return;

        CancellationTokenSource newCts = new();
        CancellationTokenSource oldCts;
        lock (_lock)
        {
            oldCts = _cts;
            _cts = newCts;
        }
        try { oldCts.Cancel(); } catch { }
        try { oldCts.Dispose(); } catch { }

        Task.Run(() =>
        {
            try { sequence(newCts.Token); }
            catch (OperationCanceledException) { }
            catch { }
        });
    }

    private static void Beep(int frequency, int duration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Console.Beep(frequency, duration);
    }

    private static void Pause(int ms, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Thread.Sleep(ms);
    }

    // ── Menu ──────────────────────────────────────────────

    public void PlayMenuSelect() => Play((ct) =>
        Beep(800, 50, ct));

    public void PlayCursorMove() => Play((ct) =>
        Beep(1200, 15, ct));

    // ── Lobby countdown ──────────────────────────────────

    public void PlayCountdownTick(int remaining) => Play((ct) =>
    {
        int freq = 300 + (5 - Math.Clamp(remaining, 1, 5)) * 100;
        Beep(freq, 100, ct);
    });

    public void PlayCountdownGo() => Play((ct) =>
    {
        Beep(800, 80, ct);
        Beep(1000, 120, ct);
    });

    // ── Board: clue & round ──────────────────────────────

    public void PlayClueGiven() => Play((ct) =>
    {
        Beep(600, 150, ct);
        Beep(800, 150, ct);
        Beep(1000, 200, ct);
    });

    public void PlayRoundStarted() => Play((ct) =>
        Beep(800, 200, ct));

    public void PlayTurnSkipped() => Play((ct) =>
    {
        Beep(200, 300, ct);
        Pause(100, ct);
        Beep(200, 300, ct);
    });

    // ── Board: voting ────────────────────────────────────

    public void PlayVoteCast() => Play((ct) =>
        Beep(500, 60, ct));

    // ── Board: word reveals ──────────────────────────────

    public void PlayRevealCorrect() => Play((ct) =>
    {
        Beep(500, 100, ct);
        Beep(700, 100, ct);
        Beep(900, 150, ct);
    });

    public void PlayRevealWrong() => Play((ct) =>
    {
        Beep(400, 200, ct);
        Beep(300, 300, ct);
    });

    public void PlayRevealAssassin() => Play((ct) =>
    {
        Beep(200, 400, ct);
        Beep(150, 500, ct);
    });

    // ── Board: timer ─────────────────────────────────────

    public void PlayTimerWarning() => Play((ct) =>
        Beep(1000, 100, ct));

    // ── Game result ──────────────────────────────────────

    public void PlayGameWon() => Play((ct) =>
    {
        Beep(400, 120, ct);
        Beep(500, 120, ct);
        Beep(600, 120, ct);
        Beep(700, 120, ct);
        Beep(800, 120, ct);
        Beep(1000, 200, ct);
    });

    public void PlayGameLost() => Play((ct) =>
    {
        Beep(500, 200, ct);
        Beep(400, 200, ct);
        Beep(300, 200, ct);
        Beep(200, 300, ct);
    });

    public void PlayGameDraw() => Play((ct) =>
        Beep(500, 300, ct));

    // ── Errors ───────────────────────────────────────────

    public void PlayError() => Play((ct) =>
        Beep(200, 200, ct));
}
