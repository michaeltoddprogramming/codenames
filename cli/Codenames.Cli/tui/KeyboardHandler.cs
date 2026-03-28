namespace Codenames.Cli.Tui;

public class KeyboardHandler
{
    public async Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
                return Console.ReadKey(intercept: true);

            await Task.Delay(50, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return default;
    }
}
