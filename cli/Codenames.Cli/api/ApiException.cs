using System.Net;

namespace Codenames.Cli.Api;

public class ApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}
