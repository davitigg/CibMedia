using Android.Content;
using CibMedia.Core.Abstractions;

namespace CibMedia.AndroidTv.Platform;

public sealed class PreferencesApiKeys : IApiKeys
{
    // The file and the TMDb entry keep the names an installed box already holds its key under.
    private const string PreferencesName = "cibmedia.tmdb";

    private readonly Dictionary<ApiKeyKind, string?> _keys = [];
    private readonly ISharedPreferences? _preferences;

    public PreferencesApiKeys(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);

        foreach (var kind in ApiKeys.All) _keys[kind] = Blank(_preferences?.GetString(NameOf(kind), null));
    }

    public string? this[ApiKeyKind kind] => _keys.GetValueOrDefault(kind);

    // Commit rather than Apply: the shell is built against these keys immediately after.
    public void Save(ApiKeyKind kind, string apiKey)
    {
        var value = Blank(apiKey);
        if (value is null || value == _keys.GetValueOrDefault(kind)) return;

        _keys[kind] = value;

        var editor = _preferences?.Edit();
        editor?.PutString(NameOf(kind), value);
        editor?.Commit();
    }

    private static string NameOf(ApiKeyKind kind)
    {
        return kind is ApiKeyKind.Tmdb ? "apiKey" : "cibmediaApiKey";
    }

    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
