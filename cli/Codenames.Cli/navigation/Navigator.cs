using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Navigation;

public class NavigateException(ScreenName target) : Exception
{
    public ScreenName Target { get; } = target;
}

public class Navigator(ScreenFactory factory, ILogger<Navigator> logger) : INavigator
{
    public Task GoToAsync(ScreenName screen, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Navigating to {Screen}", screen);
        throw new NavigateException(screen);
    }

    public async Task RunAsync(ScreenName initial, CancellationToken cancellationToken = default)
    {
        var current = initial;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger.LogDebug("Rendering screen {Screen}", current);
                await factory.Create(current).RenderAsync(cancellationToken);
                return;
            }
            catch (NavigateException nav)
            {
                current = nav.Target;
            }
        }
    }
}
