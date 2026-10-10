# Battle data

The game reads its battle content from `Unity/Assets/Resources/BattleData/`:

| File | Holds |
|---|---|
| `rules.json` | Numbers shared by every fight: turn gauge, mana, hit and crit, damage ranges, effect chance limits, type chart, faction wheel, doctrines (2/4 heroes of a faction, for example Arcana's Recitation: `recitationEvery`, `recitationPower`) and type bonuses (2/3 heroes of a type), fight length targets per difficulty. |
| `heroes.json` | The test roster: 20 heroes, two per faction, across the five rarities (4 Common, 5 Uncommon, 5 Rare, 4 Epic, 2 Legendary). |
| `enemies.json` | Enemy units, same shape as heroes. `boss` and `unyielding` mark bosses. |
| `stages.json` | The balance simulator's 6 test stages (`tools/BattleSim`, its report and the fight viewer); the game doesn't show them. Each has `difficulty`, `formation` (`2-3` or `3-2`), `hpScale`, `atkScale` and `enemies` as `[id, slot]`. |
| `progression.json` | The progression and economy rules the progression simulator plays by (campaign layout, rewards, summons, shop, the 3 starting heroes). Its `heroGrowth` (stat multipliers for rarity, level, stars, gear and skills; level caps; starting star rank) and `costs` (XP, gold, fodder, skill tomes, gear materials) sections are read by `Unity/Assets/BattleCore/Growth.cs`, the growth rules the game and the simulator share. Hand-edited. |
| `campaign.json` | The campaign: 4 difficulties × 10 realms × 10 stages × 4 battles. Same shape as `stages.json`, plus `realm`, `realmNumber`, `stage`, `battle`, `boss` (`stage` or `realm`, absent on normal battles) and `heroScale`: the strength the battle expects the player's heroes at (times their listed stats), which it is sim-checked against. The game plays these battles, with heroes at `heroScale` until they have levels. Generated and sim-checked by `tools/ProgressionSim --build-campaign`; never edit by hand. |

These files are generated. Edit `gen_data.py` here and run, from the repo root:

```
python3 -I tools/BattleData/gen_data.py Unity/Assets/Resources/BattleData 0.5
```

The second argument scales every unit's health (0.5 keeps normal fights between 1 and 3 minutes).

The script checks its own output before writing it, and a pull request fails if the files above differ from what it writes:
- each hero's kit fits its rarity (see Units);
- no hero or enemy name shares or nearly shares a dead god's name (doc 12 sections 8 and 12): no word in it is within one letter of a god's name or starts with the same four letters.

## Units

`id`, `name`, `faction`, `type`, `role` (Tank, Warrior, Ranger, Caster, Controller, Support), `kind` (`phys` hits Armour, `magic` hits Spirit), optional `description` (a note on how the kit plays), stats (`hp`, `atk`, `armour`, `spirit`, `spd`, plus optional `crit`, `critDmg`, `acc`, `dodge`, `effect`, `tenacity`, `haste`, `manaRegen`, `pen`), and a kit:

- `basic`: optional; defaults to the opposite enemy, or the front for Rangers, Casters and Supports.
- `skills`: up to three, each with `name`, `cd`, optional `startCd` and `actions`. They fire in order 1, 2, 3 by which is ready and useful.
- `ult`: fires at 100 mana, before anything else.
- `passives`: `trigger` is one of `always`, `battleStart`, `turnStart`, `onHit`, `onCrit`, `onHitTaken`, `onDodge`, `onKill`, `allyFalls`, `hpBelow`, `afterUlt`; options `once`, `cooldown`, `chance`, `stats`, `mods`, `actions`.

Heroes also have `rarity` (Common, Uncommon, Rare, Epic, Legendary) and `fantasy`, a one-line idea of who they are (placeholders until the launch roster). Rarity sets how complex a kit is, not how useful it is (doc 06 section 9):

| Rarity | Skills | Passives |
|---|---|---|
| Common | 2 | 1 that only changes stats (`always`, no `actions`) |
| Uncommon | 2 | 1 |
| Rare, Epic | 3 | 1 |
| Legendary | 3 | 2 |

Common and Uncommon heroes use the default basic attack. Listed stats are the same for every rarity; the growth rules multiply them by rarity (`progression.json`, `heroGrowth.rarityBase`, applied by `Unity/Assets/BattleCore/Growth.cs`).

## Actions

Each action has a `target` and an `effect`.

- **Enemy targets by position:** `opposite`, `front`, `back`, `pierce`, `splash`, `cross`, `frontRow`, `backRow`, `all`.
- **Enemy targets by condition:** `lowest`, `highest`, `strongest`, `fastest`, `mostMana`, `mostBuffs`, `mostDebuffs`, `role`, `revenge`, `marked`, `random` (`n`, `distinct`), `chain` (`n`, `falloff`). `n` picks the top N.
- **Ally targets:** `self`, `same`, `context`, `contextAttacker`, `lowestAlly`, `allAllies`, `behind`, `inFront`, `neighbours`, `myRow`, `debuffedAlly`, `manaAlly`, `lowestManaAlly`, `fallenAlly`.
- **Effects:** `damage`, `heal`, `shield`, `status`, `cleanse`, `strip`, `steal`, `mana`, `gauge`, `revive`, `mark`, `channel`, `resetCd`.
- **Common fields:** `mult` (times Attack), `value`, `pct`, `casterMaxHp`, `targetMaxHp`, `chance`, `turns`, `stacks`, `pen`, `lifesteal`, `execute`, `bonusVsExposed`, `bonusVs` + `bonus`, `repeatOnKill`.

Statuses: buffs `challenge`, `bulwark`, `bloodrite`, `tailwind`, `grace`, `ward`; debuffs `pyre`, `blight`, `godstruck`, `hush`, `sunder`, `eclipse`. See plan doc 04 for what each does.
