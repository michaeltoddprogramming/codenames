using Spectre.Console;

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
}
