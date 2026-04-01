using Codenames.Cli.Models;
using Codenames.Cli.Enums;
using Codenames.Cli.Tui;
using Spectre.Console;

namespace Codenames.Cli.Screens;

public record WordCard(int Id, string Word, WordCategory Category, bool Revealed)
{
    public Color GetDisplayColor(bool showColors) =>
        (showColors || Revealed) ? Category switch
        {
            WordCategory.RED      => Color.DarkRed,
            WordCategory.BLUE     => Color.Blue,
            WordCategory.ASSASSIN => Color.White,
            WordCategory.NEUTRAL  => Color.SandyBrown,
            _                     => Color.Grey
        } : Color.Grey;

    public Color GetBorderColor(bool showColors) =>
        (showColors || Revealed) ? Category switch
        {
            WordCategory.RED      => Color.Red,
            WordCategory.BLUE     => Color.Blue,
            WordCategory.ASSASSIN => Color.White,
            WordCategory.NEUTRAL  => Color.SandyBrown,
            _                     => Color.Grey
        } : Color.Grey;
}