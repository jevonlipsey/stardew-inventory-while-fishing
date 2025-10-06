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
        // expose config and monitor for harmony patch
        internal static ModConfig Config = null!;
        internal static IMonitor StaticMonitor = null!;

        // determines if time should pass in menus
        internal static bool ShouldUnfreezeTime = false;

        private bool itsStardewTimeLoaded = false;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            StaticMonitor = this.Monitor;

            // apply all harmony patches in this assembly
            new Harmony(this.ModManifest.UniqueID).PatchAll();

            // subscribe to game events
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;

            // check for compatibility with ist
            itsStardewTimeLoaded = helper.ModRegistry.IsLoaded("ItsStardewTime");
            Monitor.Log("Inventory While Fishing loaded.", LogLevel.Info);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // set up gmcm
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

        // a helper to check if a button is in a sequence
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

            // check if any inventory button was pressed
            bool openInv =
                SequenceContains(e.Pressed, SButton.E) ||
                SequenceContains(e.Pressed, SButton.Escape) ||
                SequenceContains(e.Pressed, SButton.ControllerStart) ||
                SequenceContains(e.Pressed, SButton.ControllerY) ||
                SequenceContains(e.Pressed, SButton.ControllerB);

            if (!openInv)
                return;

            // if we are fishing and no menu is open, open the game menu
            if (Game1.activeClickableMenu == null && Game1.player.CurrentTool is FishingRod { isFishing: true })
            {
                Game1.activeClickableMenu = new GameMenu();
                // suppress the input so the menu doesn't immediately close
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

            // auto-close the menu on a fish bite
            if (Config.CloseOnFishBite && Game1.player.CurrentTool is FishingRod rod && rod.isNibbling && Game1.activeClickableMenu != null)
            {
                Game1.exitActiveMenu();
                if (Config.DebugLogging)
                    Monitor.Log("Fish bite detected — closed menu.", LogLevel.Debug);
            }

            // determine if time should be unfrozen based on config and if we're fishing
            if (Game1.activeClickableMenu is GameMenu)
            {
                bool fishing = Game1.player.CurrentTool is FishingRod { isFishing: true };
                ShouldUnfreezeTime = Config.UnfreezeTimeAlways || (Config.UnfreezeTimeWhileFishing && fishing);
            }
            else
            {
                ShouldUnfreezeTime = false;
            }

            // if ist is loaded, let it handle things
            if (itsStardewTimeLoaded)
                return;

            // manually advances the game clock if time is unfrozen
            if (ShouldUnfreezeTime)
            {
                Game1.player.CanMove = true;
                Game1.player.forceTimePass = true;

                if (e.IsMultipleOf(60)) // runs once every 60 ticks (1 second)
                {
                    // calculate the next 10-minute interval
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

                    // trigger the game's 10-minute update logic
                    try { Game1.performTenMinuteClockUpdate(); }
                    catch (Exception ex)
                    {
                        StaticMonitor.Log($"Error advancing time: {ex}", LogLevel.Warn);
                    }
                }
            }
            else if (Game1.player != null)
            {
                // ensure time is frozen again if it shouldn't pass
                Game1.player.forceTimePass = false;
            }
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            // when the game menu closes, reset state
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

    // the harmony patch that targets the game's time-passing logic
    [HarmonyPatch(typeof(Game1), nameof(Game1.shouldTimePass))]
    public static class TimePatch
    {
        // this prefix runs before the original game method
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            try
            {
                if (ModEntry.ShouldUnfreezeTime)
                {
                    // set the result to 'true' (time should pass)
                    __result = true;
                    // return false to skip the original method entirely
                    return false;
                }
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor.Log($"Error in time patch: {ex}", LogLevel.Error);
            }

            // if our condition isn't met, run the original game code
            return true;
        }
    }
}