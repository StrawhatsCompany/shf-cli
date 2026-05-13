namespace Domain.Abstractions;

public interface IHasAuditColumns
{
    Guid? CreatedBy { get; set; }
    Guid? UpdatedBy { get; set; }
    Guid? DeletedBy { get; set; }
}
