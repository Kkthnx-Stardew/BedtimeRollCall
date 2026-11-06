using BedtimeRollCall.Hud;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace BedtimeRollCall.Integration
{
    /// <summary>The part of the Generic Mod Config Menu API this mod uses. The method signatures must match the real API.</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddParagraph(IManifest mod, Func<string> text);

        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);

        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string>? formatValue = null, string? fieldId = null);

        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);

        void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    }

    /// <summary>Adds Bedtime Roll Call to the Generic Mod Config Menu, when that mod is installed.</summary>
    internal static class ConfigMenu
    {
        private const string GmcmId = "spacechase0.GenericModConfigMenu";

        public static void Register(IModHelper helper, IManifest manifest, ModConfig config, IMonitor monitor)
        {
            try
            {
                var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GmcmId);
                if (api is null)
                    return;

                string T(string key) => helper.Translation.Get(key);

                api.Register(manifest, reset: () => Reset(config), save: () =>
                {
                    config.Normalize();
                    helper.WriteConfig(config);
                });
                api.AddParagraph(manifest, () => T("config.note"));

                api.AddSectionTitle(manifest, () => T("config.section.panel"));
                api.AddBoolOption(manifest, () => config.EnableHud, v => config.EnableHud = v, () => T("config.hud.name"), () => T("config.hud.tooltip"));
                api.AddTextOption(manifest, () => config.PanelPosition.ToString(), v => config.PanelPosition = Enum.TryParse<PanelPosition>(v, out var parsed) ? parsed : PanelPosition.TopLeft,
                    () => T("config.position.name"), () => T("config.position.tooltip"),
                    allowedValues: Enum.GetNames<PanelPosition>(), formatAllowedValue: v => T("config.position." + (v == nameof(PanelPosition.UnderClock) ? "under-clock" : "top-left")));
                api.AddNumberOption(manifest, () => config.PanelOffsetX, v => config.PanelOffsetX = v, () => T("config.offset-x.name"), () => T("config.offset.tooltip"),
                    min: -600, max: 600, interval: 10);
                api.AddNumberOption(manifest, () => config.PanelOffsetY, v => config.PanelOffsetY = v, () => T("config.offset-y.name"), () => T("config.offset.tooltip"),
                    min: -600, max: 600, interval: 10);
                api.AddNumberOption(manifest, () => config.HudScale, v => config.HudScale = v, () => T("config.scale.name"), () => T("config.scale.tooltip"),
                    min: 0.8f, max: 1.4f, interval: 0.1f);

                api.AddSectionTitle(manifest, () => T("config.section.idle"));
                api.AddBoolOption(manifest, () => config.EnableAfkTracking, v => config.EnableAfkTracking = v, () => T("config.afk.name"), () => T("config.afk.tooltip"));
                api.AddNumberOption(manifest, () => config.AfkMinutes, v => config.AfkMinutes = v, () => T("config.minutes.name"), () => T("config.minutes.tooltip"),
                    min: 1, max: 30, interval: 1);
                api.AddNumberOption(manifest, () => config.WarnBeforeAfkSeconds, v => config.WarnBeforeAfkSeconds = v, () => T("config.warn.name"), () => T("config.warn.tooltip"),
                    min: 0, max: 120, interval: 5);
                api.AddBoolOption(manifest, () => config.CountCursorMovementAsActivity, v => config.CountCursorMovementAsActivity = v, () => T("config.cursor.name"), () => T("config.cursor.tooltip"));
                api.AddBoolOption(manifest, () => config.AfkCountsAsReady, v => config.AfkCountsAsReady = v, () => T("config.afk-ready.name"), () => T("config.afk-ready.tooltip"));

                api.AddSectionTitle(manifest, () => T("config.section.bed"));
                api.AddKeybindList(manifest, () => config.GoToBedKey, v => config.GoToBedKey = v, () => T("config.key.name"), () => T("config.key.tooltip"));
                api.AddBoolOption(manifest, () => config.AutoSleepWhenIdle, v => config.AutoSleepWhenIdle = v, () => T("config.auto.name"), () => T("config.auto.tooltip"));
            }
            catch (Exception ex)
            {
                // the menu is a convenience, so a failure here never affects the mod
                monitor.Log($"Could not add Bedtime Roll Call to the Generic Mod Config Menu. The settings in config.json still work. {ex.Message}", LogLevel.Warn);
            }
        }

        private static void Reset(ModConfig config)
        {
            var defaults = new ModConfig();
            config.EnableHud = defaults.EnableHud;
            config.HudScale = defaults.HudScale;
            config.PanelPosition = defaults.PanelPosition;
            config.PanelOffsetX = defaults.PanelOffsetX;
            config.PanelOffsetY = defaults.PanelOffsetY;
            config.EnableAfkTracking = defaults.EnableAfkTracking;
            config.AfkMinutes = defaults.AfkMinutes;
            config.WarnBeforeAfkSeconds = defaults.WarnBeforeAfkSeconds;
            config.CountCursorMovementAsActivity = defaults.CountCursorMovementAsActivity;
            config.AfkCountsAsReady = defaults.AfkCountsAsReady;
            config.GoToBedKey = defaults.GoToBedKey;
            config.AutoSleepWhenIdle = defaults.AutoSleepWhenIdle;
        }
    }
}
