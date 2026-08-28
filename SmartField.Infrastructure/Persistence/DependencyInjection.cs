using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartField.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddSqlServerPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SmartField");

        services.AddDbContext<SmartFieldDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }
}
