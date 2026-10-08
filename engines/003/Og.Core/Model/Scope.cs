namespace Og.Core.Model;

/// <summary>Whether a piece of mutable state is shared by everyone or private to one player.</summary>
public enum Scope
{
    World,
    Player
}
