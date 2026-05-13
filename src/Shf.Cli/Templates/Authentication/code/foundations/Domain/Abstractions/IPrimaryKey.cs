namespace Domain.Abstractions;

public interface IPrimaryKey<TKey>
{
    TKey Id { get; set; }
}
