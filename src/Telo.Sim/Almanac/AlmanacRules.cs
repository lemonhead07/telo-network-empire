using Telo.Sim.Content;
using Telo.Sim.Core;
using Telo.Sim.World;

namespace Telo.Sim.Almanac;

public enum EchoBand { Quiet, Noticed, Watched, Investigated, Correction }

public enum RecallResult { Ok, NotReadable, AlreadyRecalled, NotEnoughArchive, UnknownEntry }

/// <summary>Research is recall. The world announces technology on a queue; the player can remember it early, at a price.</summary>
public static class AlmanacRules
{
    public const int CorrectionThreshold = 80;
    public const int CorrectionRelief = 30;
    public const int EchoDecayEveryDays = 30;

    // Archive pacing (milli-Archive per day). Tunable; see docs/DECISIONS.md D5.
    public const int ArchiveBasePerDay = 200;
    public const int ArchivePerSubscriber = 50;
    public const int ArchivePerStaff = 300;

    public static EchoBand Band(int echo) => echo switch
    {
        < 10 => EchoBand.Quiet,
        < 25 => EchoBand.Noticed,
        < 50 => EchoBand.Watched,
        < CorrectionThreshold => EchoBand.Investigated,
        _ => EchoBand.Correction,
    };

    public static GameDate PublicDate(GameState s, ContentDb c, string announcementId)
    {
        var baseDate = GameDate.Parse(c.Announcements[announcementId].Date);
        return baseDate.AddDays(s.AnnouncementShift.GetValueOrDefault(announcementId));
    }

    public static bool IsPublic(GameState s, string announcementId) => s.FiredAnnouncements.Contains(announcementId);

    /// <summary>Years early (rounded up) if recalled today; 0 if the world already knows.</summary>
    public static int YearsEarly(GameState s, ContentDb c, AlmanacEntryDef e)
    {
        if (IsPublic(s, e.Announcement)) return 0;
        int days = PublicDate(s, c, e.Announcement).Day - s.Day;
        return days <= 0 ? 0 : (days + 364) / 365;
    }

    public static int EchoCost(GameState s, ContentDb c, AlmanacEntryDef e) => YearsEarly(s, c, e) * e.EchoPerYearEarly;

    public static void RefreshStatuses(GameState s, ContentDb c)
    {
        foreach (var e in c.Almanac.Values)
        {
            var cur = s.Entries.GetValueOrDefault(e.Id, EntryStatus.Sealed);
            if (cur is EntryStatus.Recalled or EntryStatus.Drifting) continue;
            bool readable = s.ActIndex >= e.Act && e.Requires.All(r =>
                s.Entries.GetValueOrDefault(r) is EntryStatus.Recalled or EntryStatus.Drifting);
            var next = readable ? EntryStatus.Readable : EntryStatus.Sealed;
            if (!s.Entries.ContainsKey(e.Id)) s.Entries[e.Id] = EntryStatus.Sealed;
            if (next != cur)
            {
                s.Entries[e.Id] = next;
                if (next == EntryStatus.Readable)
                    s.Emit(EventKind.Info, $"PIP: a page un-corrupts — \"{e.Title}\".", e.Id);
            }
        }
    }

    public static RecallResult Recall(GameState s, ContentDb c, string entryId)
    {
        if (!c.Almanac.TryGetValue(entryId, out var e)) return RecallResult.UnknownEntry;
        var status = s.Entries.GetValueOrDefault(entryId);
        if (status is EntryStatus.Recalled or EntryStatus.Drifting) return RecallResult.AlreadyRecalled;
        if (status != EntryStatus.Readable) return RecallResult.NotReadable;
        if (s.Archive < e.ArchiveCost) return RecallResult.NotEnoughArchive;

        int echo = EchoCost(s, c, e);
        s.ArchiveMilli -= e.ArchiveCost * 1000L;
        s.Entries[entryId] = EntryStatus.Recalled;
        if (echo > 0)
        {
            s.Echo += echo;
            s.EarlyRecalls.Add(entryId);
            s.Emit(EventKind.Echo, $"Recalled \"{e.Title}\" {YearsEarlyText(echo / Math.Max(1, e.EchoPerYearEarly))} early. Someone noticed. (+{echo} Echo)", entryId);
        }
        else
        {
            s.Emit(EventKind.Info, $"Recalled \"{e.Title}\" — right on time. Nobody blinks.", entryId);
        }
        RefreshStatuses(s, c);
        return RecallResult.Ok;
    }

    private static string YearsEarlyText(int y) => y == 1 ? "a year" : $"{y} years";

    /// <summary>Calendar lever: fund a standards body (negative days) or quietly discredit a rival's demo (positive).</summary>
    public static bool ShiftAnnouncement(GameState s, ContentDb c, string announcementId, int deltaDays, long cost)
    {
        if (!c.Announcements.ContainsKey(announcementId) || IsPublic(s, announcementId) || s.Cash < cost) return false;
        s.Cash -= cost;
        s.AnnouncementShift[announcementId] = s.AnnouncementShift.GetValueOrDefault(announcementId) + deltaDays;
        s.Echo += 2; // meddling with the calendar is itself noticeable
        return true;
    }

    /// <summary>Daily tick for the Almanac spine.</summary>
    public static void Tick(GameState s, ContentDb c)
    {
        foreach (var a in c.Announcements.Values)
        {
            if (s.FiredAnnouncements.Contains(a.Id)) continue;
            if (PublicDate(s, c, a.Id).Day <= s.Day)
            {
                s.FiredAnnouncements.Add(a.Id);
                s.Emit(EventKind.Announcement, string.IsNullOrEmpty(a.Headline) ? a.Title : a.Headline, a.Id);
            }
        }

        s.ArchiveMilli += ArchiveBasePerDay + ArchivePerSubscriber * s.LiveSubscribers + ArchivePerStaff * s.Staff;

        if (s.Echo > 0 && s.Day % EchoDecayEveryDays == 0) s.Echo--;

        if (s.Echo >= CorrectionThreshold)
        {
            s.Echo -= CorrectionRelief;
            if (s.EarlyRecalls.Count > 0)
            {
                var id = s.EarlyRecalls[^1];
                s.EarlyRecalls.RemoveAt(s.EarlyRecalls.Count - 1);
                s.Entries[id] = EntryStatus.Drifting;
                s.Emit(EventKind.Echo, $"Correction. Your notes on \"{c.Almanac[id].Title}\" no longer match the world. The entry is drifting.", id);
            }
            else s.Emit(EventKind.Echo, "Correction. A clock in the exchange disagrees with itself for an afternoon.");
        }
        RefreshStatuses(s, c);
    }
}
