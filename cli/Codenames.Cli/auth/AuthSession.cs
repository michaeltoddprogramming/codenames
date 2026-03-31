using System.Text;
using System.Text.Json;

namespace Codenames.Cli.Auth;

public class AuthSession
{
    public string? Jwt { get; private set; }
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public int? UserId { get; private set; }
    public bool IsAuthenticated => Jwt is not null;

    public void Set(string jwt, string email, string name)
    {
        Jwt = jwt;
        Email = email;
        Name = name;
        UserId = TryGetSubjectAsInt(jwt);
    }

    public void Clear()
    {
        Jwt = null;
        Email = null;
        Name = null;
        UserId = null;
    }

    // Extracts the "sub" claim from our own server-issued JWT without signature validation.
    // The CLI already trusts this token (it was received over HTTPS from our auth endpoint),
    // so full verification is unnecessary here — we only need the embedded user ID.
    private static int? TryGetSubjectAsInt(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            while (payload.Length % 4 != 0)
            {
                payload += "=";
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("sub", out var subject))
            {
                return null;
            }

            return int.TryParse(subject.GetString(), out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }
}
