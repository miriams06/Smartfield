using System.Net.Http.Headers;

namespace SmartField.Client.Auth;

public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly ITokenStore tokenStore;

    public BearerTokenHandler(ITokenStore tokenStore)
    {
        this.tokenStore = tokenStore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
