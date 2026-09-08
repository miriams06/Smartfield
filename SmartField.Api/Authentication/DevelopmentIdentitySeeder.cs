namespace SmartField.Api.Authentication;

public static class DevelopmentIdentitySeeder
{
    public static async Task SeedDevelopmentIdentityAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        using var scope = app.Services.CreateScope();
        await DevelopmentDataSeeder.SeedAsync(
            scope.ServiceProvider, app.Environment, app.Configuration, app.Lifetime.ApplicationStopping);
    }
}
