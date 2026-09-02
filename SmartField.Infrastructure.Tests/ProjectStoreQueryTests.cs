using Microsoft.EntityFrameworkCore;
using SmartField.Infrastructure.Persistence;
using SmartField.Infrastructure.Projects;

namespace SmartField.Infrastructure.Tests;

public class ProjectStoreQueryTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");

    [Theory]
    [InlineData(null)]
    [InlineData("OBRA001")]
    public void BuildSearchQuery_IsTranslatableBySqlServerProvider(string? search)
    {
        var options = new DbContextOptionsBuilder<SmartFieldDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=SmartField_QueryTranslation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new SmartFieldDbContext(options)
        {
            CurrentCompanyId = CompanyId
        };
        var store = new ProjectStore(context);

        var sql = store.BuildSearchQuery(CompanyId, search).ToQueryString();

        Assert.Contains("FROM [Projects]", sql);
        Assert.Contains("ORDER BY", sql);
    }
}
