using Android.Content;
using CibMedia.Core.Common;

namespace CibMedia.AndroidTv.Platform;

public sealed class PreferencesUiScale
{
    private const string PreferencesName = "cibmedia.display";
    private const string Key = "uiScale";

    private readonly ISharedPreferences? _preferences;

    public PreferencesUiScale(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
    }

    // Read from disk on every get: the value is wanted in AttachBaseContext, before the
    // container exists. Committed rather than applied because the Activity is recreated as
    // soon as the setter returns and reads it straight back.
    public UiScaleStep Step
    {
        get
        {
            var stored = (UiScaleStep)(_preferences?.GetInt(Key, (int)UiScaleStep.Normal)
                                       ?? (int)UiScaleStep.Normal);

            return Enum.IsDefined(stored) ? stored : UiScaleStep.Normal;
        }
        set
        {
            var editor = _preferences?.Edit();
            editor?.PutInt(Key, (int)value);
            editor?.Commit();
        }
    }
}
