using SmartField.Application.Abstractions;
using SmartField.Application.Geolocation;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Tests;

public class GeofenceSettingsServiceTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");

    private static readonly DateTimeOffset Now =
        new(2026, 9, 3, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_ReturnsExistingCompanySettings()
    {
        var store = new FakeGeofenceSettingsStore
        {
            Settings = new CompanySettings
            {
                CompanyId = CompanyId,
                RequireGeolocation = true,
                GeofenceMode = GeofenceMode.Warning,
                DefaultGeofenceRadiusMeters = 150,
                CreatedAtUtc = Now
            }
        };
        var service = CreateService(store);

        var result = await service.GetAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.RequireGeolocation);
        Assert.Equal(GeofenceMode.Warning, result.Value?.GeofenceMode);
        Assert.Equal(150, result.Value?.DefaultGeofenceRadiusMeters);
        Assert.Equal(0, store.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingCompanySettings()
    {
        var store = new FakeGeofenceSettingsStore
        {
            Settings = new CompanySettings
            {
                CompanyId = CompanyId,
                RequireGeolocation = false,
                GeofenceMode = GeofenceMode.Disabled,
                DefaultGeofenceRadiusMeters = 100,
                CreatedAtUtc = Now.AddDays(-1)
            }
        };
        var service = CreateService(store);

        var result = await service.UpdateAsync(
            new UpdateGeofenceSettingsRequest(true, GeofenceMode.Block, 250),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(store.Settings.RequireGeolocation);
        Assert.Equal(GeofenceMode.Block, store.Settings.GeofenceMode);
        Assert.Equal(250, store.Settings.DefaultGeofenceRadiusMeters);
        Assert.Equal(Now, store.Settings.UpdatedAtUtc);
        Assert.Equal(1, store.SaveChangesCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public async Task UpdateAsync_RejectsInvalidDefaultRadius(int radius)
    {
        var store = new FakeGeofenceSettingsStore();
        var service = CreateService(store);

        var result = await service.UpdateAsync(
            new UpdateGeofenceSettingsRequest(false, GeofenceMode.Warning, radius),
            CancellationToken.None);

        Assert.Equal(GeolocationError.Validation, result.Error);
        Assert.True(result.ValidationErrors.ContainsKey(nameof(UpdateGeofenceSettingsRequest.DefaultGeofenceRadiusMeters)));
        Assert.Equal(0, store.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsCompanyUnavailableWithoutCompany()
    {
        var service = new GeofenceSettingsService(
            new FakeGeofenceSettingsStore(),
            new FakeCurrentCompanyProvider(null),
            new FixedTimeProvider(Now));

        var result = await service.UpdateAsync(
            new UpdateGeofenceSettingsRequest(false, GeofenceMode.Warning, 100),
            CancellationToken.None);

        Assert.Equal(GeolocationError.CompanyUnavailable, result.Error);
    }

    private static GeofenceSettingsService CreateService(
        FakeGeofenceSettingsStore store)
    {
        return new GeofenceSettingsService(
            store,
            new FakeCurrentCompanyProvider(CompanyId),
            new FixedTimeProvider(Now));
    }

    private sealed class FakeGeofenceSettingsStore : IGeofenceSettingsStore
    {
        public CompanySettings Settings { get; set; } = new()
        {
            CompanyId = CompanyId,
            RequireGeolocation = false,
            GeofenceMode = GeofenceMode.Disabled,
            DefaultGeofenceRadiusMeters = 100,
            CreatedAtUtc = Now
        };

        public int SaveChangesCalls { get; private set; }

        public Task<CompanySettings?> FindAsync(
            Guid companyId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<CompanySettings?>(
                companyId == CompanyId ? Settings : null);
        }

        public void Add(CompanySettings settings)
        {
            Settings = settings;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public FakeCurrentCompanyProvider(Guid? companyId)
        {
            CompanyId = companyId;
        }

        public Guid? CompanyId { get; }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
