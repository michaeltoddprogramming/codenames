using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Screens;

public class DevLoginScreen(
    AuthApiClient authApiClient,
    AuthSession authSession,
    TerminalRenderer renderer,
    INavigator navigator,
    ILogger<DevLoginScreen> logger) : IScreen
{
    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        renderer.Clear();
        renderer.RenderHeader("Codenames");
        renderer.RenderBlankLine();
        renderer.RenderStatus("[DEV] Enter a username to log in:");
        renderer.RenderBlankLine();

        Console.Write("  Username: ");
        var username = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(username))
        {
            await navigator.GoToAsync(ScreenName.Welcome, cancellationToken);
            return;
        }

        try
        {
            renderer.RenderBlankLine();
            renderer.RenderStatus($"Logging in as '{username}'...");

            var response = await authApiClient.DevLoginAsync(username, cancellationToken);
            authSession.Set(response.Token, response.Email, response.Name);

            logger.LogInformation("[DEV] User {Username} authenticated", username);

            await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
        }
        catch (Exception ex) when (ex is not NavigateException)
        {
            logger.LogError(ex, "[DEV] Login failed for {Username}", username);
            renderer.RenderBlankLine();
            renderer.RenderError($"Login failed: {ex.Message}");
            renderer.RenderBlankLine();
            renderer.RenderStatus("Press any key to return...");
            Console.ReadKey(intercept: true);
            await navigator.GoToAsync(ScreenName.Welcome, cancellationToken);
        }
    }
}
