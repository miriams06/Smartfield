using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartField.Application.Employees;
using SmartField.Infrastructure.Employees;

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
        services.AddScoped<IEmployeeStore, EmployeeStore>();

        return services;
    }
}
