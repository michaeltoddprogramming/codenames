using Codenames.Cli.Auth;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Screens;

public class MainMenuScreen(
    AuthSession authSession,
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    ILogger<MainMenuScreen> logger) : IScreen
{
    private static readonly string[] MenuItems = ["Quit"];

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
                    ExecuteSelection();
                    return;
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

    private void ExecuteSelection()
    {
        switch (MenuItems[_selectedIndex])
        {
            case "Quit":
                logger.LogInformation("User {Email} quit", authSession.Email);
                break;
        }
    }
}
