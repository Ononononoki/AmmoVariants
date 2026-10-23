# AmmoVariants

Scatter, piercing and energy ammunition for bows and crossbows. Adds 42 variants across vanilla ammo tiers, from wood and bone to Bloodgold.

## What it does

- **Scatter arrows.** Five projectiles per shot, each dealing 30% of normal shot damage, with a tighter spread than scatter bolts.
- **Scatter bolts.** Fourteen small pellets per shot, each dealing about 11.43% of normal shot damage, for up to 160% before armor and resistances.
- **Piercing.** Starts at 110% of normal shot damage and passes through up to four creatures, losing 15% of its remaining damage after each. No distance damage loss. Stops on ground, trees, rocks and structures. Each creature is hit once per projectile.
- **Energy.** Flies straight through creatures and terrain. Full shot damage becomes 85% lightning and 15% pierce, plus 15% additional fire damage that sets targets burning. All three shares are in the config. Damage falls to 25% at 200 meters, where flight ends. No creature limit or damage loss per creature. Chop and pickaxe damage are excluded from conversion.
- Piercing and energy have an extra 0.35-meter hit radius against enemies. Piercing keeps a thin collision ray against terrain and objects.
- Icons based on vanilla inventory sprites, native elemental flight effects and compact effects at held and dropped tips. Piercing and scatter trails match the material or element, energy keeps blue lightning. Separate bow and crossbow firing sounds retain the original weapon mechanism.
- Optional material-colored trails for existing ballista bolts. Trail size follows the larger bolt. Ballista damage, targeting, flight and ammo use stay unchanged.

Scatter scales the complete weapon and ammo damage, including elemental damage. Bow draw strength and the weapon's original accuracy still apply.

## Crafting

| Type | Ingredients | Output | Station |
|---|---|---|---|
| Scatter | 20 base ammo and a finishing material | 10 | Base ammo station and level |
| Piercing | 20 base ammo and a reinforcing material | 20 | Base ammo station and level |
| Energy | 20 Carapace or Bloodgold ammo and 5 refined Eitr | 10 | Black Forge |

Scatter and piercing cover thirteen arrow types and six bolt tiers. Extra materials follow the ammo tier, such as resin, coal, flint, metals or carapace. Energy is available for Carapace and Bloodgold arrows and bolts. Recipes unlock through normal ingredient discovery.

## Config

The file is `BepInEx/config/com.onononoki.ammovariants.cfg`. Gameplay and recipe settings are synced from the server. Visual and sound settings are each player's own.

| Setting | Default | What it does |
|---|---|---|
| `Scatter.BowPellets`, `Pellets` | 5, 14 | Bow and crossbow projectile counts |
| `Scatter.BowConeHalfAngle`, `ConeHalfAngle` | 4, 6 degrees | Bow and crossbow spread half-angles |
| `Scatter.BowDamageShare`, `CrossbowDamageShare` | 0.30, 0.1142857 | Damage per projectile relative to a normal shot |
| `Piercing.DamageShare` | 1.10 | Initial combat damage relative to a normal shot |
| `Piercing.MaxCreatures`, `LossPerCreature` | 4, 0.15 | Creature limit and damage loss per creature |
| `Piercing.EnemyRadius`, `Energy.EnemyRadius` | 0.35 meters | Extra radius against enemies |
| `Energy.Range`, `DamageAtRange` | 200 meters, 0.25 | End range and remaining damage share |
| `Audio.FiringSoundVolume`, `NativeReleaseVolume` | 1, 0.30 | Added firing sound and original weapon release volume |
| `Trails.Scatter`, `Piercing`, `Energy`, `Ballista` | true | Enable each custom flight trail separately, requires restart |
| `Recipes.UnlockAllRecipes` | false | Bypass normal recipe discovery |
| `Debug.LogCounters` | false | Troubleshooting logs |

Existing configs retain their saved values. For the release balance, set `Scatter.Pellets` to 14, `Scatter.CrossbowDamageShare` to 0.1142857 and `Piercing.MaxCreatures` to 4.

Crafting amounts and materials are configurable. Recipe changes apply during play. Restart after changing prefab visuals or the original weapon release volume.

Trail switches affect custom flight lines and energy flight lightning. Native elemental particles and effects at held or dropped tips remain.

## Installation and compatibility

- Use a mod manager, or put `AmmoVariants.dll` into `BepInEx/plugins`. Requires BepInExPack Valheim and Jotunn.
- Install the same version on the server and every player.
- BetterArchery is optional. Its drawing, aiming, velocity changes and quivers remain active. Its arrow recovery is disabled for AmmoVariants ammunition. Energy ammo flies without gravity, so aim offsets from BetterArchery or other mods are ignored for it and it flies straight at the crosshair. Ordinary ammo keeps its usual BetterArchery behavior.
- Secondary volleys from BowSecondaryMultishot do not receive AmmoVariants effects. ProjectileTweaks combinations have not been playtested.

## Feedback and bug reports

Report problems on [GitHub](https://github.com/Ononononoki/AmmoVariants/issues). Include the mod version, ammo type, weapon mods and relevant BepInEx log lines.
