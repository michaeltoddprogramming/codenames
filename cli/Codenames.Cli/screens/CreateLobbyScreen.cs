using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Lobby;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;

namespace Codenames.Cli.Screens;

public class CreateLobbyScreen(
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
        renderer.RenderHeader("Create Lobby");
        renderer.RenderBlankLine();

        renderer.RenderBlankLine();
        renderer.RenderStatus("Creating lobby...");

        try
        {
            var lobby = await lobbyApiClient.CreateAsync(cancellationToken);

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
            renderer.RenderError($"Failed to create lobby: {ex.Message}");
        }

        renderer.RenderBlankLine();
        renderer.RenderStatus("Press any key to return to the main menu...");
        await keyboard.ReadKeyAsync(cancellationToken);
    }
}
