using Codenames.Cli.Screens;
using Microsoft.Extensions.DependencyInjection;

namespace Codenames.Cli.Navigation;

public class ScreenFactory(IServiceProvider services)
{
    private readonly IServiceProvider _services = services;

    public IScreen Create(ScreenName name) => name switch
    {
        ScreenName.Welcome  => _services.GetRequiredService<WelcomeScreen>(),
        ScreenName.Login    => _services.GetRequiredService<LoginScreen>(),
        ScreenName.MainMenu => _services.GetRequiredService<MainMenuScreen>(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown screen")
    };
}
