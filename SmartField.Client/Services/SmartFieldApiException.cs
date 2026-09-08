using System.Net;

namespace SmartField.Client.Services;

public class SmartFieldApiException : Exception
{
    public SmartFieldApiException(
        HttpStatusCode statusCode,
        string message,
        string? correlationId = null)
        : base(message)
    {
        StatusCode = statusCode;
        CorrelationId = correlationId;
    }

    public HttpStatusCode StatusCode { get; }

    public string? CorrelationId { get; }
}
