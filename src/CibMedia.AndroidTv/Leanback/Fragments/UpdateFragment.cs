using _Microsoft.Android.Resource.Designer;
using Android.Content;
using AndroidX.Leanback.App;
using AndroidX.Leanback.Widget;
using CibMedia.AndroidTv.App;
using CibMedia.AndroidTv.Design;
using CibMedia.AndroidTv.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace CibMedia.AndroidTv.Leanback.Fragments;

// What a published release says about itself, and the two answers to it. Declining is recorded
// against that version, so the box asks again only when a newer one is published.
public sealed class UpdateFragment : GuidedStepSupportFragment
{
    private const long InstallAction = 1;
    private const long LaterAction = 2;

    private AppUpdateCheck? _updates;

    public override void OnCreate(Bundle? savedInstanceState)
    {
        _updates = AppServices.Provider.GetRequiredService<AppUpdateCheck>();

        base.OnCreate(savedInstanceState);
    }

    public override GuidanceStylist.Guidance OnCreateGuidance(Bundle? savedInstanceState)
    {
        var offered = _updates?.Offered;

        return new GuidanceStylist.Guidance(
            GetString(ResourceConstant.String.update_title),
            offered?.Notes ?? "",
            offered is null ? "" : $"{GetString(ResourceConstant.String.app_name)} {offered.VersionName}",
            null);
    }

    public override void OnCreateActions(IList<GuidedAction>? actions, Bundle? savedInstanceState)
    {
        if (actions is null) return;

        var context = RequireContext();

        actions.Add(Choice(context, InstallAction, ResourceConstant.String.update_install));
        actions.Add(Choice(context, LaterAction, ResourceConstant.String.update_later));
    }

    private GuidedAction Choice(Context context, long id, int title)
    {
        var builder = new GuidedAction.Builder(context);
        builder.Id(id);
        builder.Title(GetString(title));

        return builder.Build()!;
    }

    public override void OnGuidedActionClicked(GuidedAction? action)
    {
        if (action?.Id == LaterAction)
        {
            _updates?.Decline();
            Activity?.Finish();
            return;
        }

        if (action?.Id == InstallAction) _ = InstallAsync();
    }

    private async Task InstallAsync()
    {
        if (Context is not { } context || _updates?.Offered is not { } offered) return;

        if (!ApkInstaller.CanInstall(context))
        {
            Toasts.Show(context, ResourceConstant.String.update_allow_installs, Android.Widget.ToastLength.Long);
            ApkInstaller.AskToAllowInstalls(context);
            return;
        }

        SetActionsEnabled(false);

        var apk = await _updates.DownloadAsync(offered).ConfigureAwait(true);

        if (!IsAdded || Context is not { } current) return;

        if (apk is null)
        {
            SetActionsEnabled(true);
            Toasts.Show(current, ResourceConstant.String.update_download_failed, Android.Widget.ToastLength.Long);
            return;
        }

        ApkInstaller.Install(current, apk);
    }

    private void SetActionsEnabled(bool enabled)
    {
        foreach (var id in new[] { InstallAction, LaterAction })
        {
            if (FindActionById(id) is { } action) action.Enabled = enabled;
        }

        GuidanceStylist?.DescriptionView?.SetText(
            enabled ? _updates?.Offered?.Notes ?? "" : GetString(ResourceConstant.String.update_downloading),
            Android.Widget.TextView.BufferType.Normal);
    }
}
