#nullable enable
using System;
using System.Collections.Generic;
using GenericModConfigMenu;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace InventoryWhileFishing
{
    internal class ModConfig
    {
        public bool UnfreezeTimeAlways { get; set; } = false;
        public bool UnfreezeTimeWhileFishing { get; set; } = true;
        public bool CloseOnFishBite { get; set; } = true;
        public bool DebugLogging { get; set; } = false;
    }

    public class ModEntry : Mod
    {
        internal static ModConfig Config = null!;
        internal static IMonitor StaticMonitor = null!;
        internal static bool ShouldUnfreezeTime = false;

        private bool itsStardewTimeLoaded = false;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            StaticMonitor = this.Monitor;

            new Harmony(this.ModManifest.UniqueID).PatchAll();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;

            itsStardewTimeLoaded = helper.ModRegistry.IsLoaded("ItsStardewTime");
            Monitor.Log("Inventory While Fishing loaded.", LogLevel.Info);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api is null) return;

            api.Register(this.ModManifest, () => Config = new ModConfig(), () => this.Helper.WriteConfig(Config));

            api.AddBoolOption(this.ModManifest,
                name: () => "Always Unfreeze Time",
                tooltip: () => "If true, menus never pause time.",
                getValue: () => Config.UnfreezeTimeAlways,
                setValue: value => Config.UnfreezeTimeAlways = value);

            api.AddBoolOption(this.ModManifest,
                name: () => "Unfreeze While Fishing",
                tooltip: () => "If true, time stays unpaused only while fishing.",
                getValue: () => Config.UnfreezeTimeWhileFishing,
                setValue: value => Config.UnfreezeTimeWhileFishing = value);

            api.AddBoolOption(this.ModManifest,
                name: () => "Close Inventory on Fish Bite",
                tooltip: () => "Automatically closes menus when a fish bites.",
                getValue: () => Config.CloseOnFishBite,
                setValue: value => Config.CloseOnFishBite = value);

            api.AddBoolOption(this.ModManifest,
                name: () => "Enable Debug Logging",
                tooltip: () => "Logs detailed state updates for testing.",
                getValue: () => Config.DebugLogging,
                setValue: value => Config.DebugLogging = value);
        }

        private static bool SequenceContains(IEnumerable<SButton>? seq, SButton button)
        {
            if (seq == null) return false;
            foreach (var b in seq)
                if (b == button)
                    return true;
            return false;
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            bool openInv =
                SequenceContains(e.Pressed, SButton.E) ||
                SequenceContains(e.Pressed, SButton.Escape) ||
                SequenceContains(e.Pressed, SButton.ControllerStart) ||
                SequenceContains(e.Pressed, SButton.ControllerY) ||
                SequenceContains(e.Pressed, SButton.ControllerB);

            if (!openInv)
                return;

            if (Game1.activeClickableMenu == null && Game1.player.CurrentTool is FishingRod { isFishing: true })
            {
                Game1.activeClickableMenu = new GameMenu();
                foreach (var b in new[] { SButton.E, SButton.Escape, SButton.ControllerStart, SButton.ControllerY, SButton.ControllerB })
                    this.Helper.Input.Suppress(b);

                if (Config.DebugLogging)
                    Monitor.Log("Opened inventory while fishing.", LogLevel.Debug);
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null)
            {
                ShouldUnfreezeTime = false;
                return;
            }

            if (Config.CloseOnFishBite && Game1.player.CurrentTool is FishingRod rod && rod.isNibbling && Game1.activeClickableMenu != null)
            {
                Game1.exitActiveMenu();
                if (Config.DebugLogging)
                    Monitor.Log("Fish bite detected — closed menu.", LogLevel.Debug);
            }

            if (Game1.activeClickableMenu is GameMenu)
            {
                bool fishing = Game1.player.CurrentTool is FishingRod { isFishing: true };
                ShouldUnfreezeTime = Config.UnfreezeTimeAlways || (Config.UnfreezeTimeWhileFishing && fishing);
            }
            else
            {
                ShouldUnfreezeTime = false;
            }

            if (itsStardewTimeLoaded)
                return; // defer to It's Stardew Time for timing control

            if (ShouldUnfreezeTime)
            {
                Game1.player.CanMove = true;
                Game1.player.forceTimePass = true;

                if (e.IsMultipleOf(60))
                {
                    int time = Game1.timeOfDay;
                    int hour = time / 100;
                    int minute = time % 100 + 10;

                    if (minute >= 60)
                    {
                        hour++;
                        minute -= 60;
                    }

                    if (hour >= 24)
                    {
                        hour = 6;
                        minute = 0;
                    }

                    Game1.timeOfDay = hour * 100 + minute;
                    try { Game1.performTenMinuteClockUpdate(); }
                    catch (Exception ex)
                    {
                        StaticMonitor.Log($"Error advancing time: {ex}", LogLevel.Warn);
                    }
                }
            }
            else if (Game1.player != null)
            {
                Game1.player.forceTimePass = false;
            }
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.OldMenu is GameMenu)
            {
                ShouldUnfreezeTime = false;
                if (Game1.player != null)
                    Game1.player.forceTimePass = false;

                if (Config.DebugLogging)
                    Monitor.Log("Menu closed, time restored to normal.", LogLevel.Trace);
            }
        }
    }

    [HarmonyPatch(typeof(Game1), nameof(Game1.shouldTimePass))]
    public static class TimePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            try
            {
                if (ModEntry.ShouldUnfreezeTime)
                {
                    __result = true;
                    return false;
                }
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor.Log($"Error in time patch: {ex}", LogLevel.Error);
            }

            return true;
        }
    }
}
