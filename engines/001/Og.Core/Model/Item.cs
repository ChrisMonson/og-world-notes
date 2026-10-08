using Og.Core.Definitions;

namespace Og.Core.Model;

/// <summary>An instance of an item in the world, backed by its data definition.</summary>
public sealed class Item(ItemDefinition definition)
{
    public ItemDefinition Definition { get; } = definition;

    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public ItemKind Kind => Definition.Kind;

    public bool Matches(string name) =>
        string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Name;
}
