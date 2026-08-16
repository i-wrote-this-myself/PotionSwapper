<p align="center">
  <img src="icon.png" alt="PotionSwapper" width="128" height="128">
</p>

<h1 align="center">PotionSwapper</h1>

<p align="center">XIVCombo but for your potions. Stops you from chugging a basic Potion when you've got a stack of X-Potions sitting in your bag.</p>

## What it does

Scans your hotbar every frame. If a slot holds a potion, it silently swaps it to the best one you actually own based on your max HP. HQ variants get picked automatically if you have them.

## The smart parts

- **Deep Dungeon aware** - knows which potion belongs to which dungeon (Sustaining, Empyrean, Orthos, Pilgrim's) and only swaps in the right one.
- **Elixir handling** - can fold Elixirs into the normal pool, give them their own slot, or leave them alone entirely.
- **Cooldown aware** - won't hand you a potion that's still on cooldown.
- **Eureka** - grabs the Eurekan Potion when you're actually in Eureka.
- **Icon tinting** - optional, tints swapped icons so you can tell a swapped potion apart from your original setup.

Everything's toggleable in the config window (`/pswap`).

## How to install

In the game, open Dalamud settings, go to **Experimental**, and add this to your custom plugin repositories:

```
https://i-wrote-this-myself.github.io/PotionSwapper/pluginmaster.json
```

Save, reopen the plugin installer, and PotionSwapper should show up.
