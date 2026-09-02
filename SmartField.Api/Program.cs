using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using SmartField.Api.Authentication;
using SmartField.Api.HealthChecks;
using SmartField.Api.Middleware;
using SmartField.Application.Abstractions;
using SmartField.Application.Attendance;
using SmartField.Application.Audit;
using SmartField.Application.Employees;
using SmartField.Application.Geolocation;
using SmartField.Application.IntegrationOutbox;
using SmartField.Application.Projects;
using SmartField.Application.WorkSites;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Persistence;
using SmartField.Integrations.Primavera;

var builder = WebApplication.CreateBuilder(args);
var jwtSigningKey = JwtSigningKey.Create(builder.Configuration, builder.Environment);
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Host.UseSerilog((_, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            "logs/smartfield-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            shared: true);
});

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton(
    builder.Configuration
        .GetSection(PrimaveraOptions.SectionName)
        .Get<PrimaveraOptions>() ?? new PrimaveraOptions());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(jwtSigningKey);
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPrimaveraClient, NotConfiguredPrimaveraClient>();
builder.Services.AddScoped<IEmployeeIntegrationService, PrimaveraEmployeeIntegrationService>();
builder.Services.AddScoped<IAttendanceIntegrationService, PrimaveraAttendanceIntegrationService>();
builder.Services.AddScoped<IProjectIntegrationService, PrimaveraProjectIntegrationService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IGeolocationService, GeolocationService>();
builder.Services.AddScoped<IIntegrationOutboxService, IntegrationOutboxService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IWorkSiteService, WorkSiteService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCompanyProvider, HttpCurrentCompanyProvider>();
builder.Services.AddScoped<ICurrentUserProvider, HttpCurrentUserProvider>();
builder.Services.AddSqlServerPersistence(builder.Configuration);
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<SmartFieldDbContext>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSigningKey.SecurityKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SmartFieldPolicies.Backoffice, policy =>
        policy.RequireRole(SmartFieldRoles.Admin, SmartFieldRoles.Manager));
});
builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sql_server");
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await app.SeedDevelopmentIdentityAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set(
            "CorrelationId",
            CorrelationIdMiddleware.GetCorrelationId(httpContext));
    };
});

app.UseHttpsRedirection();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (
    HealthCheckService healthCheckService,
    CancellationToken cancellationToken) =>
{
    var report = await healthCheckService.CheckHealthAsync(cancellationToken);
    var statusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;

    return Results.Text(
        report.Status.ToString(),
        "text/plain",
        statusCode: statusCode);
})
    .AllowAnonymous()
    .WithName("Health")
    .WithSummary("Estado da API SmartField")
    .WithDescription("Executa os health checks configurados, incluindo SQL Server.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain")
    .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");
app.MapControllers();

app.Run();
