using CibMedia.Core.Common;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace CibMedia.AndroidTv.App;

// Holds a view model for the life of a fragment: subscribe, marshal the notification onto the
// UI thread, unsubscribe and dispose on the way out.
public sealed class StateBinding : IDisposable
{
    private readonly Action _detach;
    private readonly IDisposable? _owned;

    private bool _disposed;

    private StateBinding(Action detach, IDisposable? owned)
    {
        _detach = detach;
        _owned = owned;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _detach();
        _owned?.Dispose();
    }

    // ownsViewModel is false where the view model outlives the screen, as a section's rails do
    // across side-panel switches. The unsubscribe happens either way.
    public static StateBinding Bind<TState>(
        Fragment owner,
        ViewModel<TState> viewModel,
        Action render,
        bool ownsViewModel = true)
        where TState : class
    {
        void OnChanged()
        {
            owner.Activity?.RunOnUiThread(render);
        }

        viewModel.StateChanged += OnChanged;

        return new StateBinding(
            () => viewModel.StateChanged -= OnChanged,
            ownsViewModel ? viewModel : null);
    }
}
