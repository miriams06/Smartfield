using SmartField.Application.Abstractions;
using SmartField.Application.IntegrationOutbox;
using SmartField.Application.Projects;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using DomainIntegrationOutbox = SmartField.Domain.Entities.IntegrationOutbox;

namespace SmartField.Application.Tests;

public class ProjectServiceTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");
    private static readonly Guid WorkSiteId =
        Guid.Parse("cb9ed2c6-69e8-4b85-9ea5-52b496a31f11");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_UsesAuthenticatedCompanyAndNormalizesSearch()
    {
        var store = new FakeProjectStore();
        var service = CreateService(store);

        var result = await service.SearchAsync("  Obra  ", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyId, store.LastCompanyId);
        Assert.Equal("Obra", store.LastSearch);
    }

    [Fact]
    public async Task CreateAsync_NormalizesValuesAndStoresErpCodesAndWorkSite()
    {
        var store = new FakeProjectStore();
        var service = CreateService(store);

        var result = await service.CreateAsync(
            new CreateProjectRequest(
                "  PRJ001  ",
                "  Obra Lisboa  ",
                "Construction",
                "Active",
                "  Cliente A  ",
                WorkSiteId,
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 12, 31),
                "  ERP-PRJ  ",
                "  CC-001  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var addedProject = Assert.IsType<Project>(store.AddedProject);
        Assert.Equal(CompanyId, addedProject.CompanyId);
        Assert.Equal("PRJ001", addedProject.Code);
        Assert.Equal("Obra Lisboa", addedProject.Name);
        Assert.Equal(ProjectType.Construction, addedProject.ProjectType);
        Assert.Equal(ProjectStatus.Active, addedProject.Status);
        Assert.Equal("Cliente A", addedProject.CustomerName);
        Assert.Equal(WorkSiteId, addedProject.WorkSiteId);
        Assert.Equal("ERP-PRJ", addedProject.ErpProjectCode);
        Assert.Equal("CC-001", addedProject.ErpCostCenterCode);
        Assert.Equal(Now, addedProject.CreatedAtUtc);
        var outbox = Assert.Single(store.OutboxItems);
        Assert.Equal("ProjectCreated", outbox.EventType);
        Assert.Equal("Project", outbox.EntityType);
        Assert.Equal(addedProject.Id, outbox.EntityId);
        Assert.Contains(addedProject.Code, outbox.Payload);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationForInvalidEnumsAndDates()
    {
        var store = new FakeProjectStore();
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest() with
            {
                ProjectType = "Invalid",
                Status = "Invalid",
                StartDate = new DateOnly(2026, 12, 31),
                EndDate = new DateOnly(2026, 9, 1)
            },
            CancellationToken.None);

        Assert.Equal(ProjectError.Validation, result.Error);
        Assert.Contains(nameof(CreateProjectRequest.ProjectType), result.ValidationErrors.Keys);
        Assert.Contains(nameof(CreateProjectRequest.Status), result.ValidationErrors.Keys);
        Assert.Contains(nameof(CreateProjectRequest.EndDate), result.ValidationErrors.Keys);
        Assert.Null(store.AddedProject);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflictForDuplicateCode()
    {
        var store = new FakeProjectStore
        {
            CodeExists = true
        };
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest(),
            CancellationToken.None);

        Assert.Equal(ProjectError.CodeConflict, result.Error);
        Assert.Null(store.AddedProject);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNotFoundForForeignWorkSite()
    {
        var store = new FakeProjectStore
        {
            WorkSiteExists = false
        };
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest() with { WorkSiteId = WorkSiteId },
            CancellationToken.None);

        Assert.Equal(ProjectError.WorkSiteNotFound, result.Error);
        Assert.Null(store.AddedProject);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesStatusWorkSiteAndErpCodes()
    {
        var store = new FakeProjectStore();
        var service = CreateService(store);
        var existingProject = Assert.IsType<Project>(store.ExistingProject);

        var result = await service.UpdateAsync(
            existingProject.Id,
            ValidUpdateRequest() with
            {
                Status = "Closed",
                WorkSiteId = WorkSiteId,
                ErpProjectCode = "  ERP-CLOSED  ",
                ErpCostCenterCode = "  CC-CLOSED  "
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectStatus.Closed, existingProject.Status);
        Assert.Equal(WorkSiteId, existingProject.WorkSiteId);
        Assert.Equal("ERP-CLOSED", existingProject.ErpProjectCode);
        Assert.Equal("CC-CLOSED", existingProject.ErpCostCenterCode);
        Assert.Equal(Now, existingProject.UpdatedAtUtc);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundWhenProjectDoesNotExist()
    {
        var store = new FakeProjectStore
        {
            ExistingProject = null
        };
        var service = CreateService(store);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            ValidUpdateRequest(),
            CancellationToken.None);

        Assert.Equal(ProjectError.NotFound, result.Error);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCompanyUnavailableWithoutAuthenticatedCompany()
    {
        var store = new FakeProjectStore();
        var service = new ProjectService(
            store,
            new FakeCurrentCompanyProvider(null),
            new IntegrationOutboxService(store),
            new FixedTimeProvider(Now));

        var result = await service.CreateAsync(
            ValidCreateRequest(),
            CancellationToken.None);

        Assert.Equal(ProjectError.CompanyUnavailable, result.Error);
        Assert.Null(store.AddedProject);
    }

    private static ProjectService CreateService(FakeProjectStore store)
    {
        return new ProjectService(
            store,
            new FakeCurrentCompanyProvider(CompanyId),
            new IntegrationOutboxService(store),
            new FixedTimeProvider(Now));
    }

    private static CreateProjectRequest ValidCreateRequest()
    {
        return new CreateProjectRequest(
            "PRJ001",
            "Obra Lisboa",
            "Construction",
            "Active",
            "Cliente A",
            WorkSiteId,
            new DateOnly(2026, 9, 1),
            null,
            "ERP-PRJ",
            "CC-001");
    }

    private static UpdateProjectRequest ValidUpdateRequest()
    {
        return new UpdateProjectRequest(
            "PRJ001",
            "Obra Lisboa",
            "Construction",
            "Active",
            "Cliente A",
            WorkSiteId,
            new DateOnly(2026, 9, 1),
            null,
            "ERP-PRJ",
            "CC-001");
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

    private sealed class FakeProjectStore : IProjectStore, IIntegrationOutboxStore
    {
        public bool CodeExists { get; set; }

        public bool WorkSiteExists { get; set; } = true;

        public Project? ExistingProject { get; set; } = NewProject();

        public Guid? LastCompanyId { get; private set; }

        public string? LastSearch { get; private set; }

        public Project? AddedProject { get; private set; }

        public List<DomainIntegrationOutbox> OutboxItems { get; } = [];

        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<ProjectDto>> SearchAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            LastSearch = search;
            return Task.FromResult<IReadOnlyList<ProjectDto>>([]);
        }

        public Task<ProjectDto?> GetAsync(
            Guid companyId,
            Guid projectId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            var entity = AddedProject ?? ExistingProject;
            return Task.FromResult(entity is null ? null : ToDto(entity));
        }

        public Task<Project?> FindEntityAsync(
            Guid companyId,
            Guid projectId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(ExistingProject);
        }

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? projectIdToExclude,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(CodeExists);
        }

        public Task<bool> WorkSiteExistsAsync(
            Guid companyId,
            Guid workSiteId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(WorkSiteExists);
        }

        public void Add(Project project)
        {
            AddedProject = project;
        }

        public void Add(DomainIntegrationOutbox integrationOutbox)
        {
            OutboxItems.Add(integrationOutbox);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        private static Project NewProject()
        {
            return new Project
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                Code = "PRJ001",
                Name = "Obra Lisboa",
                ProjectType = ProjectType.Construction,
                Status = ProjectStatus.Active,
                CreatedAtUtc = Now
            };
        }

        private static ProjectDto ToDto(Project project)
        {
            return new ProjectDto(
                project.Id,
                project.Code,
                project.Name,
                project.ProjectType.ToString(),
                project.Status.ToString(),
                project.CustomerName,
                project.WorkSiteId,
                null,
                project.StartDate,
                project.EndDate,
                project.ErpProjectCode,
                project.ErpCostCenterCode,
                project.CreatedAtUtc,
                project.UpdatedAtUtc);
        }
    }
}
