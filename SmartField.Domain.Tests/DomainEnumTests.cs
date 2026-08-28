using SmartField.Domain.Enums;

namespace SmartField.Domain.Tests;

public class DomainEnumTests
{
    [Fact]
    public void DomainAssembly_ContainsPlannerRequestedEnums()
    {
        var expectedEnumTypes = new[]
        {
            typeof(IntegrationStatus),
            typeof(ProjectStatus),
            typeof(ProjectType),
            typeof(GeofenceMode),
            typeof(AttendanceSource),
            typeof(AttendanceEventType)
        };

        Assert.All(expectedEnumTypes, type =>
        {
            Assert.True(type.IsEnum);
            Assert.Equal("SmartField.Domain.Enums", type.Namespace);
        });
    }

    [Fact]
    public void AttendanceEventType_ContainsExpectedValues()
    {
        var expectedValues = new[]
        {
            AttendanceEventType.ClockIn,
            AttendanceEventType.BreakStart,
            AttendanceEventType.BreakEnd,
            AttendanceEventType.ClockOut
        };

        Assert.Equal(expectedValues, Enum.GetValues<AttendanceEventType>());
    }

    [Fact]
    public void AttendanceSource_ContainsExpectedValues()
    {
        var expectedValues = new[]
        {
            AttendanceSource.PWA,
            AttendanceSource.Backoffice,
            AttendanceSource.Import,
            AttendanceSource.Primavera,
            AttendanceSource.API
        };

        Assert.Equal(expectedValues, Enum.GetValues<AttendanceSource>());
    }

    [Fact]
    public void GeofenceMode_ContainsExpectedValues()
    {
        var expectedValues = new[]
        {
            GeofenceMode.Disabled,
            GeofenceMode.Warning,
            GeofenceMode.Block
        };

        Assert.Equal(expectedValues, Enum.GetValues<GeofenceMode>());
    }

    [Fact]
    public void IntegrationStatus_ContainsExpectedValues()
    {
        var expectedValues = new[]
        {
            IntegrationStatus.Pending,
            IntegrationStatus.Processing,
            IntegrationStatus.Completed,
            IntegrationStatus.Failed
        };

        Assert.Equal(expectedValues, Enum.GetValues<IntegrationStatus>());
    }

    [Fact]
    public void ProjectStatus_ContainsExpectedValues()
    {
        var expectedValues = new[]
        {
            ProjectStatus.Draft,
            ProjectStatus.Active,
            ProjectStatus.Closed,
            ProjectStatus.Cancelled
        };

        Assert.Equal(expectedValues, Enum.GetValues<ProjectStatus>());
    }

    [Fact]
    public void ProjectType_ContainsExpectedValues()
    {
        var expectedValues = new[]
        {
            ProjectType.Construction,
            ProjectType.Maintenance,
            ProjectType.Intervention,
            ProjectType.Internal,
            ProjectType.Other
        };

        Assert.Equal(expectedValues, Enum.GetValues<ProjectType>());
    }
}
