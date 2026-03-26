using Codenames.Cli;
using Microsoft.Extensions.DependencyInjection;

var host = AppHost.Build(args);
await host.StartAsync();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await host.Services.GetRequiredService<AppRunner>().RunAsync(cts.Token);
await host.StopAsync();
