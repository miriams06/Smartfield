using System.Reflection;
using SmartField.Domain.Entities;

namespace SmartField.Domain.Tests;

public class DomainModelTests
{
    [Fact]
    public void DomainAssembly_DoesNotReferenceForbiddenInfrastructureAssemblies()
    {
        var forbiddenAssemblyNames = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore.Components",
            "Microsoft.Data.SqlClient",
            "System.Data.SqlClient",
            "Primavera"
        };

        var referencedAssemblyNames = typeof(Company)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            referencedAssemblyNames,
            assemblyName => forbiddenAssemblyNames.Any(forbidden =>
                assemblyName.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void DomainAssembly_ContainsPlannerRequestedEntities()
    {
        var expectedEntityTypes = new[]
        {
            typeof(IntegrationOutbox),
            typeof(ExternalReference),
            typeof(AuditLog),
            typeof(AttendanceCorrection),
            typeof(AttendanceEvent),
            typeof(Project),
            typeof(WorkSite),
            typeof(Employee),
            typeof(CompanySettings),
            typeof(Company)
        };

        Assert.All(expectedEntityTypes, type =>
        {
            Assert.True(type.IsClass);
            Assert.Equal("SmartField.Domain.Entities", type.Namespace);
        });
    }

    [Fact]
    public void Company_DefaultTimeZone_IsEuropeLisbon()
    {
        var company = new Company();

        Assert.Equal("Europe/Lisbon", company.TimeZone);
    }
}
