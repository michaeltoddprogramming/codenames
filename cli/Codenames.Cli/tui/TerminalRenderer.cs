using Spectre.Console;
using Codenames.Cli.Screens;

namespace Codenames.Cli.Tui;

public class TerminalRenderer
{
    public void Clear() => AnsiConsole.Clear();

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

    public void RenderBoard(WordCard[,] grid, int cursorRow, int cursorCol, bool showColors)
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
                cells.Add(RenderCardAsBox(card, isSelected, showColors));
            }
            table.AddRow(cells.ToArray());
        }

        AnsiConsole.Write(table);
    }

    private static Markup RenderCardAsBox(WordCard card, bool isSelected, bool showColors)
    {
        var escapedWord = card.Word.Replace("[", "[[").Replace("]", "]]");
        var label = card.Revealed ? $"✓ {escapedWord}" : escapedWord;

        var borderColor = card.GetBorderColor(showColors);
        var colorName = GetColorName(borderColor);

        var tl = isSelected ? "╔" : "┌";
        var tr = isSelected ? "╗" : "┐";
        var bl = isSelected ? "╚" : "└";
        var br = isSelected ? "╝" : "┘";
        var h  = isSelected ? "═" : "─";
        var v  = isSelected ? "║" : "│";

        var box = $"{tl}{h}{h}{h}{h}{h}{h}{tr}\n" +
                  $"{v} {label} {v}\n" +
                  $"{bl}{h}{h}{h}{h}{h}{h}{br}";

        return new Markup($"[{colorName}]{box}[/]");
    }

    private static string GetColorName(Color color)
    {
        if (color == Color.Red) return "red";
        if (color == Color.Blue) return "blue";
        if (color == Color.White) return "white";
        if (color == Color.SandyBrown) return "sandybrown";
        return "grey";
    }

    public WordCard RenderHighlightedWord(WordCard card)
    {
        AnsiConsole.Write(new Text($"  [{card.Word}]  — arrows to navigate, Enter to vote, Esc to cancel\n",
            new Style(foreground: Color.Silver)));
        return card;
    }

    public void RenderStatus(string message) =>
        AnsiConsole.Write(new Text($"  {message}\n", new Style(foreground: Color.Aqua)));

    public void RenderError(string message) =>
        AnsiConsole.Write(new Text($"  {message}\n", new Style(foreground: Color.Red)));
}
