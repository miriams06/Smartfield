using SmartField.Application.Abstractions;
using SmartField.Application.WorkSites;
using SmartField.Domain.Entities;

namespace SmartField.Application.Tests;

public class WorkSiteServiceTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");

    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_UsesAuthenticatedCompanyAndNormalizesSearch()
    {
        var store = new FakeWorkSiteStore();
        var service = CreateService(store);

        var result = await service.SearchAsync("  Lisboa  ", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyId, store.LastCompanyId);
        Assert.Equal("Lisboa", store.LastSearch);
    }

    [Fact]
    public async Task CreateAsync_NormalizesValuesAndAllowsEmptyErpCostCenterCode()
    {
        var store = new FakeWorkSiteStore();
        var service = CreateService(store);

        var result = await service.CreateAsync(
            new CreateWorkSiteRequest(
                "  OBRA001  ",
                "  Obra Lisboa  ",
                "  Rua Principal  ",
                38.722252m,
                -9.139337m,
                150,
                true,
                "   "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var addedWorkSite = Assert.IsType<WorkSite>(store.AddedWorkSite);
        Assert.Equal(CompanyId, addedWorkSite.CompanyId);
        Assert.Equal("OBRA001", addedWorkSite.Code);
        Assert.Equal("Obra Lisboa", addedWorkSite.Name);
        Assert.Equal("Rua Principal", addedWorkSite.Address);
        Assert.Equal(38.722252m, addedWorkSite.Latitude);
        Assert.Equal(-9.139337m, addedWorkSite.Longitude);
        Assert.Equal(150, addedWorkSite.GeofenceRadiusMeters);
        Assert.Null(addedWorkSite.ErpCostCenterCode);
        Assert.Equal(Now, addedWorkSite.CreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflictForDuplicateCode()
    {
        var store = new FakeWorkSiteStore
        {
            CodeExists = true
        };
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest(),
            CancellationToken.None);

        Assert.Equal(WorkSiteError.CodeConflict, result.Error);
        Assert.Null(store.AddedWorkSite);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationErrorForInvalidCoordinatesAndRadius()
    {
        var store = new FakeWorkSiteStore();
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest() with
            {
                Latitude = 91,
                Longitude = -181,
                GeofenceRadiusMeters = 0
            },
            CancellationToken.None);

        Assert.Equal(WorkSiteError.Validation, result.Error);
        Assert.True(result.ValidationErrors.ContainsKey(nameof(CreateWorkSiteRequest.Latitude)));
        Assert.True(result.ValidationErrors.ContainsKey(nameof(CreateWorkSiteRequest.Longitude)));
        Assert.True(result.ValidationErrors.ContainsKey(nameof(CreateWorkSiteRequest.GeofenceRadiusMeters)));
        Assert.Null(store.AddedWorkSite);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_MapsConcurrentCodeConflict()
    {
        var store = new FakeWorkSiteStore
        {
            ThrowCodeConflict = true
        };
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest(),
            CancellationToken.None);

        Assert.Equal(WorkSiteError.CodeConflict, result.Error);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesLocationStatusAndTimestamp()
    {
        var store = new FakeWorkSiteStore();
        var service = CreateService(store);
        var existingWorkSite = Assert.IsType<WorkSite>(store.ExistingWorkSite);

        var result = await service.UpdateAsync(
            existingWorkSite.Id,
            ValidUpdateRequest() with
            {
                Name = "  Obra Norte  ",
                Latitude = 41.149610m,
                Longitude = -8.610990m,
                GeofenceRadiusMeters = 200,
                IsActive = false,
                ErpCostCenterCode = "  CC-27  "
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Obra Norte", existingWorkSite.Name);
        Assert.Equal(41.149610m, existingWorkSite.Latitude);
        Assert.Equal(-8.610990m, existingWorkSite.Longitude);
        Assert.Equal(200, existingWorkSite.GeofenceRadiusMeters);
        Assert.False(existingWorkSite.IsActive);
        Assert.Equal("CC-27", existingWorkSite.ErpCostCenterCode);
        Assert.Equal(Now, existingWorkSite.UpdatedAtUtc);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundWhenWorkSiteDoesNotExist()
    {
        var store = new FakeWorkSiteStore
        {
            ExistingWorkSite = null
        };
        var service = CreateService(store);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            ValidUpdateRequest(),
            CancellationToken.None);

        Assert.Equal(WorkSiteError.NotFound, result.Error);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCompanyUnavailableWithoutAuthenticatedCompany()
    {
        var store = new FakeWorkSiteStore();
        var service = new WorkSiteService(
            store,
            new FakeCurrentCompanyProvider(null),
            new FixedTimeProvider(Now));

        var result = await service.CreateAsync(
            ValidCreateRequest(),
            CancellationToken.None);

        Assert.Equal(WorkSiteError.CompanyUnavailable, result.Error);
        Assert.Null(store.AddedWorkSite);
    }

    private static WorkSiteService CreateService(FakeWorkSiteStore store)
    {
        return new WorkSiteService(
            store,
            new FakeCurrentCompanyProvider(CompanyId),
            new FixedTimeProvider(Now));
    }

    private static CreateWorkSiteRequest ValidCreateRequest()
    {
        return new CreateWorkSiteRequest(
            "OBRA001",
            "Obra Lisboa",
            "Rua Principal",
            38.722252m,
            -9.139337m,
            150,
            true,
            null);
    }

    private static UpdateWorkSiteRequest ValidUpdateRequest()
    {
        return new UpdateWorkSiteRequest(
            "OBRA001",
            "Obra Lisboa",
            "Rua Principal",
            38.722252m,
            -9.139337m,
            150,
            true,
            null);
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

    private sealed class FakeWorkSiteStore : IWorkSiteStore
    {
        public bool CodeExists { get; set; }

        public WorkSite? ExistingWorkSite { get; set; } = NewWorkSite();

        public Guid? LastCompanyId { get; private set; }

        public string? LastSearch { get; private set; }

        public WorkSite? AddedWorkSite { get; private set; }

        public int SaveCount { get; private set; }

        public bool ThrowCodeConflict { get; set; }

        public Task<IReadOnlyList<WorkSiteDto>> SearchAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            LastSearch = search;
            return Task.FromResult<IReadOnlyList<WorkSiteDto>>([]);
        }

        public Task<WorkSiteDto?> GetAsync(
            Guid companyId,
            Guid workSiteId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            var entity = AddedWorkSite ?? ExistingWorkSite;
            return Task.FromResult(entity is null ? null : ToDto(entity));
        }

        public Task<WorkSite?> FindEntityAsync(
            Guid companyId,
            Guid workSiteId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(ExistingWorkSite);
        }

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? workSiteIdToExclude,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(CodeExists);
        }

        public void Add(WorkSite workSite)
        {
            AddedWorkSite = workSite;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;

            if (ThrowCodeConflict)
            {
                throw new WorkSiteCodeConflictException(
                    "Conflito simulado.",
                    new InvalidOperationException("Conflito simulado."));
            }

            return Task.CompletedTask;
        }

        private static WorkSite NewWorkSite()
        {
            return new WorkSite
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                Code = "OBRA001",
                Name = "Obra Lisboa",
                IsActive = true,
                CreatedAtUtc = Now
            };
        }

        private static WorkSiteDto ToDto(WorkSite workSite)
        {
            return new WorkSiteDto(
                workSite.Id,
                workSite.Code,
                workSite.Name,
                workSite.Address,
                workSite.Latitude,
                workSite.Longitude,
                workSite.GeofenceRadiusMeters,
                workSite.IsActive,
                workSite.ErpCostCenterCode,
                workSite.CreatedAtUtc,
                workSite.UpdatedAtUtc);
        }
    }
}
