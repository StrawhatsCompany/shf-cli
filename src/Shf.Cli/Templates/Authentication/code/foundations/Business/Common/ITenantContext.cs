namespace Business.Common;

public interface ITenantContext
{
    Guid? TenantId { get; }
}
