namespace CibMedia.Core.Presentation.Browse;

// The union of both sections' rails. Movies and TV shows share the ones whose genres mean
// the same thing on both sides and each keep the ones that only exist for them.
public enum BrowseRail
{
    TrendingWeek,
    ActionAdventure,
    Comedy,
    Drama,
    CrimeThriller,
    CrimeMystery,
    SciFiFantasy,
    Horror,
    Romance,
    FamilyAnimation,
    AnimationKids,
    RealityTalk
}
