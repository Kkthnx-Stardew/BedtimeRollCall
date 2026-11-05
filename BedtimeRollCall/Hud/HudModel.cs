using Microsoft.Xna.Framework;

namespace BedtimeRollCall.Hud
{
    /// <summary>One line of the panel. The text always says the state, so colour is never the only signal.</summary>
    internal readonly record struct HudLine(string Text, Color Color);

    /// <summary>
    /// What the panel shows. It is rebuilt once a second and its text is measured once, the first time it is drawn at a given size, so
    /// drawing it every frame costs a few draw calls and nothing else.
    /// </summary>
    internal sealed class HudModel
    {
        public HudModel(string header, Color headerColor, IReadOnlyList<HudLine> lines)
        {
            Header = header;
            HeaderColor = headerColor;
            Lines = lines;
        }

        public string Header { get; }
        public Color HeaderColor { get; }
        public IReadOnlyList<HudLine> Lines { get; }

        /// <summary>Height of every text row, header first, at <see cref="MeasuredScale"/>. Null until the panel is first drawn.</summary>
        public Vector2[]? RowSizes { get; set; }

        public float MeasuredScale { get; set; }
        public Vector2 PanelSize { get; set; }
    }
}
