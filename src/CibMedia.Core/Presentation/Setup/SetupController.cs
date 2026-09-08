using CibMedia.Core.Abstractions;
using CibMedia.Core.Abstractions.LocalHttp;

namespace CibMedia.Core.Presentation.Setup;

// The page on one verb and the keys on the other.
public sealed class SetupController : LocalHttpController
{
    public const string Path = "/setup";

    public SetupController(Action<string?> onKeyPosted)
    {
        Get(Path, (_, _) => Task.FromResult(LocalHttpResponse.Html(SetupPage.Html)));

        // Answered before the key is checked: a good one closes the screen serving this page,
        // so a reply waiting on the verdict is one the phone may never get.
        Post(Path, (request, _) =>
        {
            var tmdb = request.Form(Field(ApiKeyKind.Tmdb));

            if (tmdb is null) return Task.FromResult(LocalHttpResponse.BadRequest);

            onKeyPosted(tmdb);

            return Task.FromResult(LocalHttpResponse.Accepted);
        });
    }

    // The form field name Setup.html posts under.
    private static string Field(ApiKeyKind kind)
    {
        return kind is ApiKeyKind.Tmdb ? "tmdb" : throw new ArgumentOutOfRangeException(nameof(kind));
    }
}
