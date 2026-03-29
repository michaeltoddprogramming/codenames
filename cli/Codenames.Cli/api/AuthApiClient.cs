namespace Codenames.Cli.Api;

public class AuthApiClient(ApiClient api)
{
    public Task<AuthResponse> LoginAsync(string idToken, CancellationToken cancellationToken = default) =>
        api.PostUnauthAsync<AuthResponse>("/auth/google", new { idToken }, cancellationToken);
}

public record AuthResponse(string Token, string Email, string Name);
