using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace BedtimeRollCall.Hud
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

            Measure(model, font, scale);
            var sizes = model.RowSizes!;
            float width = model.PanelSize.X, height = model.PanelSize.Y;

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

        /// <summary>Measures every row once per size. A new model, or a changed scale, measures again.</summary>
        private static void Measure(HudModel model, SpriteFont font, float scale)
        {
            if (model.RowSizes is not null && model.MeasuredScale == scale)
                return;

            var sizes = new Vector2[model.Lines.Count + 1];
            sizes[0] = font.MeasureString(model.Header) * scale;
            float widest = sizes[0].X, tall = sizes[0].Y;
            for (int i = 0; i < model.Lines.Count; i++)
            {
                sizes[i + 1] = font.MeasureString(model.Lines[i].Text) * scale;
                widest = Math.Max(widest, sizes[i + 1].X);
                tall += sizes[i + 1].Y;
            }

            model.RowSizes = sizes;
            model.MeasuredScale = scale;
            model.PanelSize = new Vector2(widest + Padding * 2, tall + LineGap * (sizes.Length - 1) + Padding * 2);
        }
    }
}
