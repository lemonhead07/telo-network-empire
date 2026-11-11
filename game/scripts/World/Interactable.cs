using Godot;

namespace Telo.Game.World;

/// <summary>Anything the player can look at and press E on. The prompt can change with game state.</summary>
public partial class Interactable : StaticBody3D
{
    public Func<string> Prompt { get; set; } = () => "Use";
    public Action? OnUse { get; set; }
    /// <summary>False when the prompt is only information (nothing happens on E).</summary>
    public Func<bool> CanUse { get; set; } = () => true;
}
