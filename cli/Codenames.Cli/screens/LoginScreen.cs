using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Screens;

public class LoginScreen(
    AuthService authService,
    AuthApiClient authApiClient,
    AuthSession authSession,
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator,
    ILogger<LoginScreen> logger) : IScreen
{
    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        renderer.Clear();
        renderer.RenderHeader("Codenames");
        renderer.RenderBlankLine();
        renderer.RenderStatus("Opening browser for Google sign-in...");

        try
        {
            var idToken = await authService.GetGoogleIdTokenAsync(cancellationToken);

            renderer.RenderStatus("Verifying with server...");

            var response = await authApiClient.LoginAsync(idToken, cancellationToken);
            authSession.Set(response.Token, response.Email, response.Name);

            logger.LogInformation("User {Email} authenticated", response.Email);

            await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login failed");
            renderer.RenderBlankLine();
            renderer.RenderError($"Login failed: {ex.Message}");
            renderer.RenderBlankLine();
            renderer.RenderStatus("Press any key to return...");
            await keyboard.ReadKeyAsync(cancellationToken);
            await navigator.GoToAsync(ScreenName.Welcome, cancellationToken);
        }
    }
}
