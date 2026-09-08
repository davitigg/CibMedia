using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;

namespace CibMedia.Core.Presentation.Details;

// What the movie and series pages share: the resume point follows watch history, and a
// finished title counts as never started so Play offers it from the beginning.
public abstract class DetailViewModel<TState> : ViewModel<TState>
    where TState : class
{
    private readonly IPlaybackHistory _history;

    protected DetailViewModel(IPlaybackHistory history, MediaId id, TState initial)
        : base(initial)
    {
        _history = history;
        Id = id;

        // Follows the write rather than a lifecycle callback: the player saves on its way out
        // without waiting, so re-reading history when the page resumes is a race it can lose.
        _history.Changed += OnHistoryChanged;
    }

    protected MediaId Id { get; }

    public abstract Task ReloadResumeAsync();

    protected override void OnDispose()
    {
        _history.Changed -= OnHistoryChanged;
    }

    protected async Task<WatchProgress?> ReadResumeAsync()
    {
        var progress = await _history.ForAsync(Id, Lifetime).ConfigureAwait(false);

        return progress is { IsFinished: false } ? progress : null;
    }

    private void OnHistoryChanged()
    {
        _ = ReloadResumeAsync();
    }
}
