using Domain.Abstractions;

namespace Domain.Entities.Identity;

// Tenant itself does NOT implement IHasTenant — it IS the tenant. Every other owned entity
// references it via TenantId.
public sealed class Tenant
    : IPrimaryKey<Guid>, IHasCreatedColumns, IHasAuditColumns, ISoftDeletable, IHasStatus<TenantStatus>
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public Guid? DeletedBy { get; set; }
}
