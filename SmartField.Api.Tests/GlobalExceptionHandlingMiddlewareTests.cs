using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SmartField.Api.Middleware;

namespace SmartField.Api.Tests;

public class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsGenericProblemDetailsWithCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.ItemName] = "corr-789";
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("database secret detail"),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var problem = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("application/problem+json", context.Response.ContentType);
        Assert.Equal(
            "corr-789",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
        Assert.Equal("corr-789", problem.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("database secret detail", body);
    }
}
