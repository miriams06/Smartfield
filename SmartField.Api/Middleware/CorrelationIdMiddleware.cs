using Microsoft.Extensions.Primitives;

namespace SmartField.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemName = "CorrelationId";

    private const int MaxCorrelationIdLength = 128;

    private readonly RequestDelegate next;
    private readonly ILogger<CorrelationIdMiddleware> logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    public static string GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemName, out var value)
            && value is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return context.TraceIdentifier;
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var header)
            && !StringValues.IsNullOrEmpty(header))
        {
            var value = header.ToString().Trim();
            if (value.Length is > 0 and <= MaxCorrelationIdLength)
            {
                return value;
            }
        }

        return context.TraceIdentifier;
    }
}
