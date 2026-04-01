using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Lobby;
using Codenames.Cli.Models;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Codenames.Cli.Screens;

public class MainMenuScreen(
    AuthSession authSession,
    LobbySession lobbySession,
    GameApiClient gameApiClient,
    LobbyApiClient lobbyApiClient,
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator,
    ILogger<MainMenuScreen> logger) : IScreen
{
    private static readonly string[] BaseMenuItems = ["Create Lobby", "Join Lobby", "How to Play", "Logout"];
    private const string RejoinGameItem  = "Rejoin Game";
    private const string RejoinLobbyItem = "Rejoin Lobby";
    private string[] _menuItems = BaseMenuItems;
    private int? _activeGameId;
    private LobbyStateResponse? _existingLobby;

    private int _selectedIndex;

    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        await CheckForActiveGameAsync(cancellationToken);
        if (cancellationToken.IsCancellationRequested) return;

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
        if (_activeGameId.HasValue)
            items.Add(RejoinGameItem);
        if (_existingLobby is not null)
            items.Add($"{RejoinLobbyItem} ({_existingLobby.Code})");
        items.Add("Create Lobby");
        items.Add("Join Lobby");
        items.Add("How to Play");
        items.Add("Logout");
        return [.. items];
    }

    private void Draw()
    {
        TerminalRenderer.StartFrame();
        renderer.RenderHeader("Codenames");
        renderer.RenderBlankLine();
        AnsiConsole.MarkupLine($"  [grey]Welcome,[/] [bold white]{Markup.Escape(authSession.Name ?? authSession.Email ?? "Player")}[/]");
        renderer.RenderBlankLine();
        AnsiConsole.Write(new Rule("[grey]Main Menu[/]").RuleStyle("grey"));
        renderer.RenderBlankLine();
        for (var i = 0; i < _menuItems.Length; i++)
            renderer.RenderMenuItem(_menuItems[i], isSelected: i == _selectedIndex);
        renderer.RenderBlankLine();
        AnsiConsole.MarkupLine("[dim]  Use arrow keys to navigate, Enter to select[/]");
        TerminalRenderer.EndFrame();
    }

    private async Task CheckForActiveGameAsync(CancellationToken cancellationToken)
    {
        try
        {
            _activeGameId = await gameApiClient.GetActiveGameAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not check for active game");
            _activeGameId = null;
        }
    }

    private async Task<bool> ExecuteSelectionAsync(CancellationToken cancellationToken)
    {
        switch (_menuItems[_selectedIndex])
        {
            case RejoinGameItem:
                if (_activeGameId.HasValue)
                {
                    lobbySession.SetGameId(_activeGameId.Value);
                    await navigator.GoToAsync(ScreenName.Board, cancellationToken);
                }
                return false;

            case string s when s.StartsWith(RejoinLobbyItem):
                if (_existingLobby is not null && authSession.UserId.HasValue)
                {
                    lobbySession.SetLobby(_existingLobby, authSession.UserId.Value);
                    await navigator.GoToAsync(ScreenName.LobbyRoom, cancellationToken);
                }
                return false;

            case "Create Lobby":
                await navigator.GoToAsync(ScreenName.CreateLobby, cancellationToken);
                return false;
            case "Join Lobby":
                await navigator.GoToAsync(ScreenName.JoinLobby, cancellationToken);
                return false;
            case "How to Play":
                await navigator.GoToAsync(ScreenName.Help, cancellationToken);
                return false;
            case "Logout":
                logger.LogInformation("User {Email} logged out", authSession.Email);
                authSession.Clear();
                lobbySession.Clear();
                await navigator.GoToAsync(ScreenName.Welcome, cancellationToken);
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
