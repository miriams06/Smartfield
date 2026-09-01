namespace SmartField.Application.Abstractions;

public interface ICurrentUserProvider
{
    Guid? UserId { get; }

    Guid? EmployeeId { get; }
}
