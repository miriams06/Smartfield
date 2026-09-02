using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SmartField.Api.Middleware;

namespace SmartField.Api.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_UsesIncomingCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "request-123";
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("request-123", context.Items[CorrelationIdMiddleware.ItemName]);
        Assert.Equal(
            "request-123",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
    }

    [Fact]
    public async Task InvokeAsync_FallsBackToTraceIdentifier()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-456"
        };
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("trace-456", context.Items[CorrelationIdMiddleware.ItemName]);
        Assert.Equal(
            "trace-456",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
    }
}
