namespace Codenames.Cli.Navigation;

public interface INavigator
{
    Task GoToAsync(ScreenName screen, CancellationToken cancellationToken = default);
    Task RunAsync(ScreenName initial, CancellationToken cancellationToken = default);
}
