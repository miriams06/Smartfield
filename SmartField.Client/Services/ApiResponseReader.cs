using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartField.Client.Services;

public static class ApiResponseReader
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        return await ReadRequiredAsync<T, SmartFieldApiException>(
            response,
            static (statusCode, message, correlationId) =>
                new SmartFieldApiException(statusCode, message, correlationId),
            cancellationToken);
    }

    public static async Task<T> ReadRequiredAsync<T, TException>(
        HttpResponseMessage response,
        Func<HttpStatusCode, string, string?, TException> exceptionFactory,
        CancellationToken cancellationToken)
        where T : class
        where TException : Exception
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(
                response,
                exceptionFactory,
                cancellationToken);
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(
                cancellationToken: cancellationToken);

            return value ?? throw exceptionFactory(
                response.StatusCode,
                "A API devolveu uma resposta vazia.",
                GetCorrelationId(response, null));
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            throw exceptionFactory(
                response.StatusCode,
                "A API devolveu uma resposta inválida.",
                GetCorrelationId(response, null));
        }
    }

    public static Task<SmartFieldApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return CreateExceptionAsync(
            response,
            static (statusCode, message, correlationId) =>
                new SmartFieldApiException(statusCode, message, correlationId),
            cancellationToken);
    }

    public static async Task<TException> CreateExceptionAsync<TException>(
        HttpResponseMessage response,
        Func<HttpStatusCode, string, string?, TException> exceptionFactory,
        CancellationToken cancellationToken)
        where TException : Exception
    {
        var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
        var correlationId = GetCorrelationId(response, problem);
        var functionalMessage = GetFunctionalMessage(problem);

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest =>
                functionalMessage ?? "O pedido contém dados inválidos.",
            HttpStatusCode.Unauthorized =>
                functionalMessage ?? "A autenticação não é válida ou expirou.",
            HttpStatusCode.Forbidden =>
                functionalMessage ?? "Não tem permissão para executar esta operação.",
            HttpStatusCode.Conflict =>
                functionalMessage ?? "O pedido entra em conflito com o estado atual dos dados.",
            _ when (int)response.StatusCode >= 500 =>
                "Ocorreu um erro ao processar o pedido.",
            _ => functionalMessage
                ?? $"O pedido falhou com o estado {(int)response.StatusCode}."
        };

        if ((int)response.StatusCode >= 500
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            message = $"{message} Código: {correlationId}";
        }

        return exceptionFactory(response.StatusCode, message, correlationId);
    }

    private static async Task<ApiProblemDetails?> TryReadProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiProblemDetails>(
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? GetFunctionalMessage(ApiProblemDetails? problem)
    {
        var validationMessage = problem?.Errors?
            .SelectMany(pair => pair.Value ?? [])
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return validationMessage.Trim();
        }

        if (!string.IsNullOrWhiteSpace(problem?.Detail))
        {
            return problem.Detail.Trim();
        }

        return string.IsNullOrWhiteSpace(problem?.Title)
            ? null
            : problem.Title.Trim();
    }

    private static string? GetCorrelationId(
        HttpResponseMessage response,
        ApiProblemDetails? problem)
    {
        if (response.Headers.TryGetValues(CorrelationIdHeaderName, out var values))
        {
            var headerValue = values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue.Trim();
            }
        }

        if (problem?.Extensions is null)
        {
            return null;
        }

        var extension = problem.Extensions.FirstOrDefault(pair =>
            string.Equals(pair.Key, "correlationId", StringComparison.OrdinalIgnoreCase));

        return extension.Value.ValueKind == JsonValueKind.String
            ? extension.Value.GetString()?.Trim()
            : null;
    }

    private sealed class ApiProblemDetails
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }

        public Dictionary<string, string[]>? Errors { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; set; }
    }
}
