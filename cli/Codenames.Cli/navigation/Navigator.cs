using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Navigation;

public class Navigator(ScreenFactory factory, ILogger<Navigator> logger) : INavigator
{
    public async Task GoToAsync(ScreenName screen, CancellationToken ct = default)
    {
        logger.LogDebug("Navigating to {Screen}", screen);
        await factory.Create(screen).RenderAsync(ct);
    }
}
