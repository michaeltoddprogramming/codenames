using Codenames.Cli.Navigation;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Codenames.Cli;

public class AppRunner(INavigator navigator, ILogger<AppRunner> logger)
{
    private readonly INavigator _navigator = navigator;
    private readonly ILogger<AppRunner> _logger = logger;

    public async Task RunAsync(CancellationToken ct = default)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

        _logger.LogInformation("Codenames CLI starting");

        try
        {
            await _navigator.GoToAsync(ScreenName.Welcome, ct);
        }
        catch (OperationCanceledException)
        {
            // exit via Ctrl+C
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }
}
