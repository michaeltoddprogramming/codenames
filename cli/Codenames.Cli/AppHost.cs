using Codenames.Cli.Auth;
using Codenames.Cli.Navigation;
using Codenames.Cli.Screens;
using Codenames.Cli.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                services.AddHttpClient<AuthService>();
                services.AddHttpClient<ServerClient>((sp, client) =>
                {
                    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthConfig>>().Value;
                    client.BaseAddress = new Uri(config.ServerBaseUrl);
                });

                // Screens
                services.AddTransient<WelcomeScreen>();
                services.AddTransient<LoginScreen>();
                services.AddTransient<MainMenuScreen>();

                // App runner
                services.AddSingleton<AppRunner>();
            })
            .Build();
}
