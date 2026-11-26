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
    /// <summary>Called every frame while E is held on this, with how far through it is (0..1), and with 0 when let go. The work shows on the thing itself.</summary>
    public Action<float>? OnHold { get; set; }
    /// <summary>Set when this thing's work is done at points on it (or when this is one of those points). See <see cref="WorkPoints"/>.</summary>
    public WorkPoints? Pieces { get; set; }

    // sounds of holding E to work (EA7, Audio/), heard from this thing: unset means the usual tick and click; a func returning null is silent
    /// <summary>Once, as a hold begins (the handset lifted, the ladder set against the pole).</summary>
    public Func<string?>? StartSound { get; set; }
    /// <summary>Over and over while E is held, one after another (unset: "work.tick").</summary>
    public Func<string?>? HoldSound { get; set; }
    /// <summary>When the hold completes, just before the job is done (unset: "work.done").</summary>
    public Func<string?>? DoneSound { get; set; }
}
