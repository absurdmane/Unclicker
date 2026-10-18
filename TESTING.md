# Unclicker — test plan

Build and install: `dotnet build -c Release -p:Deploy=true` in `Unclicker/` (copies the dll to `BepInEx\plugins\Unclicker`).
Log: `BepInEx\LogOutput.log` — look for `Unclicker 1.3.0 loaded` and any exceptions mentioning `Unclicker`.
Config: `BepInEx\config\obenseuer.absurdmane.unclicker.cfg` (created on the first run).

Mark each line: OK / FAIL + a note. "Shift" below means the configured `ModifierKey` (default LeftShift).

## 0. Startup

- [ ] The game starts, the log has `Unclicker 1.3.0 loaded`, no errors.
- [ ] The config file exists, with `Stations` (Growing, Manufacturing, ManufacturingProcess) and `Controls` (ModifierKey).

## 1. Growing (bed, terrarium, flowerpot)

Put seeds (and some produce) into the bed's inventory, leave several slots free.

- [ ] Recipe buttons show `N× Name`, N = min(items, free slots). The `×` sign renders (not a square).
- [ ] Hover over a button shows the recipe info; moving away hides it. Clicking does **not** open/pin the panel.
- [ ] Moving the mouse quickly up and down across the buttons: the info panel switches recipes without flicker or a replayed
      appear animation.
- [ ] With the create bar hidden, the rows fill the freed space down to the panel's bottom (log: `Info popup rows area: '...'`).
- [ ] The panel stays while the mouse is over the recipe list or over the panel itself; move onto the panel and scroll it.
      It hides ~0.3 s after the mouse leaves both.
- [ ] Shift + wheel up/down over a button changes N within 1..max. It doesn't go below 1 or above max.
- [ ] The hover info panel shows totals for N (seeds used, plants out) and updates on every wheel step.
- [ ] A plain wheel over the buttons scrolls the recipe list (try a bed with many recipes).
- [ ] Click plants exactly N. The items taken match N; free slots drop by N.
- [ ] After planting, the list rebuilds and N is back at the new max.
- [ ] Shift + click opens the vanilla amount popup. "Plant" plants the chosen amount. Is there a Cancel button? (note it)
- [ ] With exactly one free slot, Shift + click plants at once (vanilla behaviour).
- [ ] Bed full (0 free slots): no amount, click does nothing, no error.
- [ ] Recipe with missing ingredients / skill too low (red/grey button): click does nothing, hover still shows info.
- [ ] Right click on a button: vanilla behaviour, no planting.
- [ ] A bed with `ManualHarvest` (world-harvest bed) and a terrarium both work the same.

## 2. Manufacturing (workbench, stove, anything with a "Craft x/y" button)

- [ ] No amount on the buttons.
- [ ] Hover shows info; click crafts one, like the vanilla Craft button.
- [ ] Station with several parallel slots (`Craft 1/3`): three clicks start three crafts; a fourth click does nothing.
- [ ] Instant recipe (no process time): each click crafts one immediately.
- [ ] Unpowered station: click does nothing; the power icon flashes (vanilla).
- [ ] Recipe whose conditions aren't met (e.g. needs Clean / Steam): click does nothing.
- [ ] Shift + click does nothing.
- [ ] Dishwasher works like other stations.
- [ ] Autocraft stations (if you know one) behave exactly as vanilla: info opens on click, no change.

## 3. ManufacturingProcess (brewing barrel, distillery, other batch processes)

- [ ] While loading items: buttons show the batch first, `5L Kilju` for liquid recipes (`N× Name` otherwise), default = the vanilla max batch.
- [ ] The liters on the button match the output amount in the info panel (hover). Fractional amounts look sane (e.g. `2.5L`).
- [ ] Shift + wheel lowers/raises N within 1..max. The info panel updates: inputs, outputs, process time, quality.
- [ ] Click starts the process with N. The items taken and the output amount match N (compare with a vanilla max batch).
- [ ] Default N (untouched) gives the same result as the vanilla Start button.
- [ ] While processing / ready / failed: no amount; click does nothing; the vanilla Abort/Fill buttons still work if reachable.
- [ ] Save and load during a smaller batch: the process continues with the same amount and output.
- [ ] Unpowered (if it needs power): click does nothing.
- [ ] Shift + click does nothing.

## 3b. Popup without its Craft button

- [ ] Hover info on a bed, a workbench and a brewing barrel (loading stage): no Plant / Craft / Start button at the bottom,
      and no empty bar where it was. Log: `Info popup create bar: '...'` names what was hidden.
- [ ] Brewing barrel while processing / ready: the panel still has Abort / Fill.
- [ ] `HideCreateButton = false`: the button is back.

