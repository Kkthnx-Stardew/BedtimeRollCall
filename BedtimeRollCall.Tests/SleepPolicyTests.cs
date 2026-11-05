using BedtimeRollCall.Core;
using Xunit;

namespace BedtimeRollCall.Tests
{
    public class SleepPolicyTests
    {
        private static SleepContext Open() => new(
            IsMultiplayer: true, NewDayStarting: false, InEvent: false, IsFestival: false,
            InDungeon: false, Sleeping: false, PlayerFree: true, HasBed: true);

        [Fact]
        public void AnOpenSituationIsAllowed() => Assert.Equal(SleepBlock.None, SleepPolicy.Check(Open()));

        [Theory]
        [InlineData(nameof(SleepContext.IsMultiplayer), SleepBlock.NotMultiplayer)]
        [InlineData(nameof(SleepContext.NewDayStarting), SleepBlock.NewDay)]
        [InlineData(nameof(SleepContext.InEvent), SleepBlock.Event)]
        [InlineData(nameof(SleepContext.IsFestival), SleepBlock.Festival)]
        [InlineData(nameof(SleepContext.InDungeon), SleepBlock.Dungeon)]
        [InlineData(nameof(SleepContext.Sleeping), SleepBlock.AlreadyInBed)]
        [InlineData(nameof(SleepContext.PlayerFree), SleepBlock.Busy)]
        [InlineData(nameof(SleepContext.HasBed), SleepBlock.NoBed)]
        public void EachConditionBlocksOnItsOwn(string field, SleepBlock expected)
        {
            var c = Open();
            // flip one flag away from its open value
            c = field switch
            {
                nameof(SleepContext.IsMultiplayer) => c with { IsMultiplayer = false },
                nameof(SleepContext.NewDayStarting) => c with { NewDayStarting = true },
                nameof(SleepContext.InEvent) => c with { InEvent = true },
                nameof(SleepContext.IsFestival) => c with { IsFestival = true },
                nameof(SleepContext.InDungeon) => c with { InDungeon = true },
                nameof(SleepContext.Sleeping) => c with { Sleeping = true },
                nameof(SleepContext.PlayerFree) => c with { PlayerFree = false },
                _ => c with { HasBed = false }
            };
            Assert.Equal(expected, SleepPolicy.Check(c));
        }

        [Fact]
        public void AnEventOutranksAMenu()
        {
            // an event also makes the player not free, and the event is the more useful reason to show
            var c = Open() with { InEvent = true, PlayerFree = false };
            Assert.Equal(SleepBlock.Event, SleepPolicy.Check(c));
        }

        [Fact]
        public void EveryBlockHasATranslationKeyAndNoneHasNoKey()
        {
            foreach (var block in Enum.GetValues<SleepBlock>())
            {
                if (block == SleepBlock.None)
                    Assert.Equal("", SleepPolicy.ReasonKey(block));
                else
                    Assert.NotEqual("", SleepPolicy.ReasonKey(block));
            }
        }

        [Fact]
        public void AutoSleepNeedsEveryCondition()
        {
            Assert.True(SleepPolicy.ShouldAutoSleep(true, true, 1, false, SleepBlock.None));
            Assert.False(SleepPolicy.ShouldAutoSleep(false, true, 1, false, SleepBlock.None));
            Assert.False(SleepPolicy.ShouldAutoSleep(true, false, 1, false, SleepBlock.None));
            Assert.False(SleepPolicy.ShouldAutoSleep(true, true, 0, false, SleepBlock.None));
            Assert.False(SleepPolicy.ShouldAutoSleep(true, true, 1, true, SleepBlock.None));
            Assert.False(SleepPolicy.ShouldAutoSleep(true, true, 1, false, SleepBlock.Event));
        }
    }
}
