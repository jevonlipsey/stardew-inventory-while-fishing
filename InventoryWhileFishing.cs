#nullable enable

using System;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using GenericModConfigMenu;

namespace InventoryWhileFishing
{
    // mod's config settings
    internal class ModConfig
    {
        public bool UnfreezeTimeAlways { get; set; } = false;
        public bool UnfreezeTimeWhileFishing { get; set; } = true;
        public bool CloseOnFishBite { get; set; } = true;
        public bool DebugLogging { get; set; } = false;
    }

    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        
        // "ghost menu" that we manage ourselves
        private GameMenu? managedMenu;

        // NEW: Keep track of the fishing rod that started the action
        private FishingRod? activeFishingRod;

        // main entry point
        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            // listen for game events
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Input.CursorMoved += this.OnCursorMoved;
            helper.Events.Input.MouseWheelScrolled += this.OnMouseWheelScrolled;
        }

        // sets up the in-game config menu
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api is null) return;

            api.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));
            
            api.AddBoolOption(this.ModManifest, 
                name: () => "Always Unfreeze Time", 
                tooltip: () => "If true, menus never pause time.", 
                getValue: () => this.Config.UnfreezeTimeAlways, 
                setValue: value => this.Config.UnfreezeTimeAlways = value);
            
            api.AddBoolOption(this.ModManifest, name: () => "Unfreeze While Fishing", tooltip: () => "If true, time stays unpaused only while fishing.", getValue: () => this.Config.UnfreezeTimeWhileFishing, setValue: value => this.Config.UnfreezeTimeWhileFishing = value);
            api.AddBoolOption(this.ModManifest, name: () => "Close Inventory on Fish Bite", tooltip: () => "Automatically closes menus when a fish bites.", getValue: () => this.Config.CloseOnFishBite, setValue: value => this.Config.CloseOnFishBite = value);
            api.AddBoolOption(this.ModManifest, name: () => "Enable Debug Logging", tooltip: () => "Logs detailed state updates for testing.", getValue: () => this.Config.DebugLogging, setValue: value => this.Config.DebugLogging = value);
        }

        // called when the player presses a button
        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            bool isMenuButtonPressed = Game1.options.menuButton.Any(b => e.Pressed.Contains(b.ToSButton()));

            // always handle closing the menu first, with no other conditions.
            if (isMenuButtonPressed && this.managedMenu != null)
            {
                this.CloseManagedMenu();
                // FIX for CS1061: Loop through pressed buttons and suppress them individually.
                foreach (var button in e.Pressed)
                {
                    this.Helper.Input.Suppress(button);
                }
                return;
            }
            
            // after checking for close, now check if we should open a menu.
            bool isFishing = Game1.player?.CurrentTool is FishingRod { isFishing: true };
            bool shouldBeActive = (this.Config.UnfreezeTimeWhileFishing && isFishing) || this.Config.UnfreezeTimeAlways;

            if (isMenuButtonPressed && this.managedMenu == null && Game1.activeClickableMenu == null && shouldBeActive)
            {
                // safety check to prevent opening the menu right when a fish bites
                if (isFishing && Game1.player?.CurrentTool is FishingRod rod && (rod.isNibbling || rod.hit)) return;

                this.managedMenu = new GameMenu();
                
                // track the fishing rod that started this, if any
                if (isFishing)
                {
                    this.activeFishingRod = Game1.player?.CurrentTool as FishingRod;
                }

                if (this.Config.DebugLogging) this.Monitor.Log("Opened managed menu.", LogLevel.Debug);
                
                // FIX for CS1061: Loop through pressed buttons and suppress them individually.
                foreach (var button in e.Pressed)
                {
                    this.Helper.Input.Suppress(button);
                }
            }
        }

        // runs 60 times per second
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            var menu = this.managedMenu;
            if (menu == null) return;

            // Logic to handle moving the fishing rod from the active slot
            if (this.activeFishingRod != null && Game1.player?.CurrentTool != this.activeFishingRod)
            {
                if (this.Config.DebugLogging) this.Monitor.Log("Player is no longer holding the active fishing rod. Forcing fishing action to end.", LogLevel.Debug);
                
                // Force the fishing action to end cleanly
                this.activeFishingRod.doneFishing(Game1.player);

                // Close the menu now that the context has changed
                this.CloseManagedMenu();
                return; // Exit early
            }
            
            // tell our ghost menu to update itself
            menu.update(Game1.currentGameTime);

            // if a fish bites, close the menu
            if (this.Config.CloseOnFishBite && Game1.player?.CurrentTool is FishingRod rod && rod.isFishing && (rod.isNibbling || rod.hit))
            {
                this.CloseManagedMenu();
                if (this.Config.DebugLogging) this.Monitor.Log("Fish bite detected — closing managed menu.", LogLevel.Debug);
            }
        }

        // draws to screen
        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            this.managedMenu?.draw(e.SpriteBatch);
        }
        
        // the next three methods manually feed player input to our ghost menu

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            var menu = this.managedMenu;
            if (menu != null)
            {
                var point = e.Cursor.ScreenPixels.ToPoint();
                // handle clicks
                if (e.Button.IsActionButton() || e.Button.IsUseToolButton())
                {
                    menu.receiveLeftClick(point.X, point.Y, playSound: true);
                }
                // handle key presses
                if (Enum.TryParse<Keys>(e.Button.ToString(), true, out Keys xnaKey))
                {
                    menu.receiveKeyPress(xnaKey);
                }
                this.Helper.Input.Suppress(e.Button);
            }
        }

        private void OnCursorMoved(object? sender, CursorMovedEventArgs e)
        {
            var menu = this.managedMenu;
            if (menu != null)
            {
                // handle mouse hover effects
                var point = e.NewPosition.ScreenPixels.ToPoint();
                menu.performHoverAction(point.X, point.Y);
            }
        }

        private void OnMouseWheelScrolled(object? sender, MouseWheelScrolledEventArgs e)
        {
            var menu = this.managedMenu;
            if (menu != null)
            {
                // handle scrolling
                menu.receiveScrollWheelAction(e.Delta);
            }
        }

        // safely closes and cleans up our managed menu
        private void CloseManagedMenu()
        {
            var menu = this.managedMenu;
            if (menu != null)
            {
                // use reflection to call the game's internal cleanup method
                this.Helper.Reflection
                    .GetMethod(menu, "cleanupBeforeExit")
                    .Invoke();

                this.managedMenu = null;
                
                // Clear the tracked fishing rod
                this.activeFishingRod = null;

                if (this.Config.DebugLogging) this.Monitor.Log("Closed managed menu.", LogLevel.Debug);
            }
        }
    }
}