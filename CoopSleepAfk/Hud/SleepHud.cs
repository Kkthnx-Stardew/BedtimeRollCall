using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace CoopSleepAfk.Hud
{
    /// <summary>Draws the sleep panel under the clock. Fonts and the pixel texture are the game's own, nothing is loaded per frame.</summary>
    internal static class SleepHud
    {
        private const int Padding = 10;
        private const int LineGap = 4;
        private const int ScreenMargin = 8;

        public static void Draw(SpriteBatch batch, HudModel model, float scale)
        {
            var font = Game1.smallFont;
            var box = Game1.dayTimeMoneyBox;
            if (font is null || box is null)
                return;

            var sizes = new List<Vector2> { font.MeasureString(model.Header) * scale };
            foreach (var line in model.Lines)
                sizes.Add(font.MeasureString(line.Text) * scale);

            float width = sizes.Max(s => s.X) + Padding * 2;
            float height = sizes.Sum(s => s.Y) + LineGap * (sizes.Count - 1) + Padding * 2;

            // right edge of the clock box, below it, and always fully on screen
            float x = Math.Min(box.xPositionOnScreen + DayTimeMoneyBox.width, Game1.uiViewport.Width - ScreenMargin) - width;
            float y = Math.Min(box.yPositionOnScreen + DayTimeMoneyBox.height + ScreenMargin, Game1.uiViewport.Height - height - ScreenMargin);
            x = Math.Max(x, ScreenMargin);

            batch.Draw(Game1.staminaRect, new Rectangle((int)x, (int)y, (int)width, (int)height), Color.Black * 0.6f);

            float cursor = y + Padding;
            Utility.drawTextWithShadow(batch, model.Header, font, new Vector2(x + Padding, cursor), model.HeaderColor, scale);
            cursor += sizes[0].Y + LineGap;
            for (int i = 0; i < model.Lines.Count; i++)
            {
                Utility.drawTextWithShadow(batch, model.Lines[i].Text, font, new Vector2(x + Padding, cursor), model.Lines[i].Color, scale);
                cursor += sizes[i + 1].Y + LineGap;
            }
        }
    }
}
