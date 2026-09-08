using System.Net;
using System.Text;
using SmartField.Client.Auth;
using SmartField.Client.Services;

namespace SmartField.Client.Tests;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsFalse_ForInvalidCredentials()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await service.LoginAsync(
            "user@example.com",
            "wrong-password",
            CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task LoginAsync_PreservesServerErrorCorrelationId()
    {
        var service = CreateService(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(
                    """{"title":"Erro","detail":"internal detail","correlationId":"corr-login-500"}""",
                    Encoding.UTF8,
                    "application/problem+json")
            };

            return response;
        });

        var exception = await Assert.ThrowsAsync<SmartFieldApiException>(() =>
            service.LoginAsync(
                "user@example.com",
                "password",
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal("corr-login-500", exception.CorrelationId);
        Assert.Equal(
            "Ocorreu um erro ao processar o pedido. Código: corr-login-500",
            exception.Message);
        Assert.DoesNotContain("internal detail", exception.Message);
    }

    [Fact]
    public async Task LoginAsync_DoesNotConvertCommunicationFailureIntoHttpStatusError()
    {
        var service = CreateService(_ => throw new HttpRequestException("offline"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.LoginAsync(
                "user@example.com",
                "password",
                CancellationToken.None));
    }

    private static AuthenticationService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var tokenStore = new FakeTokenStore();
        var stateProvider = new SmartFieldAuthenticationStateProvider(tokenStore);
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://smartfield.test/")
        };

        return new AuthenticationService(httpClient, tokenStore, stateProvider);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responseFactory;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        private string? token;

        public ValueTask<string?> GetTokenAsync() => ValueTask.FromResult(token);

        public ValueTask SetTokenAsync(string token)
        {
            this.token = token;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearTokenAsync()
        {
            token = null;
            return ValueTask.CompletedTask;
        }
    }
}
