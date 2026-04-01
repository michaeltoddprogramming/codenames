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
        AnsiConsole.Write(new FigletText(title).Color(Color.Blue));

    public void RenderBlankLine() => AnsiConsole.WriteLine();

    public void RenderMenuItem(string label, bool isSelected)
    {
        var prefix = isSelected ? "> " : "  ";
        var style = isSelected
            ? new Style(foreground: Color.Black, background: Color.White)
            : new Style(foreground: Color.Silver);
        AnsiConsole.Write(new Text($"{prefix}{label}\n", style));
    }

    public void RenderBoard(WordCard[,] grid, int cursorRow, int cursorCol, bool showColors, IReadOnlySet<string>? myVotedWords = null)
    {
        var table = new Table().HideHeaders().Border(TableBorder.None);

        for (int col = 0; col < 5; col++)
            table.AddColumn(new TableColumn("").Width(16).Centered());

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

    private static readonly int BoxWidth = 12;

    private static Markup RenderCardAsBox(WordCard card, bool isSelected, bool showColors, bool isVotedByMe = false)
    {
        var escapedWord = card.Word.Replace("[", "[[").Replace("]", "]]");
        var prefix = card.Revealed ? "✓ " : "";
        var label = prefix + escapedWord;

        var borderColor = card.GetBorderColor(showColors);
        var colorName = GetColorName(borderColor);

        var (tl, tr, bl, br, h, v) = isSelected
            ? ("╔", "╗", "╚", "╝", "═", "║")
            : ("┌", "┐", "└", "┘", "─", "│");

        var innerWidth = BoxWidth - 2;
        var padding = Math.Max(0, innerWidth - label.Length);
        var leftPad = padding / 2;
        var rightPad = padding - leftPad;

        var topBottom = $"{tl}{string.Concat(Enumerable.Repeat(h, innerWidth))}{tr}";
        var middle = $"{v}{new string(' ', leftPad)}{label}{new string(' ', rightPad)}{v}";
        var bottom = $"{bl}{string.Concat(Enumerable.Repeat(h, innerWidth))}{br}";

        return new Markup($"[{colorName}]{topBottom}[/]\n" +
                          $"[{colorName}]{middle}[/]\n" +
                          $"[{colorName}]{bottom}[/]");
    }

    private static string GetColorName(Color color)
    {
        if (color == Color.Red) return "red";
        if (color == Color.Blue) return "blue";
        if (color == Color.White) return "white";
        if (color == Color.SandyBrown) return "sandybrown";
        if (color == Color.Yellow) return "yellow";
        return "grey";
    }

    public WordCard RenderHighlightedWord(WordCard card)
    {
        AnsiConsole.Write(new Text($"  [{card.Word}]  — arrows to navigate, Enter to vote, Esc to cancel\n",
            new Style(foreground: Color.Silver)));
        return card;
    }

    public static void RenderTimerBar(string label, DateTimeOffset endsAt, int totalSeconds)
    {
        const int barWidth = 30;
        var remaining = endsAt - DateTimeOffset.UtcNow;
        var secondsLeft = Math.Max(0, (int)remaining.TotalSeconds);
        var fraction = totalSeconds > 0 ? Math.Clamp((double)secondsLeft / totalSeconds, 0.0, 1.0) : 0.0;

        var filled = (int)Math.Round(fraction * barWidth);
        var empty  = barWidth - filled;

        string color;
        if (fraction > 0.5) color = "green";
        else if (fraction > 0.25) color = "yellow";
        else color = "red";

        var bar = new string('█', filled) + new string('░', empty);
        AnsiConsole.MarkupLine($"  [{color}]⏱ {label,-16} [[{bar}]] {secondsLeft,2}s[/]");
    }

    public void RenderStatus(string message) =>
        AnsiConsole.Write(new Text($"  {message}\n", new Style(foreground: Color.Aqua)));

    public void RenderError(string message) =>
        AnsiConsole.Write(new Text($"  {message}\n", new Style(foreground: Color.Red)));
}
