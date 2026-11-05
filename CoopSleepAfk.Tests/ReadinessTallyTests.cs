using CoopSleepAfk.Core;

namespace CoopSleepAfk.Tests
{
    public class ReadinessTallyTests
    {
        private static TallyPlayer P(string name, BedState bed, ActivityState activity) => new(name.GetHashCode(), name, bed, activity);

        [Fact]
        public void Nobody_online_is_never_all_ready()
        {
            var tally = ReadinessTally.Compute(Array.Empty<TallyPlayer>(), afkCountsAsReady: true);
            Assert.Equal(0, tally.Total);
            Assert.False(tally.AllReady);
        }

        [Fact]
        public void Players_in_bed_count_whether_or_not_they_have_the_mod()
        {
            var tally = ReadinessTally.Compute(new[]
            {
                P("Alice", BedState.Confirmed, ActivityState.Active),
                P("Bob", BedState.InBed, ActivityState.Unknown),
                P("Carol", BedState.Out, ActivityState.Active)
            }, afkCountsAsReady: false);
            Assert.Equal(2, tally.Ready);
            Assert.Equal(3, tally.Total);
            Assert.False(tally.AllReady);
        }

        [Fact]
        public void An_idle_player_counts_only_when_the_host_allows_it()
        {
            var players = new[] { P("Alice", BedState.Confirmed, ActivityState.Active), P("Bob", BedState.Out, ActivityState.Afk) };
            Assert.Equal(1, ReadinessTally.Compute(players, afkCountsAsReady: false).Ready);
            var allowed = ReadinessTally.Compute(players, afkCountsAsReady: true);
            Assert.Equal(2, allowed.Ready);
            Assert.True(allowed.AllReady);
        }

        [Fact]
        public void An_idle_player_is_listed_as_idle_either_way()
        {
            var players = new[] { P("Bob", BedState.Out, ActivityState.Afk) };
            Assert.Equal(new[] { "Bob" }, ReadinessTally.Compute(players, afkCountsAsReady: false).Idle);
            Assert.Equal(new[] { "Bob" }, ReadinessTally.Compute(players, afkCountsAsReady: true).Idle);
        }

        [Fact]
        public void A_player_in_an_event_holds_the_night_back_and_is_never_ready()
        {
            var tally = ReadinessTally.Compute(new[]
            {
                P("Alice", BedState.Confirmed, ActivityState.Active),
                P("Dave", BedState.Out, ActivityState.InEvent)
            }, afkCountsAsReady: true);
            Assert.Equal(1, tally.Ready);
            Assert.Equal(new[] { "Dave" }, tally.Blocked);
            Assert.False(tally.AllReady);
        }

        [Fact]
        public void A_player_who_got_into_bed_during_an_event_is_not_blocked()
        {
            var tally = ReadinessTally.Compute(new[] { P("Dave", BedState.InBed, ActivityState.InEvent) }, afkCountsAsReady: false);
            Assert.Empty(tally.Blocked);
            Assert.Equal(1, tally.Ready);
        }

        [Fact]
        public void Players_without_the_mod_are_counted_as_untracked_and_never_as_idle()
        {
            var tally = ReadinessTally.Compute(new[]
            {
                P("Alice", BedState.Out, ActivityState.Unknown),
                P("Bob", BedState.Out, ActivityState.Unknown)
            }, afkCountsAsReady: true);
            Assert.Equal(2, tally.Untracked);
            Assert.Empty(tally.Idle);
            Assert.Equal(0, tally.Ready);
        }

        [Fact]
        public void Day_keys_differ_between_days()
        {
            Assert.NotEqual(DayKey.Make(1, "spring", 12), DayKey.Make(1, "spring", 13));
            Assert.Equal("Y2 fall 3", DayKey.Make(2, "fall", 3));
        }
    }
}
