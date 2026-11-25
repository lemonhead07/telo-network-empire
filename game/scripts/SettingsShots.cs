using Godot;
using Telo.Game.UI;
using Telo.Game.World;
using Telo.Sim.Content;

namespace Telo.Game;

/// <summary>
/// --shots=settings (EA6b): the town at each graphics setting, then the settings page and PIP's how-to-play, each hosted
/// for the picture on a stand-in screen where the notebook and the handset lie, and leaned into like any other page.
///   xvfb-run -a godot --path game -- --shot=/abs/dir/x.png --shots=settings
/// saves x-gfx-low/-medium/-high, x-settings, x-settings-terminal, x-settings-low, x-howto (page 3), x-howto-1 ... -7, x-howto-paper.
/// </summary>
public partial class GameRoot
{
    private async Task SettingsShots(string path, Func<string, Vector3, Vector3, Vector3?, Task> shot)
    {
        // the same street at Low, Medium and High (automated runs never save, so nothing here sticks)
        var hq = _town.Centre;
        foreach (var q in new[] { GraphicsQuality.Low, GraphicsQuality.Medium, GraphicsQuality.High })
        {
            GameSettings.Graphics = q; GameSettings.Apply();
            await shot($"-gfx-{GameSettings.QualityName(q).ToLowerInvariant()}", TownBuilder.Place(_planet, 22, 40, 0, 7).Origin, hq + hq.Normalized() * 2, null);
        }
        GameSettings.Graphics = GraphicsQuality.Medium; GameSettings.Apply();

        // inside: lean in to each page on a stand-in screen, with the shot camera (so the phone in your hand and the
        // prompt strip, which belong to the player, stay out of the picture)
        var cam = GetViewport().GetCamera3D();
        _hud.Visible = false;
        RefreshIdleScreens();
        async Task Save(string suffix) { await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout); GetViewport().GetTexture().GetImage().SavePng(path.Replace(".png", suffix + ".png")); }
        async Task On(ModalView view, WorldScreen like, float widthM, bool glows, InterfaceTier tier, string suffix, Action? before = null)
        {
            var stand = WorldScreen.Create(view, widthM, glows);
            like.GetParent().AddChild(stand);
            stand.Transform = like.Transform;
            like.Visible = false;
            view.Show(tier);
            before?.Invoke();
            view.Open(tier);
            var size = GetViewport().GetVisibleRect().Size;
            cam.GlobalTransform = stand.LeanPose(cam.Fov, size.X / size.Y);
            await Save(suffix);
            view.Close();
            stand.QueueFree();
            like.Visible = true;
        }

        await On(new SettingsView(), _notebookScreen, 0.48f, false, InterfaceTier.Paper, "-settings");
        await On(new SettingsView(), _pipScreen, 0.34f, true, InterfaceTier.Terminal, "-settings-terminal");
        GameSettings.Graphics = GraphicsQuality.Low; GameSettings.Apply();   // pages must stay readable on Low
        await On(new SettingsView(), _notebookScreen, 0.48f, false, InterfaceTier.Paper, "-settings-low");
        GameSettings.Graphics = GraphicsQuality.Medium; GameSettings.Apply();

        var hands = new HowToPlayView();
        await On(hands, _pipScreen, 0.34f, true, InterfaceTier.Terminal, "-howto", () => hands.PageIndex = 2);
        for (int i = 0; i < HowToPlayView.PageCount; i++)
        {
            var page = new HowToPlayView(); int n = i;
            await On(page, _pipScreen, 0.34f, true, InterfaceTier.Terminal, $"-howto-{n + 1}", () => page.PageIndex = n);
        }
        var paper = new HowToPlayView();
        await On(paper, _notebookScreen, 0.48f, false, InterfaceTier.Paper, "-howto-paper", () => paper.PageIndex = HowToPlayView.PageCount - 1);
        _hud.Visible = true;
    }
}
