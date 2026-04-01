using Codenames.Cli.Auth;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Codenames.Cli.Screens;

public class WelcomeScreen(
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator,
    IOptions<AuthConfig> authConfig,
    ILogger<WelcomeScreen> logger) : IScreen
{
    private readonly bool _devMode = authConfig.Value.DevMode;

    private string[] MenuItems => _devMode
        ? ["Login", "Dev Login", "Quit"]
        : ["Login", "Quit"];

    private int _selectedIndex;

    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        Draw();

        while (!cancellationToken.IsCancellationRequested)
        {
            var key = await keyboard.ReadKeyAsync(cancellationToken);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _selectedIndex = Math.Max(0, _selectedIndex - 1);
                    Draw();
                    break;

                case ConsoleKey.DownArrow:
                    _selectedIndex = Math.Min(MenuItems.Length - 1, _selectedIndex + 1);
                    Draw();
                    break;

                case ConsoleKey.Enter:
                    var shouldExit = await ExecuteSelectionAsync(cancellationToken);
                    if (shouldExit)
                    {
                        return;
                    }
                    Draw();
                    break;
            }
        }
    }

    private void Draw()
    {
        renderer.Clear();
        renderer.RenderHeader("Codenames");
        renderer.RenderBlankLine();
        for (var i = 0; i < MenuItems.Length; i++)
            renderer.RenderMenuItem(MenuItems[i], isSelected: i == _selectedIndex);
    }

    private async Task<bool> ExecuteSelectionAsync(CancellationToken cancellationToken)
    {
        switch (MenuItems[_selectedIndex])
        {
            case "Login":
                await navigator.GoToAsync(ScreenName.Login, cancellationToken);
                return false;
            case "Dev Login":
                await navigator.GoToAsync(ScreenName.DevLogin, cancellationToken);
                return false;
            case "Quit":
                logger.LogInformation("User quit");
                return true;
            default:
                return false;
        }
    }
}
