using Spectre.Console;
using Codenames.Cli.Screens;

namespace Codenames.Cli.Tui;

public class TerminalRenderer
{
    public void Clear() => AnsiConsole.Clear();

    public static void StartFrame()
    {
        Console.Write("\x1b[?25l");    // hide cursor
        Console.Write("\x1b[?2026h");  // begin synchronized output
        AnsiConsole.Clear();           // Spectre-aware clear — keeps internal cursor state correct
    }

    public static void EndFrame()
    {
        Console.Write("\x1b[?2026l");  // end synchronized output — atomic paint
        Console.Write("\x1b[?25h");    // show cursor
    }

    public void RenderHeader(string title) =>
        AnsiConsole.Write(new FigletText(title).Color(Color.Gold1));

    public void RenderBlankLine() => AnsiConsole.WriteLine();

    public void RenderMenuItem(string label, bool isSelected)
    {
        var prefix = isSelected ? "  > " : "    ";
        var style = isSelected
            ? new Style(foreground: Color.Black, background: Color.Gold1)
            : new Style(foreground: Color.Grey70);

        var text = $"{prefix}{label}";
        if (isSelected)
        {
            // Pad to 40 chars for full-width highlight bar
            text = text.PadRight(40);
        }
        AnsiConsole.Write(new Text($"{text}\n", style));
    }

    public void RenderBoard(WordCard[,] grid, int cursorRow, int cursorCol, bool showColors, IReadOnlySet<string>? myVotedWords = null)
    {
        var table = new Table().HideHeaders().Border(TableBorder.None);

        for (int col = 0; col < 5; col++)
            table.AddColumn(new TableColumn("").Width(20).Centered());

        for (int row = 0; row < 5; row++)
        {
            var cells = new List<Markup>();
            for (int col = 0; col < 5; col++)
            {
                var card = grid[row, col];
                bool isSelected = row == cursorRow && col == cursorCol;
                bool isVotedByMe = myVotedWords?.Contains(card.Word) ?? false;
                cells.Add(RenderCardAsBox(card, isSelected, showColors, isVotedByMe));
            }
            table.AddRow(cells.ToArray());
        }

        AnsiConsole.Write(table);
    }

    private static readonly int BoxWidth = 16;

    private static Markup RenderCardAsBox(WordCard card, bool isSelected, bool showColors, bool isVotedByMe = false)
    {
        var escapedWord = card.Word.Replace("[", "[[").Replace("]", "]]");
        var prefix = card.Revealed ? "~ " : "";
        var label = prefix + escapedWord;

        var borderColor = card.GetBorderColor(showColors);
        var bgColor = card.GetBackgroundColor(showColors);
        var textColor = card.GetDisplayColor(showColors);

        string colorTag;
        if (isSelected)
            colorTag = "bold white";
        else
            colorTag = GetColorName(borderColor);

        // Box drawing characters
        string tl, tr, bl, br, h, v;
        if (isSelected)
        {
            (tl, tr, bl, br, h, v) = ("╔", "╗", "╚", "╝", "═", "║");
        }
        else if (isVotedByMe && !card.Revealed)
        {
            (tl, tr, bl, br, h, v) = ("┏", "┓", "┗", "┛", "╌", "┃");
        }
        else if (!card.Revealed && !showColors)
        {
            // Unrevealed cards for operatives: dotted border
            (tl, tr, bl, br, h, v) = ("┌", "┐", "└", "┘", "┄", "¦");
        }
        else
        {
            (tl, tr, bl, br, h, v) = ("┌", "┐", "└", "┘", "─", "│");
        }

        var innerWidth = BoxWidth - 2;

        // Center the label
        var padding = Math.Max(0, innerWidth - label.Length);
        var leftPad = padding / 2;
        var rightPad = padding - leftPad;

        var hBar = string.Concat(Enumerable.Repeat(h, innerWidth));
        var emptyInner = new string(' ', innerWidth);

        // Build the text color tag for the word
        string textTag;
        if (bgColor.HasValue && (showColors || card.Revealed))
            textTag = $"{GetColorName(textColor)} on {GetColorName(bgColor.Value)}";
        else if (isSelected)
            textTag = "bold white";
        else
            textTag = GetColorName(textColor);

        // Build inner fill tag (for background on revealed/spymaster cards)
        string fillTag;
        if (bgColor.HasValue && (showColors || card.Revealed))
            fillTag = $"{GetColorName(borderColor)} on {GetColorName(bgColor.Value)}";
        else
            fillTag = colorTag;

        var topLine    = $"[{colorTag}]{tl}{hBar}{tr}[/]";
        var padLine    = $"[{fillTag}]{v}{emptyInner}{v}[/]";
        var middleLine = $"[{fillTag}]{v}[/]" +
                         $"[{textTag}]{new string(' ', leftPad)}{label}{new string(' ', rightPad)}[/]" +
                         $"[{fillTag}]{v}[/]";
        var bottomLine = $"[{colorTag}]{bl}{hBar}{br}[/]";

        return new Markup($"{topLine}\n{padLine}\n{middleLine}\n{padLine}\n{bottomLine}");
    }

