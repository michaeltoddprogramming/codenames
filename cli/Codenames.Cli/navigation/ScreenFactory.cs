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
        ScreenName.CreateLobby => _services.GetRequiredService<CreateLobbyScreen>(),
        ScreenName.JoinLobby => _services.GetRequiredService<JoinLobbyScreen>(),
        ScreenName.LobbyRoom => _services.GetRequiredService<LobbyRoomScreen>(),
        ScreenName.Board      => _services.GetRequiredService<BoardScreen>(),
        ScreenName.GameResult => _services.GetRequiredService<GameResultScreen>(),
        ScreenName.DevLogin   => _services.GetRequiredService<DevLoginScreen>(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown screen")
    };
}
