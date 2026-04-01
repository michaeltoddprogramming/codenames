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
            WordCategory.RED      => Color.White,
            WordCategory.BLUE     => Color.White,
            WordCategory.ASSASSIN => Color.White,
            WordCategory.NEUTRAL  => Color.Black,
            _                     => Color.Grey70
        } : Color.Grey70;

    public Color GetBorderColor(bool showColors) =>
        (showColors || Revealed) ? Category switch
        {
            WordCategory.RED      => Color.Red,
            WordCategory.BLUE     => Color.DodgerBlue1,
            WordCategory.ASSASSIN => Color.Grey93,
            WordCategory.NEUTRAL  => Color.NavajoWhite3,
            _                     => Color.Grey50
        } : Color.Grey50;

    public Color? GetBackgroundColor(bool showColors) =>
        (showColors || Revealed) ? Category switch
        {
            WordCategory.RED      => Color.DarkRed,
            WordCategory.BLUE     => Color.NavyBlue,
            WordCategory.ASSASSIN => Color.Grey23,
            WordCategory.NEUTRAL  => Color.Wheat1,
            _                     => (Color?)null
        } : null;
}
