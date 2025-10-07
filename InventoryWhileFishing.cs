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

            // determine if the mod's core feature should be active at all
            bool isFishing = Game1.player?.CurrentTool is FishingRod { isFishing: true };
            bool shouldBeActive = (this.Config.UnfreezeTimeWhileFishing && isFishing) || this.Config.UnfreezeTimeAlways;
            if (!shouldBeActive) return;

            // check if any menu button was pressed (works for keyboard and controller)
            bool isMenuButtonPressed = Game1.options.menuButton.Any(b => e.Pressed.Contains(b.ToSButton()));

            if (isMenuButtonPressed)
            {
                // if our menu is open, close it (toggle behavior)
                if (this.managedMenu != null)
                {
                    this.CloseManagedMenu();
                }
                // otherwise, if no other menu is open, open our menu
                else if (Game1.activeClickableMenu == null)
                {
                    // safety check to prevent opening the menu right when a fish bites - caused soft lock
                    if (isFishing && Game1.player?.CurrentTool is FishingRod rod && (rod.isNibbling || rod.hit)) return;

                    this.managedMenu = new GameMenu();
                    if (this.Config.DebugLogging) this.Monitor.Log("Opened managed menu.", LogLevel.Debug);
                }
        
                // stop the game from processing the button press again
                foreach (var button in e.Pressed)
                {
                    this.Helper.Input.Suppress(button);
                }
            }
        }

        // runs 60 times per second
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (this.managedMenu == null) return;
            
            // tell our ghost menu to update itself
            this.managedMenu.update(Game1.currentGameTime);

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
            // if our menu exists, draw it to the screen
            this.managedMenu?.draw(e.SpriteBatch);
        }
        
        // the next three methods manually feed player input to our ghost menu

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (this.managedMenu != null)
            {
                var point = e.Cursor.ScreenPixels.ToPoint();
                // handle clicks
                if (e.Button.IsActionButton() || e.Button.IsUseToolButton())
                {
                    this.managedMenu.receiveLeftClick(point.X, point.Y, playSound: true);
                }
                // handle key presses
                if (Enum.TryParse<Keys>(e.Button.ToString(), true, out Keys xnaKey))
                {
                    this.managedMenu.receiveKeyPress(xnaKey);
                }
                this.Helper.Input.Suppress(e.Button);
            }
        }

        private void OnCursorMoved(object? sender, CursorMovedEventArgs e)
        {
            if (this.managedMenu != null)
            {
                // handle mouse hover effects
                var point = e.NewPosition.ScreenPixels.ToPoint();
                this.managedMenu.performHoverAction(point.X, point.Y);
            }
        }

        private void OnMouseWheelScrolled(object? sender, MouseWheelScrolledEventArgs e)
        {
            if (this.managedMenu != null)
            {
                // handle scrolling
                this.managedMenu?.receiveScrollWheelAction(e.Delta);
            }
        }

        // safely closes and cleans up our managed menu
        private void CloseManagedMenu()
        {
            if (this.managedMenu != null)
            {
                // use reflection to call the game's internal cleanup method
                this.Helper.Reflection
                    .GetMethod(this.managedMenu, "cleanupBeforeExit")
                    .Invoke();

                this.managedMenu = null;
                if (this.Config.DebugLogging) this.Monitor.Log("Closed managed menu.", LogLevel.Debug);
            }
        }
    }
}