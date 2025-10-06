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
    internal class ModConfig
    {
        public bool UnfreezeTimeWhileFishing { get; set; } = true;
        public bool CloseOnFishBite { get; set; } = true;
        public bool DebugLogging { get; set; } = false;
    }

    public class ModEntry : Mod
    {
        private ModConfig Config = null!;
        private GameMenu? managedMenu;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Input.CursorMoved += this.OnCursorMoved;
            helper.Events.Input.MouseWheelScrolled += this.OnMouseWheelScrolled;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api is null) return;

            api.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));
            api.AddBoolOption(this.ModManifest, name: () => "Unfreeze While Fishing", tooltip: () => "If true, time stays unpaused only while fishing.", getValue: () => this.Config.UnfreezeTimeWhileFishing, setValue: value => this.Config.UnfreezeTimeWhileFishing = value);
            api.AddBoolOption(this.ModManifest, name: () => "Close Inventory on Fish Bite", tooltip: () => "Automatically closes menus when a fish bites.", getValue: () => this.Config.CloseOnFishBite, setValue: value => this.Config.CloseOnFishBite = value);
            api.AddBoolOption(this.ModManifest, name: () => "Enable Debug Logging", tooltip: () => "Logs detailed state updates for testing.", getValue: () => this.Config.DebugLogging, setValue: value => this.Config.DebugLogging = value);
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady || !this.Config.UnfreezeTimeWhileFishing) return;

            bool isMenuButtonPressed = Game1.options.menuButton.Any(b => e.Pressed.Contains(b.ToSButton()));

            if (isMenuButtonPressed)
            {
                if (this.managedMenu != null)
                {
                    this.CloseManagedMenu();
                }
                else if (Game1.activeClickableMenu == null && Game1.player.CurrentTool is FishingRod rod && rod.isFishing)
                {
                    if (rod.isNibbling || rod.hit) return;

                    this.managedMenu = new GameMenu();
                    if (this.Config.DebugLogging) this.Monitor.Log("Opened managed menu.", LogLevel.Debug);
                }

        
                foreach (var button in e.Pressed)
                {
                    this.Helper.Input.Suppress(button);
                }
            }
        }


        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (this.managedMenu == null) return;
            this.managedMenu.update(Game1.currentGameTime);

            if (this.Config.CloseOnFishBite && Game1.player.CurrentTool is FishingRod rod && (rod.isNibbling || rod.hit))
            {
                this.CloseManagedMenu();
                if (this.Config.DebugLogging) this.Monitor.Log("Fish bite detected — closing managed menu.", LogLevel.Debug);
            }
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            this.managedMenu?.draw(e.SpriteBatch);
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (this.managedMenu != null)
            {
                var point = e.Cursor.ScreenPixels.ToPoint();
                if (e.Button.IsActionButton() || e.Button.IsUseToolButton())
                {
                    this.managedMenu.receiveLeftClick(point.X, point.Y, playSound: true);
                }
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
                var point = e.NewPosition.ScreenPixels.ToPoint();
                this.managedMenu.performHoverAction(point.X, point.Y);
            }
        }

        private void OnMouseWheelScrolled(object? sender, MouseWheelScrolledEventArgs e)
        {
            this.managedMenu?.receiveScrollWheelAction(e.Delta);
        }

        private void CloseManagedMenu()
        {
            if (this.managedMenu != null)
            {
                this.Helper.Reflection
                    .GetMethod(this.managedMenu, "cleanupBeforeExit")
                    .Invoke();

                this.managedMenu = null;
                if (this.Config.DebugLogging) this.Monitor.Log("Closed managed menu.", LogLevel.Debug);
            }
        }
    }
}