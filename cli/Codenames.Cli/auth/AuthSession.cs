namespace Codenames.Cli.Auth;

public class AuthSession
{
    public string? Jwt { get; private set; }
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public bool IsAuthenticated => Jwt is not null;

    public void Set(string jwt, string email, string name)
    {
        Jwt = jwt;
        Email = email;
        Name = name;
    }
}
