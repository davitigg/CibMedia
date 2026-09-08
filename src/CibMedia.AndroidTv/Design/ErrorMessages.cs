using _Microsoft.Android.Resource.Designer;
using Android.Content;
using CibMedia.Core.Common;

namespace CibMedia.AndroidTv.Design;

public static class ErrorMessages
{
    public static string For(Context context, AppError error)
    {
        return error.Kind switch
        {
            AppErrorKind.Offline => context.GetString(ResourceConstant.String.state_offline),
            AppErrorKind.Timeout => context.GetString(ResourceConstant.String.state_timeout),
            AppErrorKind.NotFound => context.GetString(ResourceConstant.String.state_not_found),
            _ => context.GetString(ResourceConstant.String.state_error)
        };
    }
}
