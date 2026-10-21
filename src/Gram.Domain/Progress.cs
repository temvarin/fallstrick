using System.Text.Json.Serialization;

namespace Gram.Domain;

/// <summary>
/// What the learner knows: the scheduler's memory of every card answered so far, and how many new cards
/// were started today. Persisted by <see cref="ProgressStore"/>.
/// </summary>
/// <remarks>State changes only through its methods; the internal setters exist for deserialization.</remarks>
public sealed class Progress
{
    public const int DefaultNewPerDay = 10;

    [JsonInclude] internal Dictionary<string, Memory> Memories { get; set; } = [];

    /// <summary>How many cards the learner meets for the first time per day.</summary>
    [JsonInclude] public int NewPerDay { get; internal set; } = DefaultNewPerDay;

    [JsonInclude] internal DateOnly NewDay { get; set; }
    [JsonInclude] internal int NewCount { get; set; }

    /// <summary>When the learner last saved a backup file; progress lives only in this browser otherwise.</summary>
    [JsonInclude] public DateOnly? LastBackup { get; internal set; }

    public const int BackupReminderDays = 30;

    public bool NeedsBackup(DateOnly today) =>
        Memories.Count > 0 && (LastBackup is not { } last || today.DayNumber - last.DayNumber >= BackupReminderDays);

    public void MarkBackedUp(DateOnly today) => LastBackup = today;

    public bool IsNew(string key) => !Memories.ContainsKey(key);

    public bool IsDue(string key, DateOnly today) => Memories.TryGetValue(key, out var memory) && memory.Due <= today;

    public int NewLeft(DateOnly today) => Math.Max(0, NewPerDay - (NewDay == today ? NewCount : 0));

    public void SetNewPerDay(int count) => NewPerDay = Math.Clamp(count, 0, 50);

    public void Record(string key, Rating rating, DateOnly today, Random? fuzz)
    {
        if (!Memories.TryGetValue(key, out var memory))
        {
            if (NewDay != today) (NewDay, NewCount) = (today, 0);
            NewCount++;
        }
        Memories[key] = Fsrs.Review(memory, rating, today, fuzz);
    }
}
