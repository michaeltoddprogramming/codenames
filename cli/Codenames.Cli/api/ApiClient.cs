using Codenames.Cli.Auth;
using System.Net.Http.Json;

namespace Codenames.Cli.Api;

public class ApiClient(HttpClient http, AuthSession session)
{
    public Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, path, body: null, bearer: true, cancellationToken);

    public Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, path, body, bearer: true, cancellationToken);

    public Task PostAsync(string path, object body, CancellationToken cancellationToken = default) =>
        SendVoidAsync(HttpMethod.Post, path, body, bearer: true, cancellationToken);

    public Task<T> PostUnauthAsync<T>(string path, object body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, path, body, bearer: false, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool bearer, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, path, body, bearer);
        var response = await http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
               ?? throw new ApiException(response.StatusCode, response.RequestMessage?.ToString() ?? "Unknown error");
    }

    private async Task SendVoidAsync(HttpMethod method, string path, object? body, bool bearer, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, path, body, bearer);
        var response = await http.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body, bool bearer)
    {
        var request = new HttpRequestMessage(method, path);
        if (body != null) request.Content = JsonContent.Create(body);
        if (bearer && !session.IsAuthenticated)
            throw new InvalidOperationException("Cannot make authenticated request: no active session.");
        if (bearer)
            request.Headers.Authorization = new("Bearer", session.Jwt);

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ApiException(response.StatusCode, body);
        }
    }
}
