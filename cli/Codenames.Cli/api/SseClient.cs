using Codenames.Cli.Auth;
using Microsoft.Extensions.Logging;

namespace Codenames.Cli.Api;

public class SseEventArgs(string eventType, string data) : EventArgs
{
    public string EventType { get; } = eventType;
    public string Data { get; } = data;
}

public class SseClient(HttpClient http, AuthSession session, ILogger<SseClient> logger)
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(2);

    public event EventHandler<SseEventArgs>? EventReceived;
    public event EventHandler<bool>? ConnectionStateChanged;

    public async Task ConnectAsync(string path, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await StreamAsync(path, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "SSE stream disconnected, reconnecting in {Delay}s", ReconnectDelay.TotalSeconds);
            }

            ConnectionStateChanged?.Invoke(this, false);

            if (!cancellationToken.IsCancellationRequested)
            {
                try {
                    await Task.Delay(ReconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task StreamAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (session.IsAuthenticated)
            request.Headers.Authorization = new("Bearer", session.Jwt);

        using var response = await http.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        ConnectionStateChanged?.Invoke(this, true);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var eventType = "message";
        var dataLines = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line == null) return;

            if (line.StartsWith("event:"))
            {
                eventType = line["event:".Length..].Trim();
            }
            else if (line.StartsWith("data:"))
            {
                dataLines.Add(line["data:".Length..].Trim());
            }
            else if (line.Length == 0 && dataLines.Count > 0)
            {
                var data = string.Join("\n", dataLines);
                EventReceived?.Invoke(this, new SseEventArgs(eventType, data));

                eventType = "message";
                dataLines.Clear();
            }
        }
    }
}
