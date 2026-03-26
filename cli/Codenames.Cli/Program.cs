using Spectre.Console;

namespace Codenames.Cli;

public class Program
{
    static void Main(string[] args)
    {
        var figlet = new FigletText("Codenames CLI")
            .Color(Color.Blue);
  
        AnsiConsole.Write(figlet);
        AnsiConsole.MarkupLine("Press any key to exit...");
        Console.ReadKey();
    }
}
