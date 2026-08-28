namespace SmartField.Infrastructure.Persistence;

internal static class SmartFieldSeedData
{
    public static readonly Guid CompanyId = Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");

    public static readonly Guid EmployeeId = Guid.Parse("49f8a4ab-9802-46a4-99d7-2bcd6a664ad8");

    public static readonly DateTimeOffset CreatedAtUtc = new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
}
