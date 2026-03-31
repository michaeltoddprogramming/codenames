using Codenames.Cli.Auth;
using Codenames.Cli.Lobby;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Screens;

public class MainMenuScreen(
    AuthSession authSession,
    LobbySession lobbySession,
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator,
    ILogger<MainMenuScreen> logger) : IScreen
{
    private static readonly string[] MenuItems = ["Create Lobby", "Join Lobby", "Logout"];

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
        renderer.RenderStatus($"Welcome, {authSession.Name ?? authSession.Email}!");
        renderer.RenderBlankLine();
        for (var i = 0; i < MenuItems.Length; i++)
            renderer.RenderMenuItem(MenuItems[i], isSelected: i == _selectedIndex);
    }

    private async Task<bool> ExecuteSelectionAsync(CancellationToken cancellationToken)
    {
        switch (MenuItems[_selectedIndex])
        {
            case "Create Lobby":
                await navigator.GoToAsync(ScreenName.CreateLobby, cancellationToken);
                return false;
            case "Join Lobby":
                await navigator.GoToAsync(ScreenName.JoinLobby, cancellationToken);
                return false;
            case "Logout":
                logger.LogInformation("User {Email} logged out", authSession.Email);
                authSession.Clear();
                lobbySession.Clear();
                return true;
            default:
                return false;
        }
    }
}
