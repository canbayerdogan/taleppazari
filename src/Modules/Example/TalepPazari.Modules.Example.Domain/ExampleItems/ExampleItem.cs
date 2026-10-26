namespace TalepPazari.Modules.Example.Domain.ExampleItems;

public sealed class ExampleItem
{
    public const int NameMaxLength = 200;

    private ExampleItem(Guid id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Invariants live here. ArgumentException is an example-level choice, not a project-wide error model.
    public static ExampleItem Create(Guid id, string name, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException($"Name must be at most {NameMaxLength} characters.", nameof(name));
        }

        if (createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("CreatedAt must be in UTC.", nameof(createdAt));
        }

        return new ExampleItem(id, name, createdAt);
    }
}
