namespace Gram.Domain;

/// <summary>
/// The learner's progress while the app runs. Every change goes through a method here and is saved before it returns,
/// so a page can't change progress and forget to save it: <see cref="Progress"/>'s own changing methods are internal.
/// </summary>
public sealed class ProgressService
{
    readonly ProgressStore _store;
    readonly Catalog _catalog;

    public ProgressService(ProgressStore store, Catalog catalog)
    {
        (_store, _catalog) = (store, catalog);
        Progress = store.Load();
    }

    /// <summary>To read; changes go through this service.</summary>
    public Progress Progress { get; private set; }

    /// <summary>A session over the catalog's cards; each answer is saved as it is recorded.</summary>
    public Session StartSession(DateOnly today, Random random) => Session.Start(_catalog.Cards, Progress, today, random, Save);

    /// <summary>How many different cards a session started now would hold. A session changes nothing until it is answered.</summary>
    public int NextSessionSize(DateOnly today) => Session.Start(_catalog.Cards, Progress, today, Random.Shared).Total;

    public void SetNewPerDay(int count) => Change(p => p.SetNewPerDay(count));

    public void MarkBackedUp(DateOnly today) => Change(p => p.MarkBackedUp(today));

    /// <summary>The progress as a backup file (see <see cref="ProgressStore.Backup"/>).</summary>
    public string Backup() => ProgressStore.Backup(Progress);

    /// <returns>The progress in a backup file; null unless it is a backup of this app.</returns>
    public static Progress? ReadBackup(string json) => ProgressStore.TryRead(json);

    /// <summary>Replaces the progress with one read from a backup file.</summary>
    public void Restore(Progress progress)
    {
        Progress = progress;
        Save();
    }

    /// <summary>Forgets everything the learner has practised.</summary>
    public void Reset() => Restore(new Progress());

    void Change(Action<Progress> change)
    {
        change(Progress);
        Save();
    }

    void Save() => _store.Save(Progress);
}
