using CibMedia.Core.Catalog;
using CibMedia.Core.Common;
using CibMedia.Core.Presentation.Home;
using CibMedia.Core.Presentation.Rails;
using CibMedia.Core.Tests.Fakes;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class ViewModelNotificationTests
{
    // The observer is serialised the way StateBinding's RunOnUiThread serialises the real one.
    [Fact]
    public async Task State_never_goes_backwards_for_a_serialized_observer()
    {
        using var vm = new HomeViewModel(new FakeCatalog(TimeSpan.FromMilliseconds(5)), new FakePlaybackHistory());

        var railCount = Enum.GetValues<HomeRail>().Length;
        var gate = new Lock();
        var lastSeen = 0;
        var regressions = 0;

        vm.StateChanged += () =>
        {
            lock (gate)
            {
                var seen = ReadyRails(vm.State);
                if (seen < lastSeen) regressions++;
                lastSeen = seen;
            }
        };

        await vm.LoadAsync();

        Assert.Equal(0, regressions);
        Assert.Equal(railCount, lastSeen);
        Assert.Equal(railCount, ReadyRails(vm.State));
    }

    private static int ReadyRails(RailsState<HomeRail> state)
    {
        return Enum.GetValues<HomeRail>()
            .Count(rail => state.For(rail) is Load<IReadOnlyList<MediaCard>>.Ready);
    }
}
