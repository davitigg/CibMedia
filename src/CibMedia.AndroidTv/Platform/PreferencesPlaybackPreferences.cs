using Android.Content;
using CibMedia.Core.Abstractions;

namespace CibMedia.AndroidTv.Platform;

public sealed class PreferencesPlaybackPreferences : IPlaybackPreferences
{
    private const string PreferencesName = "cibmedia.playback";
    private const string ProviderKey = "defaultProvider";
    private const string LanguageKey = "preferredLanguage";

    private readonly ISharedPreferences? _preferences;

    public PreferencesPlaybackPreferences(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
    }

    public string? DefaultProvider
    {
        get => _preferences?.GetString(ProviderKey, null);
        set => Write(ProviderKey, value);
    }

    public string? PreferredLanguage
    {
        get => _preferences?.GetString(LanguageKey, null);
        set => Write(LanguageKey, value);
    }

    private void Write(string key, string? value)
    {
        var editor = _preferences?.Edit();

        if (value is null)
            editor?.Remove(key);
        else
            editor?.PutString(key, value);

        editor?.Apply();
    }
}
