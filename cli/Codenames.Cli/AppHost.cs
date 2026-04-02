using Codenames.Cli.Api;
using Codenames.Cli.Auth;
using Codenames.Cli.Lobby;
using Codenames.Cli.Navigation;
using Codenames.Cli.Screens;
using Codenames.Cli.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace Codenames.Cli;

public static class AppHost
{
    public static IHost Build(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((ctx, config) => config.ReadFrom.Configuration(ctx.Configuration))
            .ConfigureServices((ctx, services) =>
            {
                // TUI
                services.AddSingleton<TerminalRenderer>();
                services.AddSingleton<KeyboardHandler>();

                // Navigation
                services.AddSingleton<ScreenFactory>();
                services.AddSingleton<INavigator, Navigator>();

                // Auth
                services.Configure<AuthConfig>(ctx.Configuration.GetSection(AuthConfig.Section));
                services.AddSingleton<AuthSession>();
                services.AddSingleton<AuthService>();
                services.AddSingleton<LobbySession>();

                // API
                services.AddHttpClient<ApiClient>((serviceProvider, client) =>
                {
                    var config = serviceProvider.GetRequiredService<IOptions<AuthConfig>>().Value;
                    client.BaseAddress = new Uri(config.ServerBaseUrl);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });
                services.AddTransient<AuthApiClient>();
                services.AddTransient<LobbyApiClient>();
                services.AddTransient<GameApiClient>();

                // SSE
                services.AddHttpClient<SseClient>((serviceProvider, client) =>
                {
                    var config = serviceProvider.GetRequiredService<IOptions<AuthConfig>>().Value;
                    client.BaseAddress = new Uri(config.ServerBaseUrl);
                });

                // Screens
                services.AddTransient<WelcomeScreen>();
                services.AddTransient<LoginScreen>();
                services.AddTransient<MainMenuScreen>();
                services.AddTransient<CreateLobbyScreen>();
                services.AddTransient<JoinLobbyScreen>();
                services.AddTransient<LobbyRoomScreen>();
                services.AddTransient<BoardScreen>();
                services.AddTransient<GameResultScreen>();
                services.AddTransient<DevLoginScreen>();
                services.AddTransient<HelpScreen>();

                // App runner
                services.AddSingleton<AppRunner>();
            })
            .Build();
}
