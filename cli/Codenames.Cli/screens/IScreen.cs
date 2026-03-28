namespace Codenames.Cli.Screens;

public interface IScreen
{
    Task RenderAsync(CancellationToken cancellationToken = default);
}
