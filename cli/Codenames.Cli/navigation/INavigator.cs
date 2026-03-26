namespace Codenames.Cli.Navigation;

public interface INavigator
{
    Task GoToAsync(ScreenName screen, CancellationToken ct = default);
}
