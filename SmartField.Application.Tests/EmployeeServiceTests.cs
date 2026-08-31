using SmartField.Application.Abstractions;
using SmartField.Application.Employees;
using SmartField.Domain.Entities;

namespace SmartField.Application.Tests;

public class EmployeeServiceTests
{
    private static readonly Guid CompanyId = Guid.Parse("f8352141-acde-432c-a43c-c982b8874593");
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_UsesAuthenticatedCompanyAndNormalizesSearch()
    {
        var repository = new FakeEmployeeStore();
        var service = CreateService(repository);

        var result = await service.SearchAsync("  Maria  ", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyId, repository.LastCompanyId);
        Assert.Equal("Maria", repository.LastSearch);
    }

    [Fact]
    public async Task CreateAsync_NormalizesValuesAndAllowsEmptyErpCode()
    {
        var repository = new FakeEmployeeStore();
        var service = CreateService(repository);
        var workSiteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await service.CreateAsync(
            new CreateEmployeeRequest(
                "  FUNC002  ",
                "  Maria Costa  ",
                "  maria@example.com  ",
                "  912345678  ",
                true,
                workSiteId,
                userId,
                "   "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var addedEmployee = Assert.IsType<Employee>(repository.AddedEmployee);
        Assert.Equal(CompanyId, addedEmployee.CompanyId);
        Assert.Equal("FUNC002", addedEmployee.EmployeeNumber);
        Assert.Equal("Maria Costa", addedEmployee.Name);
        Assert.Equal("maria@example.com", addedEmployee.Email);
        Assert.Equal("912345678", addedEmployee.MobilePhone);
        Assert.Null(addedEmployee.ErpEmployeeCode);
        Assert.Equal(workSiteId, addedEmployee.DefaultWorkSiteId);
        Assert.Equal(userId, repository.AssociatedUserId);
        Assert.Equal(Now, addedEmployee.CreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflictForDuplicateEmployeeNumber()
    {
        var repository = new FakeEmployeeStore
        {
            EmployeeNumberExists = true
        };
        var service = CreateService(repository);

        var result = await service.CreateAsync(
            ValidCreateRequest(),
            CancellationToken.None);

        Assert.Equal(EmployeeError.EmployeeNumberConflict, result.Error);
        Assert.Null(repository.AddedEmployee);
    }

    [Fact]
    public async Task CreateAsync_RejectsWorkSiteFromAnotherCompany()
    {
        var repository = new FakeEmployeeStore
        {
            WorkSiteCanBeAssigned = false
        };
        var service = CreateService(repository);

        var result = await service.CreateAsync(
            ValidCreateRequest() with { DefaultWorkSiteId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(EmployeeError.WorkSiteNotFound, result.Error);
        Assert.Null(repository.AddedEmployee);
    }

    [Fact]
    public async Task CreateAsync_RejectsUserAlreadyAssigned()
    {
        var repository = new FakeEmployeeStore
        {
            AssociationStatus = EmployeeUserAssociationStatus.UserAlreadyAssigned
        };
        var service = CreateService(repository);

        var result = await service.CreateAsync(
            ValidCreateRequest() with { UserId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(EmployeeError.UserAlreadyAssigned, result.Error);
        Assert.Null(repository.AddedEmployee);
    }

    [Fact]
    public async Task CreateAsync_RejectsUserOutsideCurrentCompany()
    {
        var store = new FakeEmployeeStore
        {
            AssociationStatus = EmployeeUserAssociationStatus.UserNotFound
        };
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest() with { UserId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(EmployeeError.UserNotFound, result.Error);
        Assert.Null(store.AddedEmployee);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationErrorForInvalidEmail()
    {
        var store = new FakeEmployeeStore();
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest() with { Email = "email-invalido" },
            CancellationToken.None);

        Assert.Equal(EmployeeError.Validation, result.Error);
        Assert.True(result.ValidationErrors.ContainsKey(nameof(CreateEmployeeRequest.Email)));
        Assert.Null(store.AddedEmployee);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_MapsConcurrentEmployeeNumberConflict()
    {
        var store = new FakeEmployeeStore
        {
            ThrowEmployeeNumberConflict = true
        };
        var service = CreateService(store);

        var result = await service.CreateAsync(
            ValidCreateRequest(),
            CancellationToken.None);

        Assert.Equal(EmployeeError.EmployeeNumberConflict, result.Error);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesStatusAssociationAndTimestamp()
    {
        var store = new FakeEmployeeStore();
        var service = CreateService(store);
        var userId = Guid.NewGuid();
        var existingEmployee = Assert.IsType<Employee>(store.ExistingEmployee);

        var result = await service.UpdateAsync(
            existingEmployee.Id,
            ValidUpdateRequest() with
            {
                Name = "  Maria Atualizada  ",
                IsActive = false,
                UserId = userId,
                ErpEmployeeCode = "  ERP-27  "
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(existingEmployee.IsActive);
        Assert.Equal("Maria Atualizada", existingEmployee.Name);
        Assert.Equal("ERP-27", existingEmployee.ErpEmployeeCode);
        Assert.Equal(Now, existingEmployee.UpdatedAtUtc);
        Assert.Equal(userId, store.AssociatedUserId);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundWhenEmployeeDoesNotExist()
    {
        var repository = new FakeEmployeeStore
        {
            ExistingEmployee = null
        };
        var service = CreateService(repository);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            ValidUpdateRequest(),
            CancellationToken.None);

        Assert.Equal(EmployeeError.NotFound, result.Error);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCompanyUnavailableWithoutAuthenticatedCompany()
    {
        var repository = new FakeEmployeeStore();
        var service = new EmployeeService(
            repository,
            new FakeCurrentCompanyProvider(null),
            new FixedTimeProvider(Now));

        var result = await service.CreateAsync(
            ValidCreateRequest(),
            CancellationToken.None);

        Assert.Equal(EmployeeError.CompanyUnavailable, result.Error);
        Assert.Null(repository.AddedEmployee);
    }

    [Fact]
    public async Task GetOptionsAsync_ValidatesEmployeeInCurrentCompany()
    {
        var repository = new FakeEmployeeStore
        {
            ExistingEmployee = null
        };
        var service = CreateService(repository);

        var result = await service.GetOptionsAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal(EmployeeError.NotFound, result.Error);
        Assert.Equal(CompanyId, repository.LastCompanyId);
    }

    private static EmployeeService CreateService(FakeEmployeeStore repository)
    {
        return new EmployeeService(
            repository,
            new FakeCurrentCompanyProvider(CompanyId),
            new FixedTimeProvider(Now));
    }

    private static CreateEmployeeRequest ValidCreateRequest()
    {
        return new CreateEmployeeRequest(
            "FUNC002",
            "Maria Costa",
            null,
            null,
            true,
            null,
            null,
            null);
    }

    private static UpdateEmployeeRequest ValidUpdateRequest()
    {
        return new UpdateEmployeeRequest(
            "FUNC002",
            "Maria Costa",
            null,
            null,
            true,
            null,
            null,
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

    private sealed class FakeEmployeeStore : IEmployeeStore
    {
        public bool EmployeeNumberExists { get; set; }

        public bool WorkSiteCanBeAssigned { get; set; } = true;

        public EmployeeUserAssociationStatus AssociationStatus { get; set; } =
            EmployeeUserAssociationStatus.Success;

        public Employee? ExistingEmployee { get; set; } = NewEmployee();

        public Guid? LastCompanyId { get; private set; }

        public string? LastSearch { get; private set; }

        public Employee? AddedEmployee { get; private set; }

        public Guid? AssociatedUserId { get; private set; }

        public int SaveCount { get; private set; }

        public bool ThrowEmployeeNumberConflict { get; set; }

        public Task<IReadOnlyList<EmployeeDto>> SearchAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            LastSearch = search;
            return Task.FromResult<IReadOnlyList<EmployeeDto>>([]);
        }

        public Task<EmployeeDto?> GetAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            var entity = AddedEmployee ?? ExistingEmployee;
            return Task.FromResult(entity is null ? null : ToDto(entity));
        }

        public Task<Employee?> FindEntityAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(ExistingEmployee);
        }

        public Task<EmployeeOptions> GetOptionsAsync(
            Guid companyId,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(new EmployeeOptions([], []));
        }

        public Task<bool> EmployeeNumberExistsAsync(
            Guid companyId,
            string employeeNumber,
            Guid? employeeIdToExclude,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(EmployeeNumberExists);
        }

        public Task<bool> WorkSiteCanBeAssignedAsync(
            Guid companyId,
            Guid workSiteId,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            return Task.FromResult(WorkSiteCanBeAssigned);
        }

        public Task<EmployeeUserAssociationStatus> SetUserAssociationAsync(
            Guid companyId,
            Guid employeeId,
            Guid? userId,
            CancellationToken cancellationToken)
        {
            LastCompanyId = companyId;
            AssociatedUserId = userId;
            return Task.FromResult(AssociationStatus);
        }

        public void Add(Employee employee)
        {
            AddedEmployee = employee;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;

            if (ThrowEmployeeNumberConflict)
            {
                throw new EmployeeNumberConflictException(
                    "Conflito simulado.",
                    new InvalidOperationException("Conflito simulado."));
            }

            return Task.CompletedTask;
        }

        private static Employee NewEmployee()
        {
            return new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                EmployeeNumber = "FUNC001",
                Name = "Funcionário Demo",
                IsActive = true,
                CreatedAtUtc = Now
            };
        }

        private EmployeeDto ToDto(Employee employee)
        {
            return new EmployeeDto(
                employee.Id,
                employee.EmployeeNumber,
                employee.Name,
                employee.Email,
                employee.MobilePhone,
                employee.IsActive,
                employee.DefaultWorkSiteId,
                null,
                AssociatedUserId,
                null,
                employee.ErpEmployeeCode,
                employee.CreatedAtUtc,
                employee.UpdatedAtUtc);
        }
    }
}
