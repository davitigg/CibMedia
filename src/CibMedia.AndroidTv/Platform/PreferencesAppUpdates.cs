using Android.Content;
using CibMedia.Core.Abstractions;

namespace CibMedia.AndroidTv.Platform;

public sealed class PreferencesAppUpdates : IAppUpdatePreferences
{
    private const string PreferencesName = "cibmedia.device";
    private const string Key = "declinedVersionCode";

    private readonly ISharedPreferences? _preferences;

    private int _declined;

    public PreferencesAppUpdates(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        _declined = _preferences?.GetInt(Key, 0) ?? 0;
    }

    public int DeclinedVersionCode
    {
        get => _declined;
        set
        {
            if (_declined == value) return;

            _declined = value;

            var editor = _preferences?.Edit();
            editor?.PutInt(Key, value);
            editor?.Commit();
        }
    }
}
