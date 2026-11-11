namespace Telo.Sim.Core;

/// <summary>A day on the worldline. Day 0 = 1 Jan 1983, the morning PIP wakes up.</summary>
public readonly record struct GameDate(int Day) : IComparable<GameDate>
{
    public static readonly DateOnly Epoch = new(1983, 1, 1);

    public DateOnly ToDate() => Epoch.AddDays(Day);
    public int Year => ToDate().Year;
    public bool IsFirstOfMonth => ToDate().Day == 1;

    public static GameDate From(int year, int month, int day) =>
        new(new DateOnly(year, month, day).DayNumber - Epoch.DayNumber);

    /// <summary>Parses "YYYY-MM-DD" (content files use this).</summary>
    public static GameDate Parse(string iso) => From(
        int.Parse(iso[..4]), int.Parse(iso.Substring(5, 2)), int.Parse(iso.Substring(8, 2)));

    public GameDate AddDays(int days) => new(Day + days);
    public int CompareTo(GameDate other) => Day.CompareTo(other.Day);
    public static bool operator <(GameDate a, GameDate b) => a.Day < b.Day;
    public static bool operator >(GameDate a, GameDate b) => a.Day > b.Day;
    public static bool operator <=(GameDate a, GameDate b) => a.Day <= b.Day;
    public static bool operator >=(GameDate a, GameDate b) => a.Day >= b.Day;

    public override string ToString() => ToDate().ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
