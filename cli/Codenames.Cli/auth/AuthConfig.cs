namespace Codenames.Cli.Auth;

public class AuthConfig
{
    public const string Section = "Auth";

    public string GoogleClientId { get; set; } = "";
    public string GoogleClientSecret { get; set; } = "";
    public string ServerBaseUrl { get; set; } = "http://localhost:8080";
    public string CallbackHost { get; set; } = "127.0.0.1";
    public int CallbackPort { get; set; } = 54321;
    public string CallbackPath { get; set; } = "/callback";

    public string RedirectUri => $"http://{CallbackHost}:{CallbackPort}{CallbackPath}";
    public string ListenerPrefix => $"http://{CallbackHost}:{CallbackPort}/";
}