## 3a. Take all button

- [ ] A workbench, a stove, a brewing barrel and a bed panel each show a round "Take All" button at the slot grid's top-right corner. Note where it
      sits and whether it overlaps anything (screenshot welcome).
- [ ] Click: everything from the station's inventory moves to yours (what doesn't fit stays). Sound plays.
- [ ] Generator panel: no button.
- [ ] The plain storage panel's own Take all still works on chests.
- [ ] `TakeAllButton = false` + game restart: no button.

## 3c. Ingredients from the inventory

- [ ] Empty brewing barrel, ingredients only in your inventory: the recipe shows, the amount counts your items, click starts it.
      The barrel took exactly what the batch needs; the rest of your stacks is back in your inventory.
- [ ] Some ingredients in the barrel, the rest in your inventory: the barrel's own are used first, the amount counts both.
- [ ] A liquid ingredient in bottles (water for kilju): after the start the empty bottles (and partly full ones) are in your inventory.
- [ ] Workbench / stove: a craft with the ingredients only in your inventory works; several clicks in a row too.
- [ ] Bed: seeds only in your inventory: N counts them, click plants N, the rest of the seeds stays with you. Shift+click popup too.
- [ ] Station slots almost full: the start still works or crafts less; nothing is lost; leftovers return.
- [ ] Your inventory full when leftovers return: they stay in the station (no items lost, no error).
- [ ] A recipe that fills an output container (distilling into bottles): note whether it works with the empty bottles only in your inventory
      (known gap: output containers are not moved).
- [ ] `FromInventory = false`: vanilla behaviour, only the station's items count.
- [ ] With **Craft From Storage** (+ Obenseuer Modding Framework, which loads before Unclicker): open a station with containers nearby
  several times. It opens fast every time, memory stays flat, no "station slots changed" warning from the framework in the log;
  after save + load the station has its usual number of slots.

## 4. Config

- [ ] `Growing = false`: growing devices are fully vanilla (click opens the panel, no amount). Same for the other two switches.
- [ ] `ModifierKey = RightShift` (or another key): the wheel and Shift + click follow the new key.
- [ ] Config changes made in-game (e.g. via a config manager) apply the next time the station is opened.

## 5. Other mods

- [ ] With **Inventory Tweaks** installed: the log says its Shift+click prefix was removed. Shift + click on a bed opens our popup
      and doesn't plant the max. Its other features (quick transfer, overflow to player) still work.
      Its tooltip "Shift + Click: plant as many as possible" on bed buttons is now wrong — note how it looks.
- [ ] Inventory Tweaks installed + `Growing = false`: its Shift+click works as before (nothing is removed).
- [ ] With **Craft From Storage**: N on bed/process buttons counts the storage items; planting takes from storage correctly.

## 6. Gamepad (out of scope, just check nothing breaks)

- [ ] Recipe selection and the vanilla buttons still work with a gamepad.

## Farming

- [ ] Shift + E on a bed with ripe plants: all go to the inventory, unripe ones stay; with nothing ripe the bed opens.
- [ ] The use text shows the harvest-all hint only when something is ripe or dead.
- [ ] Multi-harvest plants: fruit picked, the bush stays. Dead plants cleared.
- [ ] Full inventory: stops, a notification says how many plants are left; nothing lost. DropRest: the rest drops at your feet.
- [ ] Harvest button (round, smaller text that fits): left of Take All, harvests the panel's device; on Linked Planters the whole group.
- [ ] Click on a plant cell: harvest goes to the inventory; with a full inventory into the terrarium's storage.
- [ ] Worst first: mixed-quality seeds/produce in a bed and the inventory, plant: the worst are used, the best stay.
- [ ] Dead plant: a press removes it, the hint says "Uproot dead plant"; a live unripe plant still needs a hold.
- [ ] Animal cages (also Growing): Shift + E feels right or needs excluding.
## Autoloot

- [ ] Middle click on an item (own inventory or an open container): a gold star appears on every stack of it; again: it goes.
- [ ] The star does not cover the amount, quality or the game's own upgrade marker; it stays right after a scene change and a mod reload.
- [ ] The list survives a game restart (`[Autoloot] Items` in the config).
- [ ] A container outside the tenement with a listed item: opening it moves the item into the inventory, the panel shows it gone.
- [ ] Containers in the tenement (own fridge, shelves): nothing moves.
- [ ] Owned items (a shop's or a resident's container): not taken, no crime.
- [ ] Full inventory: what doesn't fit stays in the container, nothing dropped.
- [ ] Trade panels and station storages: nothing moves.
