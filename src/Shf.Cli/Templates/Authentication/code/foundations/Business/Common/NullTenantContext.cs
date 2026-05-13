namespace Business.Common;

public sealed class NullTenantContext : ITenantContext
{
    public Guid? TenantId => null;
}
