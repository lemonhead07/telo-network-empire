using Godot;

namespace Telo.Game.Audio;

/// <summary>
/// The background (EA7): a quiet outdoor bed of wind, birds by day and crickets at night, room tone indoors.
/// Reads <see cref="Sfx.Indoors"/> and <see cref="Sfx.Hour"/>; GameRoot adds one of these once the world is built.
/// </summary>
public partial class Ambience : Node
{
    private SfxLoop? _wind;

    public override void _Ready()
    {
        _wind = Sfx.Loop("amb.wind");
    }

    public override void _Process(double delta)
    {
        if (_wind is not null) _wind.Db = Sfx.Indoors() ? -14f : 0f;
    }
}
