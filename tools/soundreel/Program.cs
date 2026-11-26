// Renders the game's sound bank (game/scripts/Audio/Sounds*.cs) to .wav files, outside Godot (EA7).
//
//   dotnet run --project tools/soundreel -- <outdir> [filter ...]
//
// A filter is an id prefix ("step.", "sw.") or a family ("foley", "line", "ambience"). No filter: everything.
// Writes <outdir>/<id>.wav (every variant, 0.3 s apart; a loop is played twice through so the seam can be heard),
// <outdir>/reel.wav (all of them in a row, each at its in-game level) and <outdir>/reel.txt (where each one starts).
// Prints one line per sound with checks: length, peak and RMS (dBFS), DC offset, the jump at a loop's seam.
// tools/soundreel.sh does this and also draws a spectrogram of each sound with ffmpeg.
using System.Text;
using Telo.Game.Audio;

if (args.Length < 1) { Console.Error.WriteLine("usage: soundreel <outdir> [id-prefix|family ...]"); return 2; }
var dir = Path.GetFullPath(args[0]);
Directory.CreateDirectory(dir);
var filters = args.Skip(1).ToArray();
var picked = Sounds.All.Where(r => filters.Length == 0 || filters.Any(f => r.Family == f || r.Id.StartsWith(f))).ToList();
if (picked.Count == 0) { Console.Error.WriteLine("no sounds match"); return 1; }

var reel = new List<float>();
var index = new StringBuilder();
float gap = 0.45f;
int warnings = 0;
Console.WriteLine($"{"id",-16} {"var",3} {"len s",6} {"peak",6} {"rms",6} {"dc",7}  notes");
foreach (var r in picked)
{
    var one = new List<float>();
    float peak = 0, rms = 0, dc = 0, len = 0, seam = 0;
    for (int v = 0; v < r.Variants; v++)
    {
        var x = Sounds.Render(r, v);
        peak = Math.Max(peak, Synth.Peak(x)); rms = Math.Max(rms, Synth.Rms(x)); len = Math.Max(len, (float)x.Seconds());
        dc = Math.Max(dc, Math.Abs(x.Average()));
        if (r.Loop) { seam = Math.Max(seam, Math.Abs(x[0] - x[^1])); one.AddRange(x); one.AddRange(x); }
        else one.AddRange(x);
        one.AddRange(new float[(int)(0.3 * Synth.Rate)]);
    }
    var notes = new List<string>();
    if (peak > 0.999f) notes.Add("CLIPS");
    if (peak < 0.05f) notes.Add("very quiet or silent");
    if (dc > 0.02f) notes.Add("DC offset");
    if (r.Loop && seam > 0.05f) notes.Add($"seam jump {seam:0.00}");
    if (!r.Loop && len > 6) notes.Add("long for a one-shot");
    warnings += notes.Count;
    Console.WriteLine($"{r.Id,-16} {r.Variants,3} {len,6:0.00} {Db(peak),6:0.0} {Db(rms),6:0.0} {dc,7:0.000}  {string.Join(", ", notes)}");
    Write(Path.Combine(dir, r.Id + ".wav"), one);
    index.AppendLine($"{reel.Count / (double)Synth.Rate,7:0.00}s  {r.Id,-16} {r.Note}");
    float g = (float)Math.Pow(10, Math.Max(r.GainDb, -20) / 20.0) * 1.8f;   // roughly the in-game balance, lifted so the quiet ones can be heard
    foreach (var s in one) reel.Add(Math.Clamp(s * g, -1f, 1f));
    reel.AddRange(new float[(int)(gap * Synth.Rate)]);
}
Write(Path.Combine(dir, "reel.wav"), reel);
File.WriteAllText(Path.Combine(dir, "reel.txt"), index.ToString());
Console.WriteLine($"{picked.Count} sounds, {reel.Count / (double)Synth.Rate:0.0} s of reel, {warnings} warning(s) -> {dir}");
return 0;

static float Db(float v) => v <= 1e-6f ? -120f : 20f * MathF.Log10(v);

static void Write(string path, List<float> s)
{
    using var f = new BinaryWriter(File.Create(path));
    int n = s.Count, bytes = n * 2;
    f.Write("RIFF"u8); f.Write(36 + bytes); f.Write("WAVE"u8);
    f.Write("fmt "u8); f.Write(16); f.Write((short)1); f.Write((short)1); f.Write(Synth.Rate); f.Write(Synth.Rate * 2); f.Write((short)2); f.Write((short)16);
    f.Write("data"u8); f.Write(bytes);
    foreach (var v in s) f.Write((short)Math.Clamp((int)MathF.Round(v * 32767f), -32768, 32767));
}
