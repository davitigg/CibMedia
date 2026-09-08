using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using AndroidX.Leanback.App;
using AndroidX.Leanback.Widget;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Abstractions.LocalHttp;
using CibMedia.Core.Presentation.Setup;
using CibMedia.AndroidTv.Activities;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// Two API keys onto a box driven by a D-pad: a QR to a page this box serves on the LAN, and
// the fields below it for when there is no phone. A GuidedStepSupportFragment because it
// brings the TV text field and its IME with it.
public sealed class ApiKeysSetupFragment : GuidedStepSupportFragment
{
    private const long TmdbAction = 1;
    private const long SaveAction = 2;

    private string? _encoded;
    private IApiKeys? _keys;
    private ApiKeysSetupViewModel? _model;

    // The keys are read before the base call: GuidedStepSupportFragment builds its actions
    // from inside onCreate.
    public override void OnCreate(Bundle? savedInstanceState)
    {
        var services = AppServices.Provider;

        _keys = services.GetRequiredService<IApiKeys>();

        base.OnCreate(savedInstanceState);

        _model = new ApiKeysSetupViewModel(
            _keys,
            services.GetRequiredService<IApiKeyCheck>(),
            services.GetRequiredService<ILocalHttpServer>());

        _model.StateChanged += OnStateChanged;
    }

    // The bitmap belongs to the view: a view rebuilt under a surviving fragment gets a fresh one.
    public override void OnDestroyView()
    {
        _encoded = null;

        base.OnDestroyView();
    }

    public override void OnDestroy()
    {
        if (_model is not null)
        {
            _model.StateChanged -= OnStateChanged;
            _model.Dispose();
            _model = null;
        }

        base.OnDestroy();
    }

    // Bound to the foreground rather than to this fragment's existence: minimising leaves the
    // Activity alive, and a TV must not go on listening on a port nobody is looking at.
    public override void OnResume()
    {
        base.OnResume();

        _ = _model?.StartAsync();
    }

    public override void OnPause()
    {
        _ = _model?.StopAsync();

        base.OnPause();
    }

    // Without this the fragment inflates against the app theme, which carries none of the
    // guided step styles.
    public override int OnProvideTheme()
    {
        return ResourceConstant.Style.Theme_CibMedia_GuidedStep;
    }

    public override GuidanceStylist.Guidance OnCreateGuidance(Bundle? savedInstanceState)
    {
        return new GuidanceStylist.Guidance(
            GetString(ResourceConstant.String.setup_title),
            GetString(ResourceConstant.String.setup_description),
            GetString(ResourceConstant.String.app_name),
            null);
    }

    public override void OnCreateActions(IList<GuidedAction>? actions, Bundle? savedInstanceState)
    {
        if (actions is null) return;

        var context = RequireContext();

        Add(actions, Field(context, TmdbAction, ApiKeyKind.Tmdb, ResourceConstant.String.setup_tmdb_label));

        var save = new GuidedAction.Builder(context);
        save.Id(SaveAction);
        save.Title(GetString(ResourceConstant.String.setup_save));
        save.Enabled(_keys?.Complete() == true);

        Add(actions, save);
    }

    public override void OnGuidedActionClicked(GuidedAction? action)
    {
        if (action?.Id == SaveAction) _ = _model?.SubmitAsync(Typed(TmdbAction));
    }

    // Committing the field submits once it holds a key. Focus stays on the field rather than
    // Leanback's default of opening the next one: that hands an open keyboard to a different
    // row, and on a box that ships its own IME the D-pad then drives the rows behind it.
    public override long OnGuidedActionEditedAndProceed(GuidedAction? action)
    {
        Commit();

        return GuidedAction.ActionIdCurrent;
    }

    // Leanback writes the text into the action before it reports the edit as cancelled.
    public override void OnGuidedActionEditCanceled(GuidedAction? action)
    {
        Commit();
    }

