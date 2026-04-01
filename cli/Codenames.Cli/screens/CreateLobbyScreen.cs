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

            renderer.RenderBlankLine();
            renderer.RenderStatus("Lobby created successfully.");
            renderer.RenderStatus($"Join Code: {lobby.Code}");
            renderer.RenderStatus("Share the join code with other players.");
            renderer.RenderBlankLine();
            renderer.RenderStatus("Press any key to enter the lobby room...");
            await keyboard.ReadKeyAsync(cancellationToken);
            await navigator.GoToAsync(ScreenName.LobbyRoom, cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            renderer.RenderBlankLine();
            renderer.RenderError($"Failed to create lobby: {ex.Message}");
        }

        renderer.RenderBlankLine();
        renderer.RenderStatus("Press any key to return to the main menu...");
        await keyboard.ReadKeyAsync(cancellationToken);
    }
}
