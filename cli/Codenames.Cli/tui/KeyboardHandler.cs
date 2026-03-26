namespace Codenames.Cli.Tui;

public class KeyboardHandler
{
    public async Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
                return Console.ReadKey(intercept: true);

            await Task.Delay(50, ct);
        }

        ct.ThrowIfCancellationRequested();
        return default;
    }
}
