# Changelog

## 1.1.1

- Fixed: with Craft From Storage, stations kept the containers' slots in their own inventory after
  every recipe check. They piled up: opening a station took longer and longer, memory kept growing, and the bloated station
  went into the save.
- Fixed: the Take all and Harvest all buttons were invisible on every panel. They are now small round Take All and Harvest
  buttons in the style of the inventory's Sort button, tucked into the corner of the station's inventory.
- Harvest all no longer drops what doesn't fit. Everything that fits your inventory is harvested, plant by plant and fruit
  by fruit; the rest stays on the plants (`Overflow = StopWhenFull`).
- Linked Terrariums is now Linked Planters: its group support is found under the new name too.

## 1.1.0

- Farming: Shift + E harvests a whole bed, a Harvest all button in the growing panel, plant cells harvest into your inventory,
  worst items planted first, dead plants removed with a press. New `[Farming]` config section.
- Linked Terrariums support: on a linked terrarium the amount counts the whole group, and one click plants across it.
  Seeds from the inventory are no longer counted once per linked terrarium.
- Take all and Harvest all sit above the station's inventory and are greyed out when there's nothing to take or harvest.
  The Take all button was clipped out of sight on the terrarium panel.

## 1.0.0

- First release.
