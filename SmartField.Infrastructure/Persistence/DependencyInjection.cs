using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartField.Application.Attendance;
using SmartField.Application.Employees;
using SmartField.Application.Geolocation;
using SmartField.Application.IntegrationOutbox;
using SmartField.Application.Projects;
using SmartField.Application.WorkSites;
using SmartField.Infrastructure.Attendance;
using SmartField.Infrastructure.Employees;
using SmartField.Infrastructure.Geolocation;
using SmartField.Infrastructure.Outbox;
using SmartField.Infrastructure.Projects;
using SmartField.Infrastructure.WorkSites;

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
        services.AddScoped<IAttendanceStore, AttendanceStore>();
        services.AddScoped<IEmployeeStore, EmployeeStore>();
        services.AddScoped<IGeolocationStore, GeolocationStore>();
        services.AddScoped<IIntegrationOutboxStore, IntegrationOutboxStore>();
        services.AddScoped<IProjectStore, ProjectStore>();
        services.AddScoped<IWorkSiteStore, WorkSiteStore>();

        return services;
    }
}
