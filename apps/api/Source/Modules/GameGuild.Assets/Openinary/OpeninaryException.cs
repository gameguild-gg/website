using System.Net;

namespace GameGuild.Assets.Openinary;

/// <summary>
/// Thrown when the Openinary service replies with a non-success response.
/// Carries the HTTP status code and the error message from the JSON body when present.
/// </summary>
public sealed class OpeninaryException : Exception
{
    public OpeninaryException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
