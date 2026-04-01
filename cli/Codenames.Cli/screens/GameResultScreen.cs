using Codenames.Cli.Lobby;
using Codenames.Cli.Models;
using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Spectre.Console;

namespace Codenames.Cli.Screens;

public class GameResultScreen(
    LobbySession lobbySession,
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator) : IScreen
{
    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        var result = lobbySession.CurrentGameResult;

        TerminalRenderer.StartFrame();
        renderer.RenderHeader("Game Over");
        renderer.RenderBlankLine();

        if (result is null)
        {
            renderer.RenderStatus("Game ended.");
        }
        else
        {
            RenderResult(result);
        }

        renderer.RenderBlankLine();
        renderer.RenderStatus("Press any key to return to main menu...");
        TerminalRenderer.EndFrame();
        await keyboard.ReadKeyAsync(cancellationToken);

        lobbySession.Clear();
        await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
    }

    private void RenderResult(GameEndResult result)
    {
        if (result.Winner is null)
        {
            AnsiConsole.Write(new Markup("[yellow bold]   ══════════  DRAW  ══════════[/]"));
        }
        else if (result.Winner.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.Write(new Markup("[red bold]   ══════════  RED WINS  ══════════[/]"));
        }
        else
        {
            AnsiConsole.Write(new Markup("[blue bold]   ══════════  BLUE WINS  ══════════[/]"));
        }

        renderer.RenderBlankLine();

        var reasonText = result.Reason switch
        {
            "ASSASSIN"     => "Assassin word revealed",
            "ALL_REVEALED" => "All words revealed",
            "TIMER_EXPIRED" => "Match timer expired",
            "DRAW"         => "Match timer expired — equal scores",
            _              => result.Reason
        };
        renderer.RenderStatus($"Reason: {reasonText}");
        renderer.RenderBlankLine();

        AnsiConsole.Write(new Markup($"[red]Red[/]  remaining: [bold]{result.RedRemaining}[/] / 10"));
        renderer.RenderBlankLine();
        AnsiConsole.Write(new Markup($"[blue]Blue[/] remaining: [bold]{result.BlueRemaining}[/] / 10"));
        renderer.RenderBlankLine();

        int redRevealed  = 10 - result.RedRemaining;
        int blueRevealed = 10 - result.BlueRemaining;
        AnsiConsole.Write(new Markup($"[red]Red[/]  revealed: [bold]{redRevealed}[/] / 10   [blue]Blue[/] revealed: [bold]{blueRevealed}[/] / 10"));
    }
}
