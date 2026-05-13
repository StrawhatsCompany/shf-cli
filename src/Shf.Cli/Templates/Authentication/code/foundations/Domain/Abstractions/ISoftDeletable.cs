namespace Domain.Abstractions;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
}
