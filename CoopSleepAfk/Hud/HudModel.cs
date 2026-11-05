using Microsoft.Xna.Framework;

namespace CoopSleepAfk.Hud
{
    /// <summary>One line of the panel. The text always says the state, so colour is never the only signal.</summary>
    internal readonly record struct HudLine(string Text, Color Color);

    /// <summary>What the panel shows, rebuilt once a second so drawing it every frame costs almost nothing.</summary>
    internal sealed record HudModel(string Header, Color HeaderColor, IReadOnlyList<HudLine> Lines);
}
