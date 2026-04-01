using Spectre.Console;

namespace Codenames.Cli.Tui;

public static class AnimationHelper
{
    private static readonly string[] ConfettiChars = ["*", "+", ".", "o", "~", "^"];
    private static readonly string[] ConfettiColors = ["red", "blue", "yellow", "green", "magenta", "cyan", "gold1", "dodgerblue1"];

    public static string PulseMarkup(string text, string boldColor, string dimColor, bool phase)
    {
        var style = phase ? $"bold {boldColor}" : $"dim {dimColor}";
        return $"[{style}]{Markup.Escape(text)}[/]";
    }

    public static bool ShouldShowFlash(DateTimeOffset setAt, double durationSeconds)
    {
        return (DateTimeOffset.UtcNow - setAt).TotalSeconds < durationSeconds;
    }

    public static string ConfettiLine(int width, Random rng)
    {
        var chars = new char[width];
        var markupParts = new System.Text.StringBuilder();

        for (int i = 0; i < width; i++)
        {
            if (rng.NextDouble() < 0.3)
            {
                var c = ConfettiChars[rng.Next(ConfettiChars.Length)];
                var color = ConfettiColors[rng.Next(ConfettiColors.Length)];
                markupParts.Append($"[{color}]{c}[/]");
            }
            else
            {
                markupParts.Append(' ');
            }
        }

        return markupParts.ToString();
    }

    public static string TimerColor(double fraction)
    {
        if (fraction > 0.5) return "green";
        if (fraction > 0.25) return "yellow";
        if (fraction > 0.10) return "orange1";
        return "red";
    }
}
