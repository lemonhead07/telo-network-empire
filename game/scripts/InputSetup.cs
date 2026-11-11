using Godot;

namespace Telo.Game;

/// <summary>Registers input actions in code so the bindings live in one readable place (and can be rebound later).</summary>
public static class InputSetup
{
    public static void Register()
    {
        Key("move_forward", Godot.Key.W); Key("move_back", Godot.Key.S);
        Key("move_left", Godot.Key.A); Key("move_right", Godot.Key.D);
        Key("sprint", Godot.Key.Shift); Key("jump", Godot.Key.Space);
        Key("interact", Godot.Key.E); Key("cancel", Godot.Key.Escape);
        
        Key("save", Godot.Key.F5); Key("load", Godot.Key.F9); Key("fly", Godot.Key.F);
    }

    private static void Key(string action, Key key)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
