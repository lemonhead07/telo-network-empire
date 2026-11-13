using Godot;

namespace Telo.Game.World;

/// <summary>Anything the player can look at and press E on. The prompt can change with game state.</summary>
public partial class Interactable : StaticBody3D
{
    public Func<string> Prompt { get; set; } = () => "Use";
    public Action? OnUse { get; set; }
    /// <summary>False when the prompt is only information (nothing happens on E).</summary>
    public Func<bool> CanUse { get; set; } = () => true;
    /// <summary>A second, usually worse, way of doing it (R). The prompt is null when there isn't one right now.</summary>
    public Func<string?> AltPrompt { get; set; } = () => null;
    public Action? OnAlt { get; set; }
    /// <summary>How long E must be held to do this (real seconds). Zero means a single press. Work takes the time it takes; nothing jumps the clock.</summary>
    public Func<float> HoldSeconds { get; set; } = () => 0f;
}
