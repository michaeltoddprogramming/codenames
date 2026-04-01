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
    private static readonly Random Rng = new();

    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        var result = lobbySession.CurrentGameResult;

        // Confetti animation loop for 2 seconds (8 redraws at 250ms)
        if (result?.Winner is not null)
        {
            for (int frame = 0; frame < 8 && !cancellationToken.IsCancellationRequested; frame++)
            {
                TerminalRenderer.StartFrame();
                RenderGameOverContent(result, showConfetti: true);
                TerminalRenderer.EndFrame();
                await Task.Delay(250, cancellationToken);
            }
        }

        // Final static render
        TerminalRenderer.StartFrame();
        RenderGameOverContent(result, showConfetti: false);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]Press any key to return to main menu...[/]");
        TerminalRenderer.EndFrame();

        await keyboard.ReadKeyAsync(cancellationToken);

        lobbySession.Clear();
        await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
    }

    private void RenderGameOverContent(GameEndResult? result, bool showConfetti)
    {
        renderer.RenderHeader("Game Over");
        AnsiConsole.WriteLine();

        if (result is null)
        {
            renderer.RenderStatus("Game ended.");
            return;
        }

        // Confetti lines above winner text
        if (showConfetti)
        {
            AnsiConsole.MarkupLine(AnimationHelper.ConfettiLine(60, Rng));
            AnsiConsole.MarkupLine(AnimationHelper.ConfettiLine(60, Rng));
        }

        // Winner announcement as FigletText
        if (result.Winner is null)
        {
            AnsiConsole.Write(new FigletText("DRAW").Color(Color.Yellow).Centered());
        }
        else if (result.Winner.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.Write(new FigletText("RED WINS").Color(Color.Red).Centered());
        }
        else
        {
            AnsiConsole.Write(new FigletText("BLUE WINS").Color(Color.DodgerBlue1).Centered());
        }

        // Confetti lines below winner text
        if (showConfetti)
        {
            AnsiConsole.MarkupLine(AnimationHelper.ConfettiLine(60, Rng));
            AnsiConsole.MarkupLine(AnimationHelper.ConfettiLine(60, Rng));
        }

        AnsiConsole.WriteLine();

        // Reason
        var reasonText = result.Reason switch
        {
            "ASSASSIN"      => "Assassin word revealed",
            "ALL_REVEALED"  => "All words revealed",
            "TIMER_EXPIRED" => "Match timer expired",
            "DRAW"          => "Match timer expired — equal scores",
            _               => result.Reason
        };

        var reasonPanel = new Panel($"[bold]{Markup.Escape(reasonText)}[/]")
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 0),
            Header = new PanelHeader("[grey]Reason[/]"),
        };
        reasonPanel.BorderStyle = new Style(foreground: Color.Grey);
        AnsiConsole.Write(Align.Center(reasonPanel));
        AnsiConsole.WriteLine();

        // Score table
        int redRevealed  = 10 - result.RedRemaining;
        int blueRevealed = 10 - result.BlueRemaining;

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.BorderStyle = new Style(foreground: Color.Grey50);
        table.AddColumn(new TableColumn("[bold]Team[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Revealed[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Remaining[/]").Centered());

        table.AddRow(
            new Markup("[red bold]RED[/]"),
            new Markup($"[red]{redRevealed}[/] / 10"),
            new Markup($"[red]{result.RedRemaining}[/]"));

        table.AddRow(
            new Markup("[dodgerblue1 bold]BLUE[/]"),
            new Markup($"[dodgerblue1]{blueRevealed}[/] / 10"),
            new Markup($"[dodgerblue1]{result.BlueRemaining}[/]"));

        AnsiConsole.Write(Align.Center(table));
    }
}