    // Save opens first and unconditionally: a verdict of "no connection" has to leave
    // something to press when the network returns.
    private void Commit()
    {
        AllowSave();

        if (Filled(TmdbAction)) _ = _model?.SubmitAsync(Typed(TmdbAction));
    }

    private static void Add(IList<GuidedAction> actions, GuidedAction.Builder builder)
    {
        if (builder.Build() is { } action) actions.Add(action);
    }

    // The stored key goes in as the edit text, so Save without touching a field submits what
    // is stored, which the view model reads as unchanged.
    private GuidedAction.Builder Field(Context context, long id, ApiKeyKind kind, int label)
    {
        var stored = _keys?[kind];

        var field = new GuidedAction.Builder(context);
        field.Id(id);
        field.Title(GetString(label));
        field.Description(GetString(StatusText(kind, ApiKeySetupStatus.Waiting)));
        field.EditTitle(stored ?? string.Empty);
        field.Editable(true);

        // An IME that capitalises the first character produces a key the service refuses.
        field.EditInputType(
            (int)(InputTypes.ClassText | InputTypes.TextFlagNoSuggestions | InputTypes.TextVariationVisiblePassword));

        return field;
    }

    private void ShowStatus(long id, ApiKeyKind kind, ApiKeySetupStatus status)
    {
        if (FindActionById(id) is not { } action) return;

        var text = GetString(StatusText(kind, status));
        if (action.Description == text) return;

        action.Description = text;
        NotifyActionChanged(FindActionPositionById(id));
    }

    // Before an attempt, the line says whether the key is already on the box, or where to get one.
    private int StatusText(ApiKeyKind kind, ApiKeySetupStatus status)
    {
        return status switch
        {
            ApiKeySetupStatus.Checking => ResourceConstant.String.setup_key_checking,
            ApiKeySetupStatus.Rejected => ResourceConstant.String.setup_key_rejected,
            ApiKeySetupStatus.Unreachable => ResourceConstant.String.setup_key_unreachable,
            ApiKeySetupStatus.Saved => ResourceConstant.String.setup_key_set,
            _ when _keys?.Has(kind) == true => ResourceConstant.String.setup_key_set,
            _ => kind is ApiKeyKind.Tmdb
                ? ResourceConstant.String.setup_tmdb_hint
                : ResourceConstant.String.setup_cibmedia_hint
        };
    }

    private void AllowSave()
    {
        var filled = Filled(TmdbAction);

        if (FindActionById(SaveAction) is not { } save || save.Enabled == filled) return;

        save.Enabled = filled;
        NotifyActionChanged(FindActionPositionById(SaveAction));
    }

    private bool Filled(long id)
    {
        return !string.IsNullOrWhiteSpace(Typed(id));
    }

    private string? Typed(long id)
    {
        return FindActionById(id)?.EditTitle;
    }

    private void OnStateChanged()
    {
        Activity?.RunOnUiThread(Render);
    }

    private void Render()
    {
        if (_model is not { State: { } state } || !IsAdded || Context is not { } context) return;

        if (state.Complete)
        {
            (Activity as IKeySetupHost)?.OnKeysSaved();
            return;
        }

        ShowStatus(TmdbAction, ApiKeyKind.Tmdb, state.Tmdb);
        // Keyed on the address, because every status change renders and encoding a QR is not
        // work to repeat. Only ever set: leaving the screen nulls the address while the activity
        // is still animating out. The stylist hides the icon view when the Guidance carried no
        // icon, so visibility is set as well as the bitmap.
        if (GuidanceStylist?.IconView is { } icon && state.Address is { } address && address != _encoded)
        {
            _encoded = address;

            icon.SetImageBitmap(
                QrCode.Render(
                    address,
                    Resources!.GetDimensionPixelSize(ResourceConstant.Dimension.setup_qr_size),
                    Tokens.TextPrimary(context),
                    Color.Transparent));

            icon.Visibility = ViewStates.Visible;

            if (GuidanceStylist.DescriptionView is { } description)
                description.Text = GetString(ResourceConstant.String.setup_description_handoff);
        }
    }
}
