using Codenames.Cli.Navigation;
using Codenames.Cli.Tui;
using Spectre.Console;

namespace Codenames.Cli.Screens;

public class HelpScreen(
    TerminalRenderer renderer,
    KeyboardHandler keyboard,
    INavigator navigator) : IScreen
{
    private int _page;
    private const int TotalPages = 6;

    public async Task RenderAsync(CancellationToken cancellationToken = default)
    {
        _page = 0;
        Draw();

        while (!cancellationToken.IsCancellationRequested)
        {
            var key = await keyboard.ReadKeyAsync(cancellationToken);

            switch (key.Key)
            {
                case ConsoleKey.RightArrow:
                case ConsoleKey.D:
                    if (_page < TotalPages - 1) { _page++; Draw(); }
                    break;

                case ConsoleKey.LeftArrow:
                case ConsoleKey.A:
                    if (_page > 0) { _page--; Draw(); }
                    break;

                case ConsoleKey.Escape:
                case ConsoleKey.Q:
                    await navigator.GoToAsync(ScreenName.MainMenu, cancellationToken);
                    return;
            }
        }
    }

    private void Draw()
    {
        TerminalRenderer.StartFrame();
        RenderTopBar();
        AnsiConsole.WriteLine();

        switch (_page)
        {
            case 0: DrawPage0_HowToPlay(); break;
            case 1: DrawPage1_Overview();  break;
            case 2: DrawPage2_Roles();     break;
            case 3: DrawPage3_Round();     break;
            case 4: DrawPage4_WinLose();   break;
            case 5: DrawPage5_Controls();  break;
        }

        AnsiConsole.WriteLine();
        RenderNavBar();
        TerminalRenderer.EndFrame();
    }

    private void RenderTopBar()
    {
        renderer.RenderHeader("Codenames");

        var pageNames = new[] { "How to Play", "The Board", "Teams & Roles", "A Round", "Win & Lose", "Controls" };
        var parts = new List<string>();
        for (var i = 0; i < TotalPages; i++)
        {
            parts.Add(i == _page
                ? $"[bold white on blue] {pageNames[i]} [/]"
                : $"[grey] {pageNames[i]} [/]");
        }
        AnsiConsole.MarkupLine("  " + string.Join(" [grey]│[/] ", parts));
    }

    private void RenderNavBar()
    {
        var left  = _page > 0             ? "[blue]◄ ← prev[/]" : "         ";
        var right = _page < TotalPages - 1 ? "[blue]next → ►[/]" : "         ";
        AnsiConsole.MarkupLine($"  {left}   [grey]Esc = back to menu[/]   {right}");
    }

    private static void DrawPage0_HowToPlay()
    {
        AnsiConsole.Write(new Rule("[bold yellow]How to Play[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        var steps = new (string icon, string title, string detail)[]
        {
            ("1", "Sign in",
             "Log in using your [bold]Google account[/] from the welcome screen."),

            ("2", "Create or Join a game",
             "Choose [bold]Create Lobby[/] and share your code,\n" +
             "  or choose [bold]Join Lobby[/] and enter a friend's 6-character code."),

            ("3", "Get assigned automatically",
             "The server randomly assigns you to [red bold]Red[/] or [blue bold]Blue[/] team\n" +
             "  and gives you a role — [yellow bold]Spymaster[/] or [aqua bold]Operative[/]."),

            ("4", "A 5x5 board appears",
             "25 words are shown to everyone.\n" +
             "  Spymasters see the secret colour of every word. Operatives see only grey."),

            ("5", "If you are a Spymaster",
             "Study the board and press [bold]C[/] to submit a [bold]one-word clue[/] and a number.\n" +
             "  The number tells your operatives how many words relate to your clue.\n" +
             "  [grey]Example: \"COUNTRY 3\" means three of your team's words link to that clue.[/]"),

            ("6", "If you are an Operative",
             "Use your Spymaster's clue to pick matching words from the board.\n" +
             "  Navigate with [bold]arrow keys[/] and press [bold]Enter[/] to vote.\n" +
             "  You have [bold]30 seconds[/] and up to the number of votes given by your Spymaster."),

            ("7", "Words are revealed",
             "After the 30-second window the most-voted words are flipped.\n" +
             "  Your Spymaster then submits the next clue and the cycle repeats."),

            ("8", "Avoid the Assassin",
             "[white on red] ☠ [/] Selecting the Assassin word ends the game [red bold]immediately[/] — your team loses.\n" +
             "  [grey]Both teams play at the same time, so stay focused on your own clues.[/]"),

            ("9", "Win the race",
             "Reveal all [bold]8[/] of your team's words first, [bold]or[/] have more words\n" +
             "  revealed than the opposing team when the match timer runs out."),
        };

        foreach (var (icon, title, detail) in steps)
        {
            AnsiConsole.MarkupLine($"  [bold blue]{icon}.[/]  [bold]{title}[/]");
            AnsiConsole.MarkupLine($"       {detail}");
            AnsiConsole.WriteLine();
        }
    }

    private static void DrawPage1_Overview()
    {
        AnsiConsole.Write(new Rule("[bold yellow]The Board[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  Two teams — [red bold]RED[/] and [blue bold]BLUE[/] — race to reveal all their secret words");
        AnsiConsole.MarkupLine("  on a shared 5×5 board.  Both teams play [bold]simultaneously[/] against a match timer.");
        AnsiConsole.WriteLine();

        var grid = new Table().HideHeaders().Border(TableBorder.None);
        for (var c = 0; c < 5; c++) grid.AddColumn(new TableColumn("").Width(14));

        grid.AddRow(CardMarkup("OCEAN",   "red"),  CardMarkup("CASTLE",  "blue"), CardMarkup("MIRROR",  "grey"),
                    CardMarkup("THUNDER", "red"),  CardMarkup("FROST",   "blue"));
        grid.AddRow(CardMarkup("SHADOW",  "grey"), CardMarkup("BRIDGE",  "red"),  CardMarkup("LANTERN", "grey"),
                    CardMarkup("STONE",   "blue"), CardMarkup("ORBIT",   "red"));
        grid.AddRow(CardMarkup("RIVER",   "blue"), CardMarkup("SPINE",   "grey"), CardMarkup("HAMMER",  "red"),
                    CardMarkup("PILOT",   "grey"), CardMarkup("TORCH",   "blue"));
        grid.AddRow(CardMarkup("CROWN",   "grey"), CardMarkup("FENCE",   "blue"), CardMarkup("DUST",    "grey"),
                    CardMarkup("NEEDLE",  "red"),  CardMarkup("WAVE",    "blue"));
        grid.AddRow(CardMarkup("COMET",   "grey"), CardMarkup("TEMPLE",  "red"),  CardMarkup("CABLE",   "grey"),
                    CardMarkup("SHIELD",  "blue"), CardMarkup("GHOST",   "white"));

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();

        var legend = new Table().HideHeaders().Border(TableBorder.None);
        legend.AddColumn("").AddColumn("");
        legend.AddRow(new Markup("[red][/] [bold]RED[/]  — 8 words  (your team's target)"),
                      new Markup("[blue][/] [bold]BLUE[/] — 8 words  (opponent's target)"));
        legend.AddRow(new Markup("[grey][/] [bold]NEUTRAL[/] — 8 words (no benefit to either team)"),
                      new Markup("[white on red] ☠ [/] [bold]ASSASSIN[/] — 1 word  ([red bold]instant loss![/])"));
        AnsiConsole.Write(legend);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  [grey]Operatives see only grey cards until a word is revealed.[/]");
        AnsiConsole.MarkupLine("  [grey]Spymasters see all colours from the start.[/]");
    }

    private static void DrawPage2_Roles()
    {
        AnsiConsole.Write(new Rule("[bold yellow]Teams & Roles[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Role[/]").Centered().Width(14))
            .AddColumn(new TableColumn("[bold]Count[/]").Centered().Width(10))
            .AddColumn(new TableColumn("[bold]What they see[/]").Width(26))
            .AddColumn(new TableColumn("[bold]What they do[/]").Width(34));

        table.AddRow(
            "[yellow bold]Spymaster[/]",
            "1 per team",
            "[bold]ALL[/] 25 word colours",
            "Submit a [bold]one-word clue[/] + a number\n[grey]e.g. \"COUNTRY 3\"[/]"
        );
        table.AddRow(
            "[aqua bold]Operative[/]",
            "1–4 per team",
            "Only [bold]revealed[/] colours",
            "Vote on words that match the clue\n[grey]Up to the number given by Spymaster[/]"
        );

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Rule("[dim]Spymaster view — sees all colours[/]").RuleStyle("grey").LeftJustified());
        var spyRow = new Table().HideHeaders().Border(TableBorder.None);
        for (var c = 0; c < 5; c++) spyRow.AddColumn(new TableColumn("").Width(14));
        spyRow.AddRow(
            CardMarkup("OCEAN",   "red"),
            CardMarkup("CASTLE",  "blue"),
            CardMarkup("MIRROR",  "grey"),
            CardMarkup("THUNDER", "red"),
            CardMarkup("FROST",   "blue")
        );
        AnsiConsole.Write(spyRow);
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Rule("[dim]Operative view — all grey until revealed[/]").RuleStyle("grey").LeftJustified());
        var opRow = new Table().HideHeaders().Border(TableBorder.None);
        for (var c = 0; c < 5; c++) opRow.AddColumn(new TableColumn("").Width(14));
        opRow.AddRow(
            CardMarkup("OCEAN",   "grey"),
            CardMarkup("CASTLE",  "grey"),
            CardMarkup("MIRROR",  "grey"),
            CardMarkup("THUNDER", "grey"),
            CardMarkup("FROST",   "grey")
        );
        AnsiConsole.Write(opRow);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  [grey]Teams and roles are assigned randomly by the server when the host starts the game.[/]");
    }

    private static void DrawPage3_Round()
    {
        AnsiConsole.Write(new Rule("[bold yellow]How a Round Works[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  Both teams run their own clue → vote cycle [bold]at the same time[/], independently.");
        AnsiConsole.WriteLine();

        var steps = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Step[/]").Centered().Width(6))
            .AddColumn(new TableColumn("[bold]Who[/]").Width(14))
            .AddColumn(new TableColumn("[bold]Action[/]").Width(44))
            .AddColumn(new TableColumn("[bold]Timer[/]").Width(8));

        steps.AddRow("1", "[yellow]Spymaster[/]",
            "Submits a [bold]one-word clue[/] and a [bold]number[/]\n" +
            "[grey]The number = how many board words relate to the clue[/]",
            "[yellow]60 s[/]");
        steps.AddRow("2", "[aqua]Operatives[/]",
            "Vote on the words they believe match the clue\n" +
            "[grey]Each operative casts up to N votes, where N = the clue number[/]",
            "[yellow]30 s[/]");
        steps.AddRow("3", "Server",
            "The top N most-voted words are revealed\n" +
            "[grey]N = clue number. Equal votes broken by word order.[/]",
            "—");
        steps.AddRow("4", "Both",
            "Cycle repeats — Spymaster submits the next clue",
            "—");

        AnsiConsole.Write(steps);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  [grey]If a Spymaster does not submit a clue in time, the round is skipped and the timer resets.[/]");
    }

    private static void DrawPage4_WinLose()
    {
        AnsiConsole.Write(new Rule("[bold yellow]Win & Lose Conditions[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Condition[/]").Width(38))
            .AddColumn(new TableColumn("[bold]Result[/]").Width(28))
            .AddColumn(new TableColumn("[bold]When[/]").Width(12));

        table.AddRow(
            "A team reveals all [bold]8[/] of their assigned words",
            "[green bold]That team wins![/]",
            "Instant");
        table.AddRow(
            "A team selects the [white on red] ☠ ASSASSIN [/] word",
            "[red bold]That team loses immediately[/]",
            "Instant");
        table.AddRow(
            "Match timer expires — one team has revealed more",
            "[bold]Team with more revealed wins[/]",
            "On expiry");
        table.AddRow(
            "Match timer expires — both teams equal",
            "[yellow bold]Draw[/]",
            "On expiry");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Rule("[dim]What happens when a word is revealed[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        var outcomes = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]The revealed word belongs to…[/]").Width(30))
            .AddColumn(new TableColumn("[bold]Outcome[/]").Width(42));

        outcomes.AddRow("[bold]Your team[/]",
            "[green]✓ Marked as correctly revealed — progress toward victory[/]");
        outcomes.AddRow("[bold]The opposing team[/]",
            "[red]✗ Revealed and the opposing team benefits[/]");
        outcomes.AddRow("[bold]Neither team (Neutral)[/]",
            "[grey]→ Revealed with no benefit to either team[/]");
        outcomes.AddRow("[white on red] ☠ Assassin [/]",
            "[red bold]Your team loses the game instantly[/]");

        AnsiConsole.Write(outcomes);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  [grey]Board totals: 8 red · 8 blue · 8 neutral · 1 assassin = 25 words[/]");
    }

    private static void DrawPage5_Controls()
    {
        AnsiConsole.Write(new Rule("[bold yellow]Keyboard Reference[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  [bold]Game Board — Operative[/]");
        AnsiConsole.WriteLine();

        var opTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey37)
            .AddColumn(new TableColumn("[bold]Key[/]").Centered().Width(12))
            .AddColumn(new TableColumn("[bold]Action[/]").Width(42));

        opTable.AddRow("[bold]↑ ↓ ← →[/]", "Move cursor across the 5x5 board");
        opTable.AddRow("[bold]Enter[/]",    "Vote for the word under the cursor");
        opTable.AddRow("[bold]Esc[/]",      "Exit game and return to main menu");

        AnsiConsole.Write(opTable);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  [bold]Game Board — Spymaster[/]");
        AnsiConsole.WriteLine();

        var spyTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey37)
            .AddColumn(new TableColumn("[bold]Key[/]").Centered().Width(12))
            .AddColumn(new TableColumn("[bold]Action[/]").Width(42));

        spyTable.AddRow("[bold]C[/]",   "Open clue input — type  WORD NUMBER  then Enter");
        spyTable.AddRow("[bold]Esc[/]", "Exit game and return to main menu");

        AnsiConsole.Write(spyTable);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("  [bold]Lobby & Menus[/]");
        AnsiConsole.WriteLine();

        var menuTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey37)
            .AddColumn(new TableColumn("[bold]Key[/]").Centered().Width(12))
            .AddColumn(new TableColumn("[bold]Action[/]").Width(42));

        menuTable.AddRow("[bold]↑ ↓[/]",  "Navigate menu items");
        menuTable.AddRow("[bold]Enter[/]", "Select highlighted item");
        menuTable.AddRow("[bold]Esc[/]",   "Leave lobby / cancel");
        menuTable.AddRow("[bold]Enter[/]", "[grey](host only)[/] Start game when 4 or more players are in the lobby");

        AnsiConsole.Write(menuTable);
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Rule("[dim]Clue format[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  Type exactly:  [bold yellow]WORD NUMBER[/]   e.g.  [bold]COUNTRY 3[/]  or  [bold]SHADOW 1[/]");
        AnsiConsole.MarkupLine("  [grey]· Single word only — no spaces in the clue[/]");
        AnsiConsole.MarkupLine("  [grey]· Number must be between 1 and 9[/]");
    }

    private static Markup CardMarkup(string word, string color)
    {
        const int innerWidth = 10;
        var escapedWord = word.Replace("[", "[[").Replace("]", "]]");
        var pad = Math.Max(0, innerWidth - escapedWord.Length);
        var lp = pad / 2;
        var rp = pad - lp;
        var top = $"┌{new string('─', innerWidth)}┐";
        var mid = $"│{new string(' ', lp)}{escapedWord}{new string(' ', rp)}│";
        var bot = $"└{new string('─', innerWidth)}┘";
        return new Markup($"[{color}]{top}[/]\n[{color}]{mid}[/]\n[{color}]{bot}[/]");
    }

}
