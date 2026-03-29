using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Codenames.Cli.Auth;

public class AuthService(IOptions<AuthConfig> options, HttpClient http, ILogger<AuthService> logger)
{
    private readonly AuthConfig _config = options.Value;
    private readonly ILogger<AuthService> _logger = logger;

    private static readonly TimeSpan _listenerTimeout = TimeSpan.FromMinutes(2);

    public async Task<string> GetGoogleIdTokenAsync(CancellationToken cancellationToken = default)
    {
        var codeVerifier  = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state         = GenerateState();

        var authUrl = BuildAuthUrl(codeChallenge, state);

        using var listener = new HttpListener();
        listener.Prefixes.Add(_config.ListenerPrefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            _logger.LogError(ex, "Failed to start HTTP listener.");
            throw new InvalidOperationException(
                $"Failed to start HTTP listener on {_config.ListenerPrefix}.");
        }

        OpenBrowser(authUrl);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationTokenSource.CancelAfter(_listenerTimeout);

        using var stopOnCancel = cancellationTokenSource.Token.Register(() =>
        {
            try { listener.Stop(); } catch {}
        });

        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync();
        }
        catch (HttpListenerException) when (cancellationTokenSource.Token.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for authentication response.");
        }

        var code          = context.Request.QueryString["code"];
        var returnedState = context.Request.QueryString["state"];

        var responseBytes = Encoding.UTF8.GetBytes("Auth complete. You can close this tab.");
        context.Response.ContentLength64 = responseBytes.Length;
        await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
        context.Response.Close();
        listener.Stop();

        if (returnedState != state)
            throw new InvalidOperationException("State mismatch — possible CSRF.");

        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("No authorization code received.");

        return await ExchangeCodeAsync(code, codeVerifier, cancellationToken);
    }

    private string BuildAuthUrl(string codeChallenge, string state)
    {
        var @params = new Dictionary<string, string>
        {
            ["client_id"]             = _config.GoogleClientId,
            ["redirect_uri"]          = _config.RedirectUri,
            ["response_type"]         = "code",
            ["scope"]                 = "openid email profile",
            ["state"]                 = state,
            ["code_challenge"]        = codeChallenge,
            ["code_challenge_method"] = "S256",
        };

        var query = string.Join("&", @params.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"https://accounts.google.com/o/oauth2/v2/auth?{query}";
    }

    private async Task<string> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["code"]          = code,
            ["client_id"]     = _config.GoogleClientId,
            ["client_secret"] = _config.GoogleClientSecret,
            ["redirect_uri"]  = _config.RedirectUri,
            ["grant_type"]    = "authorization_code",
            ["code_verifier"] = codeVerifier,
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(form),
                cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to call Google token endpoint.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Token exchange failed ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id_token", out var idTokenProp))
            throw new InvalidOperationException($"No id_token in response: {body}");

        return idTokenProp.GetString()!;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else
                Process.Start("xdg-open", url);
        }
        catch
        {
            Console.WriteLine($"Please open this URL manually:\n{url}");
        }
    }

    private static string GenerateCodeVerifier() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string GenerateState() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
