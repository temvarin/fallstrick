using Gram.Domain;

namespace Gram.App.Services;

/// <summary>The learner's progress for the running app. Components change <see cref="Progress"/>, then call <see cref="Save"/>.</summary>
/// <remarks>Progress from an earlier release, stored or from a backup, is moved to this release's card keys first.</remarks>
public sealed class AppState(ProgressStore store, Catalog catalog)
{
    public Progress Progress { get; private set; } = Current(store.Load(), catalog);

    public void Save() => store.Save(Progress);

    /// <summary>Replaces the progress, e.g. with one read from a backup file.</summary>
    public void Replace(Progress progress)
    {
        Progress = Current(progress, catalog);
        Save();
    }

    static Progress Current(Progress progress, Catalog catalog)
    {
        progress.Move(catalog.KeyMoves);
        return progress;
    }
}

public static class Clock
{
    /// <summary>The learner's calendar day. Never stored: the app may stay open across midnight.</summary>
    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
