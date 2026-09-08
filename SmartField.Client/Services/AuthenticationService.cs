using System.Net;
using System.Net.Http.Json;
using SmartField.Client.Auth;

namespace SmartField.Client.Services;

public sealed class AuthenticationService
{
    private readonly HttpClient httpClient;
    private readonly ITokenStore tokenStore;
    private readonly SmartFieldAuthenticationStateProvider authenticationStateProvider;

    public AuthenticationService(
        HttpClient httpClient,
        ITokenStore tokenStore,
        SmartFieldAuthenticationStateProvider authenticationStateProvider)
    {
        this.httpClient = httpClient;
        this.tokenStore = tokenStore;
        this.authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<bool> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(email, password),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }

        var login = await ApiResponseReader.ReadRequiredAsync<LoginResponse>(
            response,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(login.AccessToken))
        {
            throw new SmartFieldApiException(
                response.StatusCode,
                "A API devolveu uma resposta inválida.");
        }

        await tokenStore.SetTokenAsync(login.AccessToken);
        authenticationStateProvider.NotifyUserAuthenticationStateChanged();

        return true;
    }

    public async Task LogoutAsync()
    {
        await tokenStore.ClearTokenAsync();
        authenticationStateProvider.NotifyUserSignedOut();
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "api/auth/me",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await LogoutAsync();
            return null;
        }

        return await ApiResponseReader.ReadRequiredAsync<CurrentUserResponse>(
            response,
            cancellationToken);
    }
}
