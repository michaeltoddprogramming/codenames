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
            AnsiConsole.Write(new Markup($"[yellow]Current clue:[/] [bold]{clueWord}[/] ({clueNumber})[/]"));
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
            AnsiConsole.Write(new Rule("Clues").RuleStyle("grey"));
            foreach (var clue in clues)
            {
                var color = clue.TeamName?.Equals("RED", StringComparison.OrdinalIgnoreCase) == true ? "red" : "blue";
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
        _renderer.Clear();
        AnsiConsole.Write(new FigletText("Give Clue").Color(Color.Blue));
        _renderer.RenderBlankLine();

        AnsiConsole.Write(new Markup("[yellow]Enter clue word:[/] "));
        var clueWord = AnsiConsole.Ask<string>("");

        AnsiConsole.Write(new Markup("[yellow]Enter number:[/] "));
        if (!int.TryParse(AnsiConsole.Ask<string>(""), out var clueNumber))
        {
            _renderer.RenderError("Invalid number. Press any key to return...");
            Console.ReadKey(intercept: true);
            return false;
        }

        try
        {
            await _gameApiClient.SubmitClueAsync(gameId, clueWord, clueNumber, cancellationToken);
            _renderer.RenderStatus($"Clue submitted: {clueWord} {clueNumber}");
            _renderer.RenderStatus("Press any key to return to board...");
            Console.ReadKey(intercept: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to submit clue");
            _renderer.RenderError($"Failed to submit clue: {ex.Message}");
            _renderer.RenderStatus("Press any key to return to board...");
            Console.ReadKey(intercept: true);
            return false;
        }
    }
}