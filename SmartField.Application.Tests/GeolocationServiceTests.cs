using SmartField.Application.Abstractions;
using SmartField.Application.Geolocation;
using SmartField.Domain.Enums;

namespace SmartField.Application.Tests;

public class GeolocationServiceTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");

    private static readonly Guid WorkSiteId =
        Guid.Parse("cb9ed2c6-69e8-4b85-9ea5-52b496a31f11");

    [Fact]
    public async Task ValidateAsync_DisabledAcceptsWithoutLocationOrWorkSite()
    {
        var store = new FakeGeolocationStore
        {
            Reference = new GeofenceValidationReference(
                false,
                GeofenceMode.Disabled,
                100,
                null)
        };
        var service = CreateService(store);

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.IsAccepted);
        Assert.Null(result.Value?.IsInsideGeofence);
        Assert.Null(result.Value?.DistanceFromWorkSiteMeters);
        Assert.Equal("GeofenceDisabled", result.Value?.ResultCode);
        Assert.Equal(CompanyId, store.LastCompanyId);
    }

    [Fact]
    public async Task ValidateAsync_CalculatesDistanceAndAcceptsInsideRadius()
    {
        var store = new FakeGeolocationStore();
        var service = CreateService(store);

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(
                38.722300m,
                -9.139300m,
                12.5m,
                WorkSiteId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.IsAccepted);
        Assert.True(result.Value?.IsInsideGeofence);
        Assert.InRange(result.Value!.DistanceFromWorkSiteMeters!.Value, 0, 10);
        Assert.Equal("InsideGeofence", result.Value.ResultCode);
    }

    [Fact]
    public async Task ValidateAsync_WarningAcceptsAndMarksOutsideGeofence()
    {
        var store = new FakeGeolocationStore
        {
            Reference = CreateReference(GeofenceMode.Warning)
        };
        var service = CreateService(store);

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(
                41.149610m,
                -8.610990m,
                20,
                WorkSiteId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.IsAccepted);
        Assert.False(result.Value?.IsInsideGeofence);
        Assert.True(result.Value?.DistanceFromWorkSiteMeters > 250000);
        Assert.Equal("OutsideGeofenceWarning", result.Value?.ResultCode);
    }

    [Fact]
    public async Task ValidateAsync_BlockRejectsOutsideGeofence()
    {
        var store = new FakeGeolocationStore
        {
            Reference = CreateReference(GeofenceMode.Block)
        };
        var service = CreateService(store);

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(
                41.149610m,
                -8.610990m,
                20,
                WorkSiteId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value?.IsAccepted);
        Assert.False(result.Value?.IsInsideGeofence);
        Assert.True(result.Value?.DistanceFromWorkSiteMeters > 250000);
        Assert.Equal("OutsideGeofenceBlocked", result.Value?.ResultCode);
    }

    [Fact]
    public async Task ValidateAsync_BlockRejectsWhenGpsIsUnavailable()
    {
        var store = new FakeGeolocationStore
        {
            Reference = CreateReference(GeofenceMode.Block)
        };
        var service = CreateService(store);

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(null, null, null, WorkSiteId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value?.IsAccepted);
        Assert.False(result.Value?.IsInsideGeofence);
        Assert.Null(result.Value?.DistanceFromWorkSiteMeters);
        Assert.Equal("LocationUnavailableBlocked", result.Value?.ResultCode);
    }

    [Fact]
    public async Task ValidateAsync_RequireGeolocationRejectsWithoutLocation()
    {
        var store = new FakeGeolocationStore
        {
            Reference = CreateReference(GeofenceMode.Disabled) with
            {
                RequireGeolocation = true
            }
        };
        var service = CreateService(store);

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value?.IsAccepted);
        Assert.Equal("LocationRequired", result.Value?.ResultCode);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsValidationForInvalidCoordinates()
    {
        var store = new FakeGeolocationStore();
        var service = CreateService(store);

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(
                91,
                -181,
                -1,
                WorkSiteId),
            CancellationToken.None);

        Assert.Equal(GeolocationError.Validation, result.Error);
        Assert.True(result.ValidationErrors.ContainsKey(nameof(GeolocationValidationRequest.Latitude)));
        Assert.True(result.ValidationErrors.ContainsKey(nameof(GeolocationValidationRequest.Longitude)));
        Assert.True(result.ValidationErrors.ContainsKey(nameof(GeolocationValidationRequest.AccuracyMeters)));
        Assert.Null(store.LastCompanyId);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsWorkSiteNotFoundForAnotherCompany()
    {
        var store = new FakeGeolocationStore
        {
            Reference = null
        };
        var service = CreateService(store);

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(
                38.722300m,
                -9.139300m,
                10,
                WorkSiteId),
            CancellationToken.None);

        Assert.Equal(GeolocationError.WorkSiteNotFound, result.Error);
        Assert.Equal(CompanyId, store.LastCompanyId);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsCompanyUnavailableWithoutAuthenticatedCompany()
    {
        var store = new FakeGeolocationStore();
        var service = new GeolocationService(
            store,
            new FakeCurrentCompanyProvider(null));

        var result = await service.ValidateAsync(
            new GeolocationValidationRequest(
                38.722300m,
                -9.139300m,
                10,
                WorkSiteId),
            CancellationToken.None);

        Assert.Equal(GeolocationError.CompanyUnavailable, result.Error);
        Assert.Null(store.LastCompanyId);
    }

    private static GeolocationService CreateService(FakeGeolocationStore store)
    {
        return new GeolocationService(
            store,
            new FakeCurrentCompanyProvider(CompanyId));
    }

    private static GeofenceValidationReference CreateReference(GeofenceMode geofenceMode)
    {
        return new GeofenceValidationReference(
            false,
            geofenceMode,
            100,
            new WorkSiteGeofenceReference(
                WorkSiteId,
                38.722252m,
                -9.139337m,
                null));
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public FakeCurrentCompanyProvider(Guid? companyId)
        {
            CompanyId = companyId;
        }

        public Guid? CompanyId { get; }
    }

    private sealed class FakeGeolocationStore : IGeolocationStore
    {
        public GeofenceValidationReference? Reference { get; set; } =
            CreateReference(GeofenceMode.Block);

        public Guid? LastCompanyId { get; private set; }

        public Guid? LastWorkSiteId { get; private set; }

        public Task<GeofenceValidationReference?> GetValidationReferenceAsync(
            Guid companyId,
            Guid? workSiteId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            LastWorkSiteId = workSiteId;
            return Task.FromResult(Reference);
        }
    }
}
