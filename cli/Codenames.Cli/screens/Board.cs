using Codenames.Cli.Models;
using Codenames.Cli.Tui;
using Spectre.Console;

namespace Codenames.Cli.Screens;

public class Board
{
    private const int Size = 5;

    private readonly WordCard[,] _grid;
    private readonly TerminalRenderer _renderer;
    private readonly bool _showColors;
    private readonly string? _clueWord;
    private readonly int? _clueNumber;
    private int _cursorRow;
    private int _cursorCol;

    public WordCard SelectedCard => _grid[_cursorRow, _cursorCol];

    public Board(IReadOnlyList<WordCard> cards, TerminalRenderer renderer, bool showColors = false, string? clueWord = null, int? clueNumber = null)
    {
        if (cards.Count != Size * Size)
            throw new ArgumentException($"Board requires exactly {Size * Size} cards.");

        _renderer = renderer;
        _showColors = showColors;
        _clueWord = clueWord;
        _clueNumber = clueNumber;
        _grid = new WordCard[Size, Size];

        for (int i = 0; i < Size * Size; i++)
            _grid[i / Size, i % Size] = cards[i];
    }

    public WordCard? Run()
    {
        WordCard? selectedCardVote = null;
        Console.CursorVisible = false;

        while (true)
        {
            RenderHeader();
            _renderer.RenderBoard(_grid, _cursorRow, _cursorCol, _showColors);
            if (!_showColors)
                _renderer.RenderHighlightedWord(SelectedCard);

            var key = Console.ReadKey(intercept: true);

            if (_showColors)
            {
                // Spymaster view — read-only, only Escape exits
                if (key.Key == ConsoleKey.Escape)
                {
                    Console.CursorVisible = true;
                    return null;
                }
            }
            else
            {
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        _cursorRow = Math.Max(0, _cursorRow - 1);
                        break;
                    case ConsoleKey.DownArrow:
                        _cursorRow = Math.Min(Size - 1, _cursorRow + 1);
                        break;
                    case ConsoleKey.LeftArrow:
                        _cursorCol = Math.Max(0, _cursorCol - 1);
                        break;
                    case ConsoleKey.RightArrow:
                        _cursorCol = Math.Min(Size - 1, _cursorCol + 1);
                        break;
                    case ConsoleKey.Enter:
                        var card = _grid[_cursorRow, _cursorCol];
                        var revealedCard = card with { Revealed = true };
                        _grid[_cursorRow, _cursorCol] = revealedCard;
                        selectedCardVote = revealedCard;
                        AnsiConsole.Write(new Markup($"[green]  Selected: {revealedCard.Word}[/]\n"));
                        break;
                    case ConsoleKey.Escape:
                        Console.CursorVisible = true;
                        return selectedCardVote;
                }
            }
        }
    }

    private void RenderHeader()
    {
        _renderer.Clear();
        
        RenderHeaderNoClear();
    }

    private void RenderHeaderNoClear()
    {
        // Title
        AnsiConsole.Write(new FigletText("Board").Color(Color.Blue));
        AnsiConsole.WriteLine();

        // Clue
        if (!string.IsNullOrEmpty(_clueWord) && _clueNumber.HasValue)
        {
            AnsiConsole.Write(new Markup($"[yellow]Clue:[/] [bold]{_clueWord}[/] ({_clueNumber})[/]"));
        }
        else
        {
            AnsiConsole.Write(new Markup("[yellow]Clue:[/] [dim]No clue yet[/]"));
        }
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        // Color key legend
        AnsiConsole.Write(new Markup("Key: \n[red]Red[/] = Red team words  \n[blue]Blue[/] = Blue team words  \n[sandybrown]Brown[/] = Neutral words  \n[white]White[/] = Assassin word  \n[grey]Grey[/] = Unrevealed words"));
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }
}
