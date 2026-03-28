using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Codenames.Cli.Auth;

public class ServerClient(HttpClient httpClient)
{
    public async Task<ServerAuthResponse> AuthenticateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var resp = await httpClient.PostAsJsonAsync("/auth/google", new { idToken }, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Server auth failed ({(int)resp.StatusCode}): {body}");

        return await resp.Content.ReadFromJsonAsync<ServerAuthResponse>(cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("Empty response from server.");
    }
}

public record ServerAuthResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")]  string Name);
