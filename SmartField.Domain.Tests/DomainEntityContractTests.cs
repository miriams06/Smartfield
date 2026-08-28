using System.Reflection;
using SmartField.Domain.Entities;

namespace SmartField.Domain.Tests;

public class DomainEntityContractTests
{
    private static readonly Type[] EntitiesWithId =
    [
        typeof(IntegrationOutbox),
        typeof(ExternalReference),
        typeof(AuditLog),
        typeof(AttendanceCorrection),
        typeof(AttendanceEvent),
        typeof(Project),
        typeof(WorkSite),
        typeof(Employee),
        typeof(Company)
    ];

    private static readonly Type[] MultiCompanyEntities =
    [
        typeof(IntegrationOutbox),
        typeof(ExternalReference),
        typeof(AuditLog),
        typeof(AttendanceCorrection),
        typeof(AttendanceEvent),
        typeof(Project),
        typeof(WorkSite),
        typeof(Employee),
        typeof(CompanySettings)
    ];

    private static readonly Type[] EntitiesWithCreatedAtUtc =
    [
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
    ];

    private static readonly Type[] EntitiesWithUpdatedAtUtc =
    [
        typeof(ExternalReference),
        typeof(Project),
        typeof(WorkSite),
        typeof(Employee),
        typeof(CompanySettings),
        typeof(Company)
    ];

    [Fact]
    public void Entities_UseGuidAsPrimaryIdentifier()
    {
        Assert.All(EntitiesWithId, entityType =>
        {
            var property = RequireProperty(entityType, "Id");

            Assert.Equal(typeof(Guid), property.PropertyType);
        });

        Assert.Equal(typeof(Guid), RequireProperty(typeof(CompanySettings), "CompanyId").PropertyType);
    }

    [Fact]
    public void MultiCompanyEntities_HaveCompanyId()
    {
        Assert.All(MultiCompanyEntities, entityType =>
        {
            var property = RequireProperty(entityType, "CompanyId");

            Assert.Equal(typeof(Guid), property.PropertyType);
        });
    }

    [Fact]
    public void Entities_HaveCreatedAtUtc()
    {
        Assert.All(EntitiesWithCreatedAtUtc, entityType =>
        {
            var property = RequireProperty(entityType, "CreatedAtUtc");

            Assert.Equal(typeof(DateTimeOffset), property.PropertyType);
        });
    }

    [Fact]
    public void ApplicableEntities_HaveNullableUpdatedAtUtc()
    {
        Assert.All(EntitiesWithUpdatedAtUtc, entityType =>
        {
            var property = RequireProperty(entityType, "UpdatedAtUtc");

            Assert.Equal(typeof(DateTimeOffset?), property.PropertyType);
        });
    }

    [Fact]
    public void EmployeeWorkSiteAndProject_HaveNullableExternalReferenceFields()
    {
        var entityTypes = new[] { typeof(Employee), typeof(WorkSite), typeof(Project) };

        Assert.All(entityTypes, entityType =>
        {
            AssertNullableReferenceProperty(entityType, "ExternalSystem");
            AssertNullableReferenceProperty(entityType, "ExternalId");
        });
    }

    [Fact]
    public void ExternalReference_HasExternalMappingFields()
    {
        Assert.Equal(typeof(string), RequireProperty(typeof(ExternalReference), "SystemName").PropertyType);
        Assert.Equal(typeof(string), RequireProperty(typeof(ExternalReference), "EntityType").PropertyType);
        Assert.Equal(typeof(Guid), RequireProperty(typeof(ExternalReference), "LocalEntityId").PropertyType);
        Assert.Equal(typeof(string), RequireProperty(typeof(ExternalReference), "ExternalEntityId").PropertyType);
        AssertNullableReferenceProperty(typeof(ExternalReference), "ExternalCode");
    }

    [Fact]
    public void ErpFields_AreNullable()
    {
        AssertNullableReferenceProperty(typeof(Employee), "ErpEmployeeCode");
        AssertNullableReferenceProperty(typeof(WorkSite), "ErpCostCenterCode");
        AssertNullableReferenceProperty(typeof(Project), "ErpProjectCode");
        AssertNullableReferenceProperty(typeof(Project), "ErpCostCenterCode");
    }

    private static PropertyInfo RequireProperty(Type type, string propertyName)
    {
        return type.GetProperty(propertyName)
            ?? throw new InvalidOperationException($"{type.Name}.{propertyName} was not found.");
    }

    private static void AssertNullableReferenceProperty(Type type, string propertyName)
    {
        var property = RequireProperty(type, propertyName);
        var nullability = new NullabilityInfoContext().Create(property);

        Assert.Equal(typeof(string), property.PropertyType);
        Assert.Equal(NullabilityState.Nullable, nullability.ReadState);
    }
}
