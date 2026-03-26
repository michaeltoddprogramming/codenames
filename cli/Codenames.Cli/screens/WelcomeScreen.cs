using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Screens;

public class WelcomeScreen(
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    ILogger<WelcomeScreen> logger) : IScreen
{
    private static readonly string[] MenuItems = ["Login", "Quit"];

    private readonly TerminalRenderer _renderer = renderer;
    private readonly KeyboardHandler _keyboard = keyboard;
    private readonly ILogger<WelcomeScreen> _logger = logger;

    private int _selectedIndex;

    public async Task RenderAsync(CancellationToken ct = default)
    {
        Draw();

        while (!ct.IsCancellationRequested)
        {
            var key = await _keyboard.ReadKeyAsync(ct);

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
                    await ExecuteSelectionAsync(ct);
                    return;
            }
        }
    }

    private void Draw()
    {
        _renderer.Clear();
        _renderer.RenderHeader("Codenames");
        _renderer.RenderBlankLine();

        for (var i = 0; i < MenuItems.Length; i++)
            _renderer.RenderMenuItem(MenuItems[i], isSelected: i == _selectedIndex);
    }

    private Task ExecuteSelectionAsync(CancellationToken ct)
    {
        switch (MenuItems[_selectedIndex])
        {
            case "Quit":
                _logger.LogInformation("User quit");
                break;
        }

        return Task.CompletedTask;
    }
}
