using Godot;
using Telo.Sim.Content;

namespace Telo.Game.World;

/// <summary>
/// Visible things by prop id (D30). Asks for "vehicle.van", gets the Blender model at
/// res://assets/models/vehicle.van.glb if one exists, otherwise the code-built placeholder.
/// Making a model is then just dropping a .glb in that folder: no code change.
/// </summary>
public static class Props
{
    private static PropCatalog? _catalog;
    public static PropCatalog Catalog => _catalog ??= PropCatalog.FromJson(Godot.FileAccess.GetFileAsString("res://content/props.json"));

    public static string ModelPath(string id) => $"res://assets/models/{id}.glb";

    public static bool HasModel(string id) => ResourceLoader.Exists(ModelPath(id));

    /// <summary>The model if there is one, else <paramref name="placeholder"/>, else a labelled box sized from the brief.</summary>
    public static Node3D Make(string id, Func<Node3D>? placeholder = null)
    {
        if (HasModel(id) && GD.Load<PackedScene>(ModelPath(id)) is { } scene)
        {
            var model = scene.Instantiate<Node3D>();
            model.Name = id.Replace('.', '_');
            return model;
        }
        return placeholder?.Invoke() ?? Box(id);
    }

    /// <summary>Grey box the size of the brief, with its name floating on it.</summary>
    public static Node3D Box(string id)
    {
        var def = Catalog.Get(id) ?? throw new InvalidOperationException($"Unknown prop '{id}' (add it to content/props.json)");
        var size = new Vector3(def.Size[0], def.Size[1], def.Size[2]);
        var root = new LowPoly().Box(Vector3.Zero, size, new Color(0.62f, 0.62f, 0.6f)).Instance(id.Replace('.', '_'));
        root.AddChild(new Label3D { Text = def.Name.Length > 0 ? def.Name : id, PixelSize = 0.002f, Position = new Vector3(0, 0, size.Z / 2 + 0.002f), Modulate = Colors.Black, OutlineSize = 0 });
        return root;
    }

    /// <summary>Child nodes whose names start with <paramref name="prefix"/> (e.g. the van's "wheel-" nodes), searched at any depth.</summary>
    public static List<Node3D> Parts(Node root, string prefix)
    {
        var found = new List<Node3D>();
        void Walk(Node n) { foreach (var c in n.GetChildren()) { if (c is Node3D n3 && c.Name.ToString().StartsWith(prefix)) found.Add(n3); Walk(c); } }
        Walk(root);
        return found;
    }
}
