# Inventory While Fishing

A **Stardew Valley** mod that lets you open and manage your inventory, map, and other menus **while fishing**, without freezing the game. Perfect for multitaskers or players using auto-fishing mods who want time to continue while waiting for a bite.

---

## Features

* Open the **inventory, map, skills**, and other menus while fishing. Good for organizing, especially with [Chests Anywhere](https://www.nexusmods.com/stardewvalley/mods/518).
* **Time continues** naturally instead of pausing.
* **Menus close automatically** when a fish bites. Good for autofishing mods.
* **Customizable** through **Generic Mod Config Menu**. Choose whether time always passes in menus or only while fishing. Also, choose if the menu automatically closes on a fish bite or not.
* Keyboard and Gamepad support.
* Optional Integration with [ItsStardewTime.](https://github.com/Enerrex/ItsStardewTime)

---

## Installation
1. Install **SMAPI 4.0.0+**  
2. Install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)  
3. Extract the mod into your `Stardew Valley/Mods` folder  
4. Launch the game through SMAPI

---

## Config
A `config.json` file is generated on first launch. You can edit it directly or through GMCM.

| Option | Default | Description |
|--------|----------|-------------|
| `UnfreezeTimeAlways` | `false` | If true, time always passes when menus are open. |
| `UnfreezeTimeWhileFishing` | `true` | If true, time passes only when fishing. |
| `CloseOnFishBite` | `true` | Automatically closes menus when a fish bites. |
| `DebugLogging` | `false` | Prints detailed debug messages to the SMAPI console. |

---

## Compatibility

* Built for **Stardew Valley 1.6+**, requires **SMAPI 4.0+** and GMCM.
* Uses Harmony transpilers. Other mods that patch `Game1.UpdateGameClock` or `Game1.shouldTimePass` may conflict.
* I use this mod while running 140+ mods, including an autofisher, time controllers, content patched mods, etc. Everything should be okay, but report issues if you encounter problems.

---

## Overview + Technical Summary

Identified and solved a hard-coded limitation in Stardew Valley's game engine where opening any menu freezes in-game time. I developed this mod to allow time to progress naturally while the player's inventory is open while fishing. This is a huge benefit to me, as I like to organize while autofishing. I figured others would enjoy it too.

The technical solution involved using the Harmony library to perform runtime patching of the game's C# code. After looking through some [decompiled code](https://github.com/Dannode36/StardewValleyDecompiled), I wrote a prefix patch targeting the core time-keeping method, Game1.shouldTimePass(), to inject logic that overrides the engine's default behavior. This required a deep analysis of the game loop to ensure the patch was efficient, compatible with other mods, and did not disrupt critical 10-minute clock updates that manage world events. The mod also includes custom input handlers and state management to provide a seamless user experience, complete with a config menu via the Generic Mod Config Menu API.
