using System.Text.Json;
using BedtimeRollCall.Core;

namespace BedtimeRollCall.Tests
{
    public class TranslationTests
    {
        private static Dictionary<string, string> Load() =>
            JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "i18n", "default.json")))!;

        [Fact]
        public void EveryRefusalKeyHasText()
        {
            var texts = Load();
            foreach (var block in Enum.GetValues<SleepBlock>().Where(b => b != SleepBlock.None))
                Assert.True(texts.ContainsKey(SleepPolicy.ReasonKey(block)), $"missing text for {block}");
        }

        [Fact]
        public void NoTextIsEmpty()
        {
            foreach (var (key, value) in Load())
                Assert.False(string.IsNullOrWhiteSpace(value), $"{key} is empty");
        }

        [Fact]
        public void TextFollowsTheWritingRules()
        {
            // no dashes used as punctuation, no semicolons, no emojis
            foreach (var (key, value) in Load())
            {
                Assert.DoesNotContain('—', value);
                Assert.DoesNotContain('–', value);
                Assert.DoesNotContain(';', value);
                Assert.True(value.All(c => c < 0x2000), $"{key} has a symbol outside plain text");
            }
        }
    }
}
