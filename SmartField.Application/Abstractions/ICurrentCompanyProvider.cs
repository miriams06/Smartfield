namespace SmartField.Application.Abstractions;

public interface ICurrentCompanyProvider
{
    Guid? CompanyId { get; }
}
