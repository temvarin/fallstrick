using Gram.Domain;

namespace Gram.App.Services;

public abstract record Modal;

/// <param name="Cancel">Second button; none for a plain message.</param>
public sealed record MessageModal(string Message, string Ok, string? Cancel, bool Danger, TaskCompletionSource<bool> Result) : Modal;

public sealed record TermModal(Term Term) : Modal;

/// <summary>
/// In-app dialogs, shown by <c>DialogHost</c>. Replaces confirm()/alert(): some browsers and embedded views
/// suppress native dialogs, which makes confirm() silently return false.
/// </summary>
public sealed class Dialogs
{
    public Modal? Current { get; private set; }

    public event Action? Changed;

    /// <returns>true for the primary button; false if cancelled or closed by the app.</returns>
    public Task<bool> Confirm(string message, string ok = "OK", string? cancel = null, bool danger = false)
    {
        Close();
        var result = new TaskCompletionSource<bool>();
        Show(new MessageModal(message, ok, cancel, danger, result));
        return result.Task;
    }

    /// <summary>A message with an OK button.</summary>
    public void Alert(string message) => _ = Confirm(message);

    public void ShowTerm(Term term)
    {
        Close();
        Show(new TermModal(term));
    }

    public void Close(bool result = false)
    {
        if (Current is null) return;
        if (Current is MessageModal m) m.Result.TrySetResult(result);
        Current = null;
        Changed?.Invoke();
    }

    private void Show(Modal modal)
    {
        Current = modal;
        Changed?.Invoke();
    }
}
