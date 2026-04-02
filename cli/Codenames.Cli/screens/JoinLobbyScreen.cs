using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Lobby;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Spectre.Console;

namespace Codenames.Cli.Screens;

public class JoinLobbyScreen(
    AuthSession authSession,
    LobbyApiClient lobbyApiClient,
    LobbySession lobbySession,
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator) : IScreen
{
    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        renderer.Clear();
        renderer.RenderHeader("Join Lobby");
        renderer.RenderBlankLine();

        var code = AnsiConsole.Ask<string>("Enter [green]join code[/]:").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            renderer.RenderError("Join code cannot be empty.");
            renderer.RenderStatus("Press any key to return to main menu...");
            await keyboard.ReadKeyAsync(cancellationToken);
            return;
        }

        renderer.RenderBlankLine();
        renderer.RenderStatus("Joining lobby...");

        try
        {
            var lobby = await lobbyApiClient.JoinAsync(code, cancellationToken);
            var userId = authSession.UserId;
            if (userId is null)
            {
                throw new InvalidOperationException("Unable to determine current user from session.");
            }

            lobbySession.SetLobby(lobby, userId.Value);
            await navigator.GoToAsync(ScreenName.LobbyRoom, cancellationToken);
            return;
        }
        catch (Exception ex) when (ex is not NavigateException)
        {
            renderer.RenderBlankLine();
            renderer.RenderError($"Failed to join lobby: {ex.Message}");
        }

        renderer.RenderBlankLine();
        renderer.RenderStatus("Press any key to return to main menu...");
        await keyboard.ReadKeyAsync(cancellationToken);
    }
}
