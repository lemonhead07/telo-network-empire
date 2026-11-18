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

    /// <summary>The model's first mesh, for props drawn many times from one shared mesh (poles). Null if there is no model.</summary>
    public static Mesh? MeshOf(string id)
    {
        if (!HasModel(id) || GD.Load<PackedScene>(ModelPath(id)) is not { } scene) return null;
        var root = scene.Instantiate<Node>();
        Mesh? mesh = null;
        void Walk(Node n) { foreach (var c in n.GetChildren()) { if (mesh is null && c is MeshInstance3D mi) mesh = mi.Mesh; Walk(c); } }
        Walk(root);
        root.Free();
        return mesh;
    }

    /// <summary>Box around everything drawn under <paramref name="root"/>, in root's own space (works before it is in the tree).</summary>
    public static Aabb Bounds(Node3D root)
    {
        Aabb? box = null;
        void Walk(Node n, Transform3D xf)
        {
            foreach (var c in n.GetChildren())
            {
                var cx = c is Node3D n3 ? xf * n3.Transform : xf;
                if (c is VisualInstance3D vi) { var b = cx * vi.GetAabb(); box = box is { } a ? a.Merge(b) : b; }
                Walk(c, cx);
            }
        }
        Walk(root, Transform3D.Identity);
        return box ?? new Aabb(Vector3.Zero, Vector3.Zero);
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