    private static string GetColorName(Color color)
    {
        if (color == Color.Red) return "red";
        if (color == Color.DodgerBlue1) return "dodgerblue1";
        if (color == Color.Blue) return "blue";
        if (color == Color.White) return "white";
        if (color == Color.Grey93) return "grey93";
        if (color == Color.NavajoWhite3) return "navajowhite3";
        if (color == Color.SandyBrown) return "sandybrown";
        if (color == Color.Yellow) return "yellow";
        if (color == Color.Grey50) return "grey50";
        if (color == Color.Grey70) return "grey70";
        if (color == Color.Grey23) return "grey23";
        if (color == Color.DarkRed) return "darkred";
        if (color == Color.NavyBlue) return "navyblue";
        if (color == Color.Wheat1) return "wheat1";
        if (color == Color.Black) return "black";
        if (color == Color.Gold1) return "gold1";
        return "grey";
    }

    public WordCard RenderHighlightedWord(WordCard card)
    {
        AnsiConsole.Write(new Text($"  [{card.Word}]  — arrows to navigate, Enter to vote, Esc to cancel\n",
            new Style(foreground: Color.Silver)));
        return card;
    }

    public static void RenderTimerBar(string label, DateTimeOffset endsAt, int totalSeconds, bool pulsePhase = false)
    {
        const int barWidth = 30;
        var remaining = endsAt - DateTimeOffset.UtcNow;
        var secondsLeft = Math.Max(0, (int)remaining.TotalSeconds);
        var fraction = totalSeconds > 0 ? Math.Clamp((double)secondsLeft / totalSeconds, 0.0, 1.0) : 0.0;

        var filled = (int)Math.Round(fraction * barWidth);
        var empty  = barWidth - filled;

        string color = AnimationHelper.TimerColor(fraction);

        // Pulse effect when under 5 seconds
        var urgency = "";
        if (secondsLeft <= 5 && secondsLeft > 0)
        {
            color = pulsePhase ? "bold red" : "dim red";
            urgency = pulsePhase ? " !!!" : " !!!";
        }

        var bar = new string('█', filled) + new string('░', empty);
        AnsiConsole.MarkupLine($"  [{color}]⏱  {label,-16} [[{bar}]] {secondsLeft,2}s{urgency}[/]");
    }

    public void RenderStatus(string message) =>
        AnsiConsole.Write(new Text($"  {message}\n", new Style(foreground: Color.Aqua)));

    public void RenderError(string message) =>
        AnsiConsole.Write(new Text($"  {message}\n", new Style(foreground: Color.Red)));

    public static void RenderBanner(string text, string color, bool pulse = false, bool pulsePhase = false)
    {
        string style;
        if (pulse)
            style = pulsePhase ? $"bold {color}" : $"dim {color}";
        else
            style = $"bold {color}";

        var panel = new Panel($"[{style}]{Markup.Escape(text)}[/]")
        {
            Border = BoxBorder.Double,
            Padding = new Padding(2, 0),
            Width = 54
        };
        panel.BorderStyle = new Style(foreground: GetColorFromName(color));
        AnsiConsole.Write(Align.Center(panel));
    }

    public static void RenderStatusPanel(string message, string color)
    {
        var panel = new Panel($"[{color}]{Markup.Escape(message)}[/]")
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0),
        };
        panel.BorderStyle = new Style(foreground: GetColorFromName(color));
        AnsiConsole.Write(panel);
    }

    public static void RenderScoreBar(string teamLabel, string teamColor, int remaining, int total)
    {
        const int barWidth = 10;
        var filled = (int)Math.Round((double)remaining / total * barWidth);
        var empty = barWidth - filled;

        var bar = new string('█', filled) + new string('░', empty);
        AnsiConsole.Markup($"[{teamColor}]{teamLabel} [[{bar}]] {remaining} left[/]");
    }

    private static Color GetColorFromName(string name) => name.ToLowerInvariant() switch
    {
        "red" => Color.Red,
        "blue" => Color.DodgerBlue1,
        "yellow" => Color.Yellow,
        "green" => Color.Green,
        "gold1" => Color.Gold1,
        "grey" => Color.Grey,
        "aqua" => Color.Aqua,
        "white" => Color.White,
        _ => Color.Grey
    };
}
