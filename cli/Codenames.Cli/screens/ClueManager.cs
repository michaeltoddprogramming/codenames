using Codenames.Cli.Api;
using Codenames.Cli.Models;
using Codenames.Cli.Tui;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Codenames.Cli.Screens;

public class ClueManager
{
    private readonly GameApiClient _gameApiClient;
    private readonly TerminalRenderer _renderer;
    private readonly ILogger<ClueManager>? _logger;

    public ClueManager(
        GameApiClient gameApiClient,
        TerminalRenderer renderer,
        ILogger<ClueManager>? logger = null)
    {
        _gameApiClient = gameApiClient;
        _renderer = renderer;
        _logger = logger;
    }

    public void RenderCurrentClue(string? clueWord, int? clueNumber)
    {
        if (!string.IsNullOrEmpty(clueWord) && clueNumber.HasValue)
        {
            AnsiConsole.Write(new Markup($"[yellow]Current clue:[/] [bold]{Markup.Escape(clueWord!)}[/] ({clueNumber})"));
        }
        else
        {
            AnsiConsole.Write(new Markup("[yellow]Current clue:[/] [dim]No clue yet[/]"));
        }
        _renderer.RenderBlankLine();
    }

    public void RenderClueHistory(List<ClueResponse>? clues)
    {
        if (clues != null && clues.Count > 0)
        {
            AnsiConsole.Write(new Rule("[grey]Clues[/]").RuleStyle("grey"));
            foreach (var clue in clues)
            {
                var color = clue.TeamName?.Equals("RED", StringComparison.OrdinalIgnoreCase) == true ? "red" : "dodgerblue1";
                AnsiConsole.Write(new Markup($"[{color}]{clue.TeamName}[/] - {clue.Word} {clue.Number}"));
            }
        }
        else
        {
            AnsiConsole.Write(new Markup("[dim]No clues yet[/]"));
        }
        _renderer.RenderBlankLine();
    }

    public async Task<bool> SubmitClueAsync(int gameId, CancellationToken cancellationToken)
    {
        while (true)
        {
            _renderer.Clear();

            // Title panel
            var titlePanel = new Panel("[bold gold1]Give Your Clue[/]")
            {
                Border = BoxBorder.Double,
                Padding = new Padding(2, 0),
            };
            titlePanel.BorderStyle = new Style(foreground: Color.Gold1);
            AnsiConsole.Write(titlePanel);
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("  [grey]Format:[/] [bold]WORD NUMBER[/]  [dim](e.g. WATER 3)[/]");
            AnsiConsole.MarkupLine("  [dim]Press Esc to cancel[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.Markup("[gold1]  > [/]");

            var input = ReadLineOrEsc();
            if (input is null) return false;

            input = input.Trim();

            var lastSpace = input.LastIndexOf(' ');
            if (lastSpace < 1)
            {
                ShowInputError("Format must be: WORD NUMBER  (e.g. WATER 3)");
                await Task.Delay(1200, cancellationToken);
                continue;
            }

            var word   = input[..lastSpace].Trim();
            var numStr = input[(lastSpace + 1)..].Trim();

            if (string.IsNullOrEmpty(word) || word.Contains(' '))
            {
                ShowInputError("Clue must be a single word.");
                await Task.Delay(1200, cancellationToken);
                continue;
            }

            if (!int.TryParse(numStr, out var clueNumber) || clueNumber < 1 || clueNumber > 9)
            {
                ShowInputError("Number must be between 1 and 9.");
                await Task.Delay(1200, cancellationToken);
                continue;
            }

            try
            {
                await _gameApiClient.SubmitClueAsync(gameId, word, clueNumber, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to submit clue");

                _renderer.Clear();
                AnsiConsole.MarkupLine("[yellow]Give clue[/]");
                _renderer.RenderBlankLine();
                _renderer.RenderError(GetFriendlyClueErrorMessage(ex));
                _renderer.RenderBlankLine();
                _renderer.RenderStatus("Press any key to return to the board...");
                Console.ReadKey(intercept: true);
                return false;
            }
        }
    }

    private static string GetFriendlyClueErrorMessage(Exception ex)
    {
        var message = ex.Message;

        if (message.Contains("already has an active round", StringComparison.OrdinalIgnoreCase))
        {
            return "You already submitted a clue for this round. You must wait for the next round before submitting another clue.";
        }

        return $"Server rejected clue: {message}";
    }

    private static void ShowInputError(string message)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(message)}[/]");
    }

    private static string? ReadLineOrEsc()
    {
        var buffer = new System.Text.StringBuilder();
        Console.CursorVisible = true;
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape)   return null;
                if (key.Key == ConsoleKey.Enter)     break;
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Remove(buffer.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    continue;
                }
                if (!char.IsControl(key.KeyChar))
                {
                    buffer.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
        }
        finally { Console.CursorVisible = false; }

        Console.WriteLine();
        return buffer.ToString();
    }
}
