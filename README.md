# Unclicker for Obenseuer

A BepInEx 6 (Unity Mono) plugin for **Obenseuer**. The game makes you click far too much to get anything done: planting
one bed takes a recipe click, a Plant button, a slider and a confirm, and every ingredient has to be dragged into the station first.
Unclicker reworks the station UI and UX to cut that to the minimum: one click on a recipe does the job, the amount is set on
the button, ingredients come straight from your inventory, one button empties a station, and one key harvests a whole bed. Nothing is written to the game's saves.

## What it does

**One click does what you most likely want**

- Bed, terrarium, flowerpot: click a recipe and it plants as many as fit. No info panel, no Plant button, no slider.
- Workstation: click a recipe and it crafts one.
- Brewing barrel, distillery: click a recipe and it starts the biggest batch the ingredients allow.
- No need to load the station first: ingredients come straight from your inventory; leftovers and empty bottles come back.
- Done? One **Take all** button moves everything from the station into your inventory.

**When you want something else**

- The amount is right on the button: `6× Turnip`, `5L Kilju`.
- Shift + mouse wheel over the button changes it before you click.
- Shift + click on a growing recipe opens the vanilla amount slider.
- Hover a recipe to see its info, with totals for the chosen amount.

**Farming**

- Shift + E on a bed or terrarium harvests every ripe plant into your inventory; a **Harvest all** button does the same in the panel
  (on Linked Terrariums, the whole group). Dead plants are cleared too. When your inventory fills up it stops; the rest stays on the plants.
- A click on a plant cell in the panel puts the harvest into your inventory instead of the bed's storage.
- Planting uses up the worst items first: lowest quality, then the least fresh. The good ones stay for you.
- A dead plant is removed with a press, not a hold.

**Stays out of the way**

- The popup's own Craft / Plant / Start button is hidden, since the click on the recipe already does it.
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
| Farming | `HarvestAll` | `true` | ModifierKey + interact on a growing device harvests everything ripe |
| | `HarvestAllHint` | `true` | the "harvest all" hint under the device's name |
| | `HarvestAllButton` | `true` | the Harvest all button in the growing panel (needs a restart) |
| | `HarvestDeadPlants` | `true` | harvest all also clears dead plants |
| | `Overflow` | `StopWhenFull` | `StopWhenFull` / `DropRest` (drop what doesn't fit) |
| | `CellHarvestToPlayer` | `true` | a click on a plant cell harvests into your inventory |
| | `PlantingOrder` | `WorstFirst` | `WorstFirst` / `Vanilla` (slot order) |
| | `InstantDeadRemoval` | `true` | dead plants go with a press |
| Mod | `Enabled` | `true` | off = no patches, everything as in vanilla (needs a restart) |

## Compatibility

- **Inventory Tweaks**: its "Shift + click plants the max" is replaced on growing devices, since Shift + click opens the
  amount popup here. Its other features keep working.
- **Craft From Storage**: should work together; amounts count the items it pulls from storage.
- **Linked Terrariums**: on a linked terrarium the amount counts the seeds and free slots of the whole group, and one click
  plants across it.
- Gamepad: not changed.

When reporting a problem, please attach `BepInEx/LogOutput.log` and say which station and recipe.

## Building

Requirements: .NET SDK 6+ and the game with BepInEx 6 installed.

1. Copy `local.props.example` to `local.props` and set `GameDir` to your Obenseuer folder.
2. `dotnet build Unclicker -c Release` (add `-p:Deploy=true` to copy the DLL into the game's plugins folder).

## License

MIT © absurdmane, see [LICENSE](LICENSE).
