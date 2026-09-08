using Android.Content;
using CibMedia.Core.Abstractions;

namespace CibMedia.AndroidTv.Platform;

public sealed class PreferencesRemotePlaySwitch : IRemotePlaySwitch
{
    private const string PreferencesName = "cibmedia.device";
    private const string Key = "remotePlay";

    private readonly ISharedPreferences? _preferences;

    private bool _isOn;

    public PreferencesRemotePlaySwitch(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        _isOn = _preferences?.GetBoolean(Key, false) ?? false;
    }

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value) return;

            _isOn = value;

            var editor = _preferences?.Edit();
            editor?.PutBoolean(Key, value);
            editor?.Commit();

            Changed?.Invoke();
        }
    }

    public event Action? Changed;
}
