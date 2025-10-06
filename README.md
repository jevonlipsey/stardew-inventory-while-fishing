# Inventory While Fishing

A **Stardew Valley** mod that lets you open and manage your inventory, map, and other menus **while fishing**—without freezing the game. Perfect for multitaskers or players using auto-fishing mods who want time to keep flowing while waiting for a bite.

---

## ✨ Features

* **Open Menus While Fishing**: Access inventory, map, skills, and more after casting your line.
* **Unpaused Time**: Menus no longer freeze the in-game clock during fishing, just like in multiplayer.
* **Seamless Gameplay**: Menus close instantly when a fish bites, ensuring the fishing minigame works correctly.
* **Configurable**: Supports [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) so you can choose whether time always passes in menus or only while fishing.

---

## ⚙️ How It Works: Technical Deep Dive

### The Problem

In Stardew Valley’s core loop, the method `Game1.shouldTimePass(bool)` enforces a hard rule:

```csharp
if (activeClickableMenu != null) 
    return false;
```

This halts the in-game clock whenever a menu is open. Simply setting `Game1.paused = false` doesn’t fix this, since the game never even accumulates milliseconds toward advancing time while menus are active. The result: menus always freeze time.

### The Solution

This mod uses **Harmony transpilers and patches** to surgically override the time logic:

1. **Transpiler on `Game1.UpdateGameClock`**

   * Replaces calls to `Game1.shouldTimePass(bool)` with a custom wrapper.
   * The wrapper first checks the vanilla logic, then applies the mod’s rules: if the player is fishing (or if always-unfreeze is enabled), the game is told to keep ticking.
   * This ensures that 10-minute updates (lighting, NPC schedules, weather, mines, etc.) still run exactly as designed, without stalls at the 9th minute.

2. **Postfix Safeguards**

   * A postfix on `performTenMinuteClockUpdate` ensures that `Game1.paused` is never left in a frozen state.
   * A postfix on `shouldTimePass` makes the override visible to other parts of the engine, keeping consistency.

3. **Input Handling**

   * Menu hotkeys are intercepted to open the inventory during fishing, while suppressing double inputs that would instantly close it.
   * When a fish bites, the menu auto-closes to let the fishing minigame appear normally.

The result is **smooth, lag-free time progression** while menus are open, even during fishing.

---

## 📚 Developer Reflection

This project was my **first Stardew Valley mod built from scratch**, and it required going beyond surface-level SMAPI usage. Key takeaways:

* **Deep Dive into the Game Loop**: Learned how `UpdateGameClock`, `performTenMinuteClockUpdate`, and `shouldTimePass` interact to control in-game time.
* **Advanced Harmony Usage**: Went from simple postfixes to writing a **transpiler** that rewrites IL instructions — a precise, low-overhead fix instead of brute force.
* **Debugging State Interactions**: Diagnosed the “9th-minute freeze” by understanding that the problem wasn’t just `paused`, but the way milliseconds accumulate toward clock updates.
* **Config + UX Design**: Integrated [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) for player-friendly customization.
* **Portfolio-Ready Learning**: This mod demonstrates the ability to identify a hard-coded engine limitation and engineer a safe, efficient solution using runtime patching.

---

## 📥 Installation

1. Install the latest version of [SMAPI](https://smapi.io/).
2. Install [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098).
3. Download this mod and unzip it into your `Stardew Valley/Mods` folder.
4. Launch the game through SMAPI.

---

## ⚠️ Compatibility Notes

* Requires **Stardew Valley 1.6+** and **SMAPI 4.0+**.
* Uses Harmony transpilers. Other mods that patch `Game1.UpdateGameClock` or `Game1.shouldTimePass` may conflict.
* Please report issues if you encounter unusual interactions.

---

## 💡 Credits

* Built with [SMAPI](https:
