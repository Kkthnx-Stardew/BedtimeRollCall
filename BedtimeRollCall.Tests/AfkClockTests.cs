using BedtimeRollCall.Core;

namespace BedtimeRollCall.Tests
{
    public class AfkClockTests
    {
        [Fact]
        public void Idle_time_adds_up_while_the_player_is_free()
        {
            var clock = new AfkClock();
            clock.Advance(1, counting: true);
            clock.Advance(1, counting: true);
            Assert.Equal(2, clock.IdleSeconds);
        }

        [Fact]
        public void Time_does_not_count_while_the_player_is_busy_and_is_not_reset_either()
        {
            var clock = new AfkClock();
            clock.Advance(100, counting: true);
            clock.Advance(500, counting: false);
            Assert.Equal(100, clock.IdleSeconds);
        }

        [Fact]
        public void Any_activity_starts_the_count_again()
        {
            var clock = new AfkClock();
            clock.Advance(290, counting: true);
            clock.NoteActivity();
            Assert.Equal(0, clock.IdleSeconds);
            Assert.False(clock.IsAfk(300));
        }

        [Theory]
        [InlineData(299, false)]
        [InlineData(300, true)]
        [InlineData(301, true)]
        public void A_player_is_idle_from_the_threshold_on(double idle, bool expected)
        {
            var clock = new AfkClock();
            clock.Advance(idle, counting: true);
            Assert.Equal(expected, clock.IsAfk(300));
        }

        [Fact]
        public void A_threshold_of_zero_turns_idle_tracking_off()
        {
            var clock = new AfkClock();
            clock.Advance(100000, counting: true);
            Assert.False(clock.IsAfk(0));
            Assert.False(clock.InWarning(0, 60));
        }

        [Theory]
        [InlineData(239, false)]
        [InlineData(240, true)]
        [InlineData(299, true)]
        [InlineData(300, false)]
        public void The_warning_covers_only_the_last_seconds_before_the_threshold(double idle, bool expected)
        {
            var clock = new AfkClock();
            clock.Advance(idle, counting: true);
            Assert.Equal(expected, clock.InWarning(300, 60));
        }

        [Fact]
        public void Negative_time_is_ignored()
        {
            var clock = new AfkClock();
            clock.Advance(-5, counting: true);
            Assert.Equal(0, clock.IdleSeconds);
        }

        [Fact]
        public void Seconds_until_idle_never_goes_below_zero()
        {
            var clock = new AfkClock();
            clock.Advance(400, counting: true);
            Assert.Equal(0, clock.SecondsUntilAfk(300));
            clock.NoteActivity();
            Assert.Equal(300, clock.SecondsUntilAfk(300));
        }
    }
}
