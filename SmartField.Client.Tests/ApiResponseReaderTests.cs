using System.Net;
using System.Text;
using SmartField.Client.Services;

namespace SmartField.Client.Tests;

public class ApiResponseReaderTests
{
    [Fact]
    public async Task BadRequest_UsesValidationMessage()
    {
        using var response = CreateProblemResponse(
            HttpStatusCode.BadRequest,
            """{"title":"Validation failed","errors":{"Name":["O nome é obrigatório."]}}""");

        var exception = await ApiResponseReader.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("O nome é obrigatório.", exception.Message);
    }

    [Fact]
    public async Task Unauthorized_UsesAuthenticationMessage_WhenProblemDetailsAreMissing()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var exception = await ApiResponseReader.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal("A autenticação não é válida ou expirou.", exception.Message);
    }

    [Fact]
    public async Task Forbidden_UsesPermissionMessage()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden);

        var exception = await ApiResponseReader.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal("Não tem permissão para executar esta operação.", exception.Message);
    }

    [Fact]
    public async Task Conflict_PreservesFunctionalProblemDetailsMessage()
    {
        using var response = CreateProblemResponse(
            HttpStatusCode.Conflict,
            """{"title":"Conflito","detail":"Sequência de picagens inválida."}""");

        var exception = await ApiResponseReader.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal("Sequência de picagens inválida.", exception.Message);
    }

    [Fact]
    public async Task InternalServerError_UsesSafeMessageAndCorrelationHeader()
    {
        using var response = CreateProblemResponse(
            HttpStatusCode.InternalServerError,
            """{"title":"Não foi possível processar o pedido.","detail":"detalhe que não deve ser mostrado","correlationId":"body-correlation"}""");
        response.Headers.TryAddWithoutValidation("X-Correlation-ID", "header-correlation");

        var exception = await ApiResponseReader.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal("header-correlation", exception.CorrelationId);
        Assert.Equal(
            "Ocorreu um erro ao processar o pedido. Código: header-correlation",
            exception.Message);
        Assert.DoesNotContain("detalhe que não deve ser mostrado", exception.Message);
    }

    [Fact]
    public async Task InternalServerError_UsesCorrelationIdFromProblemDetails_WhenHeaderIsMissing()
    {
        using var response = CreateProblemResponse(
            HttpStatusCode.InternalServerError,
            """{"title":"Erro","correlationId":"corr-body-123"}""");

        var exception = await ApiResponseReader.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal("corr-body-123", exception.CorrelationId);
        Assert.Contains("Código: corr-body-123", exception.Message);
    }

    [Fact]
    public async Task InvalidSuccessPayload_ReturnsSafeClientError()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        };

        var exception = await Assert.ThrowsAsync<SmartFieldApiException>(() =>
            ApiResponseReader.ReadRequiredAsync<SampleDto>(
                response,
                CancellationToken.None));

        Assert.Equal("A API devolveu uma resposta inválida.", exception.Message);
    }

    private static HttpResponseMessage CreateProblemResponse(
        HttpStatusCode statusCode,
        string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/problem+json")
        };
    }

    private sealed record SampleDto(string Value);
}
