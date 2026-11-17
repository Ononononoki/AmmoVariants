# Changelog

## 1.2.0

- New reusable Yondu Arrow, made at the Black Forge from 20 Bloodgold, 5 refined Eitr and 1 Thunderblood Essence. It stays in your inventory and flies back after each shot.
- After release it passes through enemies for half a second, then hunts enemies within 50 m for up to 10 guided hits, one enemy at a time before repeating any. Each repeat on the same enemy deals 10% less.
- Each Yondu Arrow has 1000 durability, 1 per guided hit, repaired at the Black Forge. The last point still lands its hit.
- Each Yondu hit raises your bow skill by one tenth of a normal arrow hit.
- Explosive blast damage is 50% higher for all six explosive ammo. Existing configs get the four new blast values once at startup.
- Scatter and explosive bolt icons are longer and easier to read.
- Needs the same version on the server and every player.

## 1.1.0

- New explosive arrows and bolts in three tiers: fire arrow and bone bolt, needle arrow and black metal bolt, Charred arrow and bolt.
- An explosive shot sticks where it hits, deals 30% of a shot as pierce and fire, and blows up 1.25 seconds later. The blast deals 125% of a full draw shot as blunt and fire, with chop and pickaxe damage at the tier's tool level.
- Explosive ammo never hurts you, other players without PvP or tames, and spares buildings, trees and rocks inside a workbench's range. Shots into water are duds.
- Each tier has its own charge model, flames, firing sounds, blast sound and blast size. The early and plains tiers have a red-hot head and charge area, a black charred band and soot fading into the shaft.
- Item texts are one short line per kind and show the ammo's own damage of one projectile as count x value in whole numbers, like a vanilla arrow, with the equipped bow or crossbow's value in a light blue bracket. Explosive ammo adds its blast and blast radius.
- Fixed: the grappling hook no longer shows equipped mod ammo on it.

## 1.0.1

- Mod page text corrected. The trails take the color of their material or element, the old text said gold or lightning.

## 1.0.0

- First public release with 42 scatter, piercing and energy arrows and bolts.
- Tiered recipes, five scatter arrows at 30% damage each, fourteen scatter bolts at about 11.43% each, and enemy-only collision widening.
- Piercing starts at 110% damage, passes up to four creatures and loses 15% after each. Energy turns the full shot into 85% lightning and 15% pierce plus 15% fire (each share in the config), has distance falloff only, and excludes utility damage from conversion.
- Vanilla-based ammo icons, held and dropped models with localized tip effects, native flight effects, firing sounds and fading trails.
- Optional ballista bolt trails and separate config switches for scatter, piercing, energy and ballista trails.
- BetterArchery support preserves aiming, drawing and quivers while excluding variant arrow recovery.
- Energy arrows and bolts fly straight at the crosshair. Aim offsets from other mods are ignored for them.
- Energy arrows and bolts have their own blue icons with small lightning arcs.
