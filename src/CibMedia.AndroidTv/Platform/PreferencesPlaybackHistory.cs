using System.Text.Json;
using Android.Content;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Catalog;
using CibMedia.Core.Playback;

namespace CibMedia.AndroidTv.Platform;

// SharedPreferences holding one JSON array, which is fine while this is a bounded list of
// recent titles.
public sealed class PreferencesPlaybackHistory(Context context) : IPlaybackHistory
{
    private const string PreferencesName = "cibmedia.history";
    private const string Key = "recent";
    private const int MaxEntries = 25;

    public event Action? Changed;

    public Task<IReadOnlyList<WatchProgress>> RecentAsync(CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<WatchProgress>>(
            [.. Read().Where(p => !p.IsFinished).OrderByDescending(p => p.WatchedAt)]);
    }

    public Task<WatchProgress?> ForAsync(MediaId id, CancellationToken ct)
    {
        return Task.FromResult(Read().FirstOrDefault(p => p.Id == id));
    }

    public Task<int> CountAsync(CancellationToken ct)
    {
        return Task.FromResult(Read().Length);
    }

    // Commit, not Apply: Changed promises the write has landed.
    public Task RecordAsync(WatchProgress progress, CancellationToken ct)
    {
        var entries = Read()
            .Where(p => p.Id != progress.Id)
            .Prepend(progress)
            .OrderByDescending(p => p.WatchedAt)
            .Take(MaxEntries)
            .ToArray();

        var editor = Preferences()?.Edit();
        editor?.PutString(Key, JsonSerializer.Serialize(entries, HistoryJson.Default.WatchProgressArray));
        editor?.Commit();

        Changed?.Invoke();

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct)
    {
        var editor = Preferences()?.Edit();
        editor?.Remove(Key);
        editor?.Commit();

        Changed?.Invoke();

        return Task.CompletedTask;
    }

    private WatchProgress[] Read()
    {
        var raw = Preferences()?.GetString(Key, null);
        if (string.IsNullOrWhiteSpace(raw)) return [];

        try
        {
            return JsonSerializer.Deserialize(raw, HistoryJson.Default.WatchProgressArray) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private ISharedPreferences? Preferences()
    {
        return context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
    }
}
