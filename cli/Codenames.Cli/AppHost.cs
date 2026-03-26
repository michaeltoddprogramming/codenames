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
            .ConfigureServices((_, services) =>
            {
                // TUI
                services.AddSingleton<TerminalRenderer>();
                services.AddSingleton<KeyboardHandler>();

                // Navigation
                services.AddSingleton<ScreenFactory>();
                services.AddSingleton<INavigator, Navigator>();

                // Screens
                services.AddTransient<WelcomeScreen>();

                // App runner
                services.AddSingleton<AppRunner>();
            })
            .Build();
}
