namespace Domain.Abstractions;

public interface IHasTenant
{
    Guid TenantId { get; set; }
}
