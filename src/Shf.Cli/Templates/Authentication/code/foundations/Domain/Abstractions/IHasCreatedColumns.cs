namespace Domain.Abstractions;

public interface IHasCreatedColumns
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
