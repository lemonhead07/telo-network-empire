using Godot;

namespace Telo.Game.World;

/// <summary>
/// Lights the screens of a model. Models with glass a view could be drawn on carry empties named <c>screen</c> or
/// <c>screen-0</c>, <c>screen-1</c>… at the centre of the glass, facing +Z (tools/models/dc.py, modern.py). This puts a
/// quad there showing a Control drawn in its own SubViewport, so a NOC desk's monitors, the ops wall or a phone show
/// something live. For a screen the player leans into and clicks, use <see cref="WorldScreen"/> at the same spot.
/// </summary>
public static class ScreenFaces
{
    /// <summary>Every empty under <paramref name="model"/> whose name starts with <paramref name="prefix"/> gets a lit face
    /// <paramref name="sizeM"/> metres across, drawn at <paramref name="px"/> pixels by <paramref name="view"/>(index).</summary>
    public static List<SubViewport> Light(Node3D model, string prefix, Vector2 sizeM, Vector2I px, Func<int, Control> view)
    {
        var made = new List<SubViewport>();
        var spots = Props.Parts(model, prefix).OrderBy(n => n.Name.ToString()).ToList();
        for (int i = 0; i < spots.Count; i++)
        {
            var vp = new SubViewport { Size = px, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, TransparentBg = false, GuiDisableInput = true };
            var c = view(i);
            c.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            vp.AddChild(c);
            spots[i].AddChild(vp);
            var mat = new StandardMaterial3D { AlbedoTexture = vp.GetTexture(), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps };
            spots[i].AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = sizeM }, MaterialOverride = mat, Position = new Vector3(0, 0, 0.002f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            made.Add(vp);
        }
        return made;
    }
}
