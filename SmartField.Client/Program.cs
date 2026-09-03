using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SmartField.Client;
using SmartField.Client.Auth;
using SmartField.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiConfiguration:BaseUrl"];
var apiBaseAddress = string.IsNullOrWhiteSpace(apiBaseUrl)
    ? new Uri(builder.HostEnvironment.BaseAddress)
    : new Uri(apiBaseUrl, UriKind.Absolute);

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ITokenStore, SessionStorageTokenStore>();
builder.Services.AddScoped<SmartFieldAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<SmartFieldAuthenticationStateProvider>());
builder.Services.AddScoped<BearerTokenHandler>();
builder.Services.AddHttpClient("SmartField.Api", client =>
    {
        client.BaseAddress = apiBaseAddress;
    })
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped(provider =>
    provider.GetRequiredService<IHttpClientFactory>().CreateClient("SmartField.Api"));
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<AttendanceApiClient>();
builder.Services.AddScoped<BrowserGeolocationService>();
builder.Services.AddScoped<EmployeeApiClient>();
builder.Services.AddScoped<GeofenceSettingsApiClient>();
builder.Services.AddScoped<GeolocationApiClient>();
builder.Services.AddScoped<ProjectApiClient>();
builder.Services.AddScoped<WorkSiteApiClient>();

await builder.Build().RunAsync();
