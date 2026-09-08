using Android.Content;
using CibMedia.Core.Catalog;
using CibMedia.Core.Playback;
using CibMedia.AndroidTv.App;

namespace CibMedia.AndroidTv.Playback;

// Everything the player is launched with. The caller hands across what the continue-watching
// card needs, so the player never fetches details it was opened from.
public sealed record PlaybackArgs(
    PlaybackTarget Target,
    string Title,
    string? PosterUrl = null,
    string? BackdropUrl = null,
    int? Year = null,
    double? Rating = null,
    long ResumePositionMs = 0,
    string? Provider = null)
{
    public string Heading => Target is TitleTarget { SeasonNumber: { } season, EpisodeNumber: { } episode }
        ? $"{Title} {Core.Catalog.Episode.LabelFor(season, episode)}"
        : Title;

    public static PlaybackArgs For(
        PlaybackTarget target, MovieDetails? movie, long resumePositionMs = 0, string? provider = null)
    {
        return new PlaybackArgs(
            target,
            movie?.Title ?? string.Empty,
            movie?.PosterUrl,
            movie?.BackdropUrl,
            movie?.Year,
            movie?.Rating,
            resumePositionMs,
            provider);
    }

    public static PlaybackArgs For(
        PlaybackTarget target, TvShowDetails? show, long resumePositionMs = 0, string? provider = null)
    {
        return new PlaybackArgs(
            target,
            show?.Title ?? string.Empty,
            show?.PosterUrl,
            show?.BackdropUrl,
            show?.FirstAirYear,
            show?.Rating,
            resumePositionMs,
            provider);
    }

    public static PlaybackArgs? ReadFrom(Intent? intent)
    {
        if (TargetIn(intent) is not { } target) return null;

        return new PlaybackArgs(
            target,
            intent!.GetStringExtra(IntentExtras.Title) ?? string.Empty,
            intent.GetStringExtra(IntentExtras.PosterUrl),
            intent.GetStringExtra(IntentExtras.BackdropUrl),
            IntentExtras.GetOptionalInt(intent, IntentExtras.Year),
            IntentExtras.GetOptionalDouble(intent, IntentExtras.Rating),
            intent.GetLongExtra(IntentExtras.ResumePositionMs, 0),
            intent.GetStringExtra(IntentExtras.Provider));
    }

    public void WriteTo(Intent intent)
    {
        switch (Target)
        {
            case TitleTarget title:
                IntentExtras.PutMediaId(intent, title.Id);

                if (title.SeasonNumber is { } season) intent.PutExtra(IntentExtras.Season, season);
                if (title.EpisodeNumber is { } episode) intent.PutExtra(IntentExtras.Episode, episode);

                break;

            case LiveballTarget liveball:
                intent.PutExtra(IntentExtras.LiveballUrl, liveball.Url);

                break;
        }

        intent.PutExtra(IntentExtras.Title, Title);

        if (PosterUrl is not null) intent.PutExtra(IntentExtras.PosterUrl, PosterUrl);
        if (BackdropUrl is not null) intent.PutExtra(IntentExtras.BackdropUrl, BackdropUrl);
        if (Year is { } year) intent.PutExtra(IntentExtras.Year, year);
        if (Rating is { } rating) intent.PutExtra(IntentExtras.Rating, rating);
        if (ResumePositionMs > 0) intent.PutExtra(IntentExtras.ResumePositionMs, ResumePositionMs);
        if (Provider is not null) intent.PutExtra(IntentExtras.Provider, Provider);
    }

    // Null for a liveball url: the continue-watching rail has no card that could take you
    // back to one.
    public WatchProgress? ToProgress(long positionMs, long durationMs)
    {
        if (Target is not TitleTarget title) return null;

        return new WatchProgress(
            title.Id,
            Title,
            PosterUrl,
            BackdropUrl,
            Year,
            Rating,
            title.SeasonNumber,
            title.EpisodeNumber,
            positionMs,
            durationMs,
            DateTimeOffset.UtcNow);
    }

    private static PlaybackTarget? TargetIn(Intent? intent)
    {
        if (intent?.GetStringExtra(IntentExtras.LiveballUrl) is { Length: > 0 } url) return new LiveballTarget(url);

        if (IntentExtras.GetMediaId(intent) is not { } id) return null;

        var season = intent!.GetIntExtra(IntentExtras.Season, -1);
        var episode = intent.GetIntExtra(IntentExtras.Episode, -1);

        return new TitleTarget(id, season < 0 ? null : season, episode < 0 ? null : episode);
    }
}
