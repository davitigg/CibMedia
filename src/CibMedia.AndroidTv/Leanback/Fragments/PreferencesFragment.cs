using _Microsoft.Android.Resource.Designer;
using Android.Content;
using AndroidX.Leanback.App;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Common;
using CibMedia.Core.Playback;
using CibMedia.AndroidTv.App;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// Two dropdowns on one guided step: the provider a title plays from and the dub it starts in.
// Each list opens with "none chosen", which means the first the API offers and the box's own
// language. Leanback moves the radio mark itself; this records the pick and names it on the
// row above the list.
public sealed class PreferencesFragment : GuidedStepSupportFragment
{
    private const long ProviderAction = 1;
    private const long LanguageAction = 2;

    // Choice ids: a list's "none" entry at the base, its entries counted from one above it.
    private const long ProviderChoice = 100;
    private const long LanguageChoice = 200;

    // The API's list, which arrives after the actions are built and fills the first dropdown in.
    private IReadOnlyList<string> _names = [];
    private bool _listing;
    private IPlaybackPreferences? _preferences;
    private IPlaybackProviders? _providers;

    // Read before the base call: GuidedStepSupportFragment builds its actions from inside onCreate.
    public override void OnCreate(Bundle? savedInstanceState)
    {
        var services = AppServices.Provider;

        _preferences = services.GetRequiredService<IPlaybackPreferences>();
        _providers = services.GetRequiredService<IPlaybackProviders>();

        base.OnCreate(savedInstanceState);

        _ = ListProvidersAsync();
    }

    // Cached for a month, so this is normally back before the screen is drawn. The first time,
    // or offline, it is not: the request goes out again when the dropdown opens empty. Leanback
    // hands an open list its entries at expand time, so an answer that lands while the list is
    // open shows the next time it opens.
    private async Task ListProvidersAsync()
    {
        if (_providers is null || _listing || _names.Count > 0) return;

        _listing = true;

        try
        {
            var listed = await Load.RunAsync(ct => _providers.NamesAsync(ct), default).ConfigureAwait(true);

            if (listed is not Load<IReadOnlyList<string>>.Ready { Value: { } names } || !IsAdded || Context is not { } context) return;

            _names = names;

            if (FindActionById(ProviderAction) is not { } action) return;

            action.SubActions = [.. ProviderChoices().Select(choice => Radio(context, choice))];
            NotifyActionChanged(FindActionPositionById(ProviderAction));
        }
        finally
        {
            _listing = false;
        }
    }

    public override int OnProvideTheme()
    {
        return ResourceConstant.Style.Theme_CibMedia_GuidedStep;
    }

    public override GuidanceStylist.Guidance OnCreateGuidance(Bundle? savedInstanceState)
    {
        return new GuidanceStylist.Guidance(
            GetString(ResourceConstant.String.preferences_title),
            GetString(ResourceConstant.String.preferences_description),
            GetString(ResourceConstant.String.app_name),
            null);
    }

    public override void OnCreateActions(IList<GuidedAction>? actions, Bundle? savedInstanceState)
    {
        if (actions is null) return;

        var context = RequireContext();
        var language = _preferences?.PreferredLanguage;

        actions.Add(Dropdown(context, ProviderAction, ResourceConstant.String.provider_default_title, ProviderChoices(), _preferences?.DefaultProvider));

        actions.Add(Dropdown(
            context,
            LanguageAction,
            ResourceConstant.String.language_default_title,
            [
                (LanguageChoice, GetString(ResourceConstant.String.language_default_device), language is null),
                .. PlaybackLanguage.Known.Select((known, index) => (LanguageChoice + index + 1, known.Name, Same(known.Code, language)))
            ],
            language));
    }

    private IReadOnlyList<(long Id, string Label, bool Lit)> ProviderChoices()
    {
        var provider = _preferences?.DefaultProvider;

        return
        [
            (ProviderChoice, GetString(ResourceConstant.String.provider_default_none), provider is null),
            .. _names.Select((name, index) => (ProviderChoice + index + 1, name, Same(name, provider)))
        ];
    }

    // Leanback pins an opened list under the row's current place and only then moves the
    // selection to that row. From a remote the row is already selected; from a mouse or touch
    // it is not, and the list lands a row too high, or off the top. Selecting first settles it.
    public override void OnGuidedActionClicked(GuidedAction? action)
    {
        if (action is null) return;

        SelectedActionPosition = FindActionPositionById(action.Id);

        if (action.Id == ProviderAction) _ = ListProvidersAsync();
    }

    // True collapses the list.
    public override bool OnSubGuidedActionClicked(GuidedAction? action)
    {
        if (action is null || _preferences is null) return true;

        if (action.Id >= LanguageChoice)
        {
            var picked = (int)(action.Id - LanguageChoice) - 1;
            _preferences.PreferredLanguage = picked < 0 ? null : PlaybackLanguage.Known[picked].Code;
            Name(LanguageAction, action);
        }
        else if (action.Id >= ProviderChoice)
        {
            var picked = (int)(action.Id - ProviderChoice) - 1;
            _preferences.DefaultProvider = picked < 0 ? null : _names[picked];
            Name(ProviderAction, action);
        }

        return true;
    }

    private void Name(long id, GuidedAction chosen)
    {
        if (FindActionById(id) is not { } action || action.Description == chosen.Title) return;

        action.Description = chosen.Title;
        NotifyActionChanged(FindActionPositionById(id));
    }

    private static bool Same(string candidate, string? chosen)
    {
        return string.Equals(candidate, chosen, StringComparison.OrdinalIgnoreCase);
    }

    // The stored value stands in as the description while the list that would name it is
    // still on its way.
    private GuidedAction Dropdown(
        Context context,
        long id,
        int title,
        IReadOnlyList<(long Id, string Label, bool Lit)> choices,
        string? stored)
    {
        var builder = new GuidedAction.Builder(context);
        builder.Id(id);
        builder.Title(GetString(title));
        builder.Description(choices.FirstOrDefault(choice => choice.Lit).Label ?? stored ?? string.Empty);
        builder.SubActions([.. choices.Select(choice => Radio(context, choice))]);

        return builder.Build()!;
    }

    private static GuidedAction Radio(Context context, (long Id, string Label, bool Lit) choice)
    {
        var builder = new GuidedAction.Builder(context);
        builder.Id(choice.Id);
        builder.Title(choice.Label);
        builder.CheckSetId(GuidedAction.DefaultCheckSetId);
        builder.Checked(choice.Lit);

        return builder.Build()!;
    }
}
