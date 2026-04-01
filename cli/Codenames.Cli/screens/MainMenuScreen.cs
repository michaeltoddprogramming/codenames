using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Lobby;
using Codenames.Cli.Models;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Screens;

public class MainMenuScreen(
    AuthSession authSession,
    LobbySession lobbySession,
    LobbyApiClient lobbyApiClient,
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator,
    ILogger<MainMenuScreen> logger) : IScreen
{
    private string[] _menuItems = [];
    private LobbyStateResponse? _existingLobby;
    private int _selectedIndex;

    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        _existingLobby = await TryGetExistingLobbyAsync(cancellationToken);
        _menuItems = BuildMenuItems();
        _selectedIndex = 0;
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
                    _selectedIndex = Math.Min(_menuItems.Length - 1, _selectedIndex + 1);
                    Draw();
                    break;

                case ConsoleKey.Enter:
                    var shouldExit = await ExecuteSelectionAsync(cancellationToken);
                    if (shouldExit) return;
                    Draw();
                    break;
            }
        }
    }

    private string[] BuildMenuItems()
    {
        var items = new List<string>();
        if (_existingLobby is not null)
            items.Add($"Rejoin Lobby ({_existingLobby.Code})");
        items.Add("Create Lobby");
        items.Add("Join Lobby");
        items.Add("Logout");
        return [.. items];
    }

    private void Draw()
    {
        renderer.Clear();
        renderer.RenderHeader("Codenames");
        renderer.RenderBlankLine();
        renderer.RenderStatus($"Welcome, {authSession.Name ?? authSession.Email}!");
        renderer.RenderBlankLine();
        for (var i = 0; i < _menuItems.Length; i++)
            renderer.RenderMenuItem(_menuItems[i], isSelected: i == _selectedIndex);
    }

    private async Task<bool> ExecuteSelectionAsync(CancellationToken cancellationToken)
    {
        var selected = _menuItems[_selectedIndex];

        if (selected.StartsWith("Rejoin Lobby") && _existingLobby is not null)
        {
            if (authSession.UserId is { } userId)
                lobbySession.SetLobby(_existingLobby, userId);
            await navigator.GoToAsync(ScreenName.LobbyRoom, cancellationToken);
            return false;
        }

        switch (selected)
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

    private async Task<LobbyStateResponse?> TryGetExistingLobbyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await lobbyApiClient.GetMyLobbyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not check for existing lobby");
            return null;
        }
    }
}
