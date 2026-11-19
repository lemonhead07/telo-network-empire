using Godot;
using Telo.Game.World;
using Telo.Sim.Nez;

namespace Telo.Game;

/// <summary>
/// Nez in the world (spine step 8): the exchange at Nadir on the far side of the planet, the monopoly's van that parks in your
/// street once it has noticed you, and a plaque on every house it took first. Placeholder boxes; the rules are in NezRules.
/// </summary>
public partial class GameRoot
{
    private Node3D? _nezVan;
    private readonly Dictionary<string, Label3D> _nezPlaques = new();
    private static readonly Color NezBlue = new(0.12f, 0.2f, 0.42f), NezGrey = new(0.62f, 0.64f, 0.66f), NezLetter = new(0.95f, 0.93f, 0.86f);

    private Vector3 _nadir;

    /// <summary>Build Nadir: the national exchange, at the antipode of your town. If the antipode is sea, it stands on a pier.</summary>
    private void BuildNadir()
    {
        var dir = PlanetBuilder.ToGodot(NezRules.NadirDir(_planet)).Normalized();
        float R = (float)_planet.RadiusM;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(dir * (R + 600), dir * (R - 200), 1));
        float ground = hit.Count > 0 ? ((Vector3)hit["position"]).Length() : R;
        ground = Mathf.Max(ground, R + 1.5f);                       // above the waves
        _nadir = dir * ground;
        var fwd = dir.Cross(Vector3.Up).Normalized();
        if (fwd.LengthSquared() < 0.1f) fwd = dir.Cross(Vector3.Right).Normalized();
        var basis = new Basis(fwd.Cross(dir).Normalized(), dir, fwd);
        var root = new Node3D { Name = "Nadir", Transform = new Transform3D(basis, _nadir) };
        if (Props.HasModel("nez.nadir")) root.AddChild(Props.Make("nez.nadir"));
        else root.AddChild(new LowPoly()
            .Box(new Vector3(0, -4f, 0), new Vector3(40, 8, 30), NezGrey * 0.8f)                     // plinth / pier
            .Box(new Vector3(0, 6f, 0), new Vector3(30, 12, 18), NezGrey)                          // the exchange hall
            .Box(new Vector3(0, 12.4f, 0), new Vector3(31, 0.8f, 19), NezBlue)                     // roof band
            .Box(new Vector3(10, 20f, -4), new Vector3(4, 28, 4), NezGrey * 0.9f)                  // microwave tower
            .Cylinder(new Vector3(10, 34f, -4), 2.4f, 2.4f, 0.6f, 10, NezBlue)                     // dish
            .Box(new Vector3(0, 3f, 9.05f), new Vector3(6, 6, 0.2f), NezBlue)                      // doors
            .Instance());
        root.AddChild(new Label3D { Text = "NATIONAL TELEPHONE EXCHANGE\nNADIR", Position = new Vector3(0, 16f, 0), FontSize = 96, PixelSize = 0.02f, Modulate = NezLetter, OutlineSize = 12, OutlineModulate = NezBlue, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
        AddChild(root);
    }

    /// <summary>Keep the street up to date with what Nez has done: its van once it has noticed you, a plaque on each house it took.</summary>
    private void SyncNez()
    {
        var s = Sim.State;
        if (NezRules.Stage(s) >= NezStage.Notice && _nezVan is null)
        {
            _nezVan = new Node3D { Name = "NezVan", Transform = TownBuilder.Place(_planet, -34, 11.5f, 90, 0.05f) };
            if (Props.HasModel("vehicle.nez-van")) _nezVan.AddChild(Props.Make("vehicle.nez-van"));   // lettered on the model itself
            else
            {
                _nezVan.AddChild(new LowPoly().Box(new Vector3(0, 1.1f, 0), new Vector3(2.0f, 1.9f, 4.6f), NezBlue)
                    .Box(new Vector3(0, 1.5f, -1.95f), new Vector3(1.7f, 0.6f, 0.75f), new Color(0.25f, 0.35f, 0.42f))
                    .Box(new Vector3(0, 0.9f, 0), new Vector3(2.04f, 0.2f, 4.62f), NezLetter)
                    .Instance());
                foreach (int side in new[] { -1, 1 })
                    _nezVan.AddChild(new Label3D { Text = "NATIONAL TELEPHONE", Position = new Vector3(side * 1.03f, 1.45f, 0.3f), RotationDegrees = new Vector3(0, side * 90, 0), FontSize = 48, PixelSize = 0.0045f, Modulate = NezLetter, OutlineSize = 0 });
            }
            _town.Root.AddChild(_nezVan);
        }
        foreach (var id in s.NezLots)
        {
            if (_nezPlaques.ContainsKey(id) || _town.Lots.FirstOrDefault(l => l.Lot.Id == id) is not { } lot) continue;
            var plaque = new Label3D { Text = "NATIONAL\nTELEPHONE", Position = lot.Body.ToLocal(lot.Door.GlobalPosition) + new Vector3(1.1f, 0.6f, 0.15f), FontSize = 32, PixelSize = 0.004f, Modulate = NezBlue, OutlineSize = 6, OutlineModulate = NezLetter };
            lot.Body.AddChild(plaque);
            _nezPlaques[id] = plaque;
        }
    }
}
