namespace Intentify.Shared.Abstractions;

public readonly record struct StrongId(Guid Value)
{
    public static StrongId New() => new(Guid.NewGuid());
}
