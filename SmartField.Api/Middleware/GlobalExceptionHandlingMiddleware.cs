using Microsoft.AspNetCore.Mvc;

namespace SmartField.Api.Middleware;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(
                    exception,
                    "Unhandled exception after response started for {Method} {Path}.",
                    context.Request.Method,
                    context.Request.Path.Value);
                throw;
            }

            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
        var path = context.Request.Path.Value;
        var method = context.Request.Method;

        if (IsSqlFailure(exception))
        {
            logger.LogError(
                exception,
                "SQL failure while processing {Method} {Path}. CorrelationId: {CorrelationId}",
                method,
                path,
                correlationId);
        }
        else
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}. CorrelationId: {CorrelationId}",
                method,
                path,
                correlationId);
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Não foi possível processar o pedido.",
            Detail = "Ocorreu um erro inesperado. Contacte o suporte com o identificador do pedido.",
            Instance = context.Request.Path
        };
        problemDetails.Extensions["correlationId"] = correlationId;

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private static bool IsSqlFailure(Exception exception)
    {
        return exception.GetType().Name is "SqlException" or "DbUpdateException"
            || exception.InnerException is not null && IsSqlFailure(exception.InnerException);
    }
}
