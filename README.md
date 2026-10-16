# Unclicker for Obenseuer

A BepInEx 6 (Unity Mono) plugin for **Obenseuer**. Recipe buttons that just do the thing: click to plant, craft or start
a brew, pick the amount with Shift + mouse wheel, see the recipe info on hover. Nothing is written to the game's saves.

## What it does

**Growing devices** (beds, terrariums, flowerpots, ...)

- Each recipe button shows how many it will plant, e.g. `6× Turnip`. By default that's the most that fits.
- Click plants right away: no info panel, no Plant button, no popup.
- Shift + mouse wheel over the button changes the amount; Shift + click opens the vanilla amount popup.

**Workstations** (Craft buttons): a click on the recipe crafts one, like the Craft button.

**Batch processes** (brewing, distilling, ...)

- The button shows the batch first, in liters for liquids, e.g. `5L Kilju`. By default it is the vanilla maximum.
- Shift + mouse wheel changes the batch; click starts it.

**Everywhere**

- Recipe info shows on hover, with totals for the chosen amount, and updates as you scroll.
- Ingredients in your inventory count too: they move into the station on start, and what's left over (and empty bottles) comes back.
- A **Take all** button on station panels moves everything from the station into your inventory.
- The info popup's own Craft / Plant / Start button is hidden, since a click on the recipe already does it.
- A plain mouse wheel still scrolls the recipe list. Missing ingredients, no power or wrong conditions: the click does nothing, as in vanilla.

## Install

1. Install [BepInEx 6 (Unity Mono)](https://builds.bepinex.dev/projects/bepinex_be) into the game folder.
2. Put `Unclicker.dll` into `BepInEx/plugins/Unclicker/`.
3. Start the game once; settings appear in `BepInEx/config/obenseuer.absurdmane.unclicker.cfg`.

## Configuration

| Section | Key | Default | Meaning |
|---|---|---|---|
| Stations | `Growing` / `Manufacturing` / `ManufacturingProcess` | `true` | per station type; a type that's off stays fully vanilla |
| Controls | `ModifierKey` | `LeftShift` | hold for the mouse wheel amount and Shift + click |
| Panels | `TakeAllButton` | `true` | the Take all button (needs a restart) |
| | `HideCreateButton` | `true` | hide the popup's Craft / Plant / Start button |
| Ingredients | `FromInventory` | `true` | stations also use ingredients from your inventory |
| Mod | `Enabled` | `true` | off = no patches, everything as in vanilla (needs a restart) |

## Compatibility

- **Inventory Tweaks**: its "Shift + click plants the max" is replaced on growing devices, since Shift + click opens the
  amount popup here. Its other features keep working.
- **Craft From Storage**: should work together; amounts count the items it pulls from storage.
- Gamepad: not changed.

When reporting a problem, please attach `BepInEx/LogOutput.log` and say which station and recipe.

## Building

Requirements: .NET SDK 6+ and the game with BepInEx 6 installed.

1. Copy `local.props.example` to `local.props` and set `GameDir` to your Obenseuer folder.
2. `dotnet build Unclicker -c Release` (add `-p:Deploy=true` to copy the DLL into the game's plugins folder).

## License

MIT © absurdmane, see [LICENSE](LICENSE).
