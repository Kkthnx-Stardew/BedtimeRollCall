using BedtimeRollCall.Core;

namespace BedtimeRollCall.Tests
{
    public class PeerActivityBookTests
    {
        private const string Today = "Y1 spring 12";

        [Fact]
        public void A_player_nothing_was_heard_from_is_unknown()
        {
            var book = new PeerActivityBook();
            Assert.Equal((ActivityState.Unknown, 0), book.Get(7, now: 10, Today, maxAgeSeconds: 30));
            Assert.False(book.HasHeardFrom(7));
        }

        [Fact]
        public void A_fresh_report_from_today_is_used_as_it_was_sent()
        {
            var book = new PeerActivityBook();
            book.Update(7, ActivityState.Afk, 310, Today, now: 100);
            Assert.Equal((ActivityState.Afk, 310), book.Get(7, now: 110, Today, maxAgeSeconds: 30));
        }

        [Fact]
        public void A_report_that_is_too_old_counts_for_nothing()
        {
            var book = new PeerActivityBook();
            book.Update(7, ActivityState.Active, 0, Today, now: 100);
            Assert.Equal(ActivityState.Unknown, book.Get(7, now: 131, Today, maxAgeSeconds: 30).State);
            Assert.Equal(ActivityState.Active, book.Get(7, now: 130, Today, maxAgeSeconds: 30).State);
        }

        [Fact]
        public void A_report_from_another_day_counts_for_nothing()
        {
            var book = new PeerActivityBook();
            book.Update(7, ActivityState.Afk, 400, "Y1 spring 11", now: 100);
            Assert.Equal(ActivityState.Unknown, book.Get(7, now: 101, Today, maxAgeSeconds: 30).State);
        }

        [Fact]
        public void A_newer_report_replaces_the_older_one()
        {
            var book = new PeerActivityBook();
            book.Update(7, ActivityState.Afk, 400, Today, now: 100);
            book.Update(7, ActivityState.Active, 0, Today, now: 105);
            Assert.Equal(ActivityState.Active, book.Get(7, now: 106, Today, maxAgeSeconds: 30).State);
        }

        [Fact]
        public void A_player_who_left_is_forgotten()
        {
            var book = new PeerActivityBook();
            book.Update(7, ActivityState.Active, 0, Today, now: 100);
            book.Remove(7);
            Assert.False(book.HasHeardFrom(7));
            book.Update(8, ActivityState.Active, 0, Today, now: 100);
            book.Clear();
            Assert.False(book.HasHeardFrom(8));
        }
    }
}
