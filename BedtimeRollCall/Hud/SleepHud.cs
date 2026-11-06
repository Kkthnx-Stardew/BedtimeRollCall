using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace BedtimeRollCall.Hud
{
    /// <summary>Draws the sleep panel under the clock. Fonts and the pixel texture are the game's own, nothing is loaded per frame.</summary>
    internal static class SleepHud
    {
        private const int Padding = 16;
        private const int LineGap = 2;
        private const int ScreenMargin = 16;

        // the game sorts the HUD by layer depth, and its tooltip box is drawn at about 0.8, so the text must sit above that
        private const float TextLayer = 0.95f;

        public static void Draw(SpriteBatch batch, HudModel model, ModConfig config)
        {
            var font = Game1.smallFont;
            var box = Game1.dayTimeMoneyBox;
            if (font is null || box is null)
                return;

            float scale = config.HudScale;
            Measure(model, font, scale);
            var sizes = model.RowSizes!;
            float width = model.PanelSize.X, height = model.PanelSize.Y;

            var safe = Game1.game1.GraphicsDevice.Viewport.TitleSafeArea;
            float x, y;
            if (config.PanelPosition == PanelPosition.UnderClock)
            {
                // right edge of the clock box, below the journal button and the new quest arrow that hangs under it (a 16 pixel arrow drawn at 4x, 8 pixels below the button)
                x = box.xPositionOnScreen + DayTimeMoneyBox.width - width;
                y = box.questButton.bounds.Bottom + 8 + 64 + ScreenMargin;
            }
            else
            {
                x = safe.Left + ScreenMargin;
                y = safe.Top + ScreenMargin;
            }

            // the offsets, then always fully on screen
            x = Math.Clamp(x + config.PanelOffsetX, ScreenMargin, Math.Max(ScreenMargin, Game1.uiViewport.Width - width - ScreenMargin));
            y = Math.Clamp(y + config.PanelOffsetY, ScreenMargin, Math.Max(ScreenMargin, Game1.uiViewport.Height - height - ScreenMargin));

            // the same box the game draws its tooltips in, so the panel looks like it belongs in the game
            IClickableMenu.drawTextureBox(batch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), (int)x, (int)y, (int)width, (int)height, Color.White, 1f, true);

            float cursor = y + Padding;
            Utility.drawTextWithShadow(batch, model.Header, font, new Vector2(x + Padding, cursor), model.HeaderColor, scale, TextLayer);
            cursor += sizes[0].Y + LineGap;
            for (int i = 0; i < model.Lines.Count; i++)
            {
                Utility.drawTextWithShadow(batch, model.Lines[i].Text, font, new Vector2(x + Padding, cursor), model.Lines[i].Color, scale, TextLayer);
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
