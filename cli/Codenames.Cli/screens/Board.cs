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

    public BoardResult Run()
    {
        Console.CursorVisible = false;
      
        int boardTop = Console.CursorTop;

        while (true)
        {
            Console.SetCursorPosition(0, boardTop);

            _renderer.RenderBoard(_grid, _cursorRow, _cursorCol, _showColors);

            if (!_showColors)
                _renderer.RenderHighlightedWord(SelectedCard);

            var key = Console.ReadKey(intercept: true);

            if (_showColors)
            {
                switch (key.Key)
                {
                    case ConsoleKey.C:
                        Console.CursorVisible = true;
                        return new BoardResult(BoardAction.GiveClue);

                    case ConsoleKey.Escape:
                        Console.CursorVisible = true;
                        return new BoardResult(BoardAction.Escape);
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
                        Console.CursorVisible = true;
                        return new BoardResult(BoardAction.CardSelected, revealedCard);
                    case ConsoleKey.Escape:
                        Console.CursorVisible = true;
                        return new BoardResult(BoardAction.Escape);
                }
            }
        }
    }
}