# Battle data

The game reads its battle content from `Unity/Assets/Resources/BattleData/`:

| File | Holds |
|---|---|
| `rules.json` | Numbers shared by every fight: turn gauge, mana, hit and crit, damage ranges, effect chance limits, type chart, faction wheel, doctrines (2/4 heroes of a faction) and type bonuses (2/3 heroes of a type), fight length targets per difficulty. |
| `heroes.json` | The test roster: 20 heroes, two per faction. |
| `enemies.json` | Enemy units, same shape as heroes. `boss` and `unyielding` mark bosses. |
| `stages.json` | Stages: `difficulty`, `formation` (`2-3` or `3-2`), `hpScale`, `atkScale` and `enemies` as `[id, slot]`. |

These files are generated. Edit `gen_data.py` here and run, from the repo root:

```
python3 -I tools/BattleData/gen_data.py Unity/Assets/Resources/BattleData 0.5
```

The second argument scales every unit's health (0.5 keeps normal fights between 1 and 3 minutes).

## Units

`id`, `name`, `faction`, `type`, `role` (Tank, Warrior, Ranger, Caster, Support), `kind` (`phys` hits Armour, `magic` hits Spirit), stats (`hp`, `atk`, `armour`, `spirit`, `spd`, plus optional `crit`, `critDmg`, `acc`, `dodge`, `effect`, `tenacity`, `haste`, `manaRegen`, `pen`), and a kit:

- `basic`: optional; defaults to the opposite enemy, or the front for Rangers, Casters and Supports.
- `skills`: up to three, each with `name`, `cd`, optional `startCd` and `actions`. They fire in order 1, 2, 3 by which is ready and useful.
- `ult`: fires at 100 mana, before anything else.
- `passives`: `trigger` is one of `always`, `battleStart`, `turnStart`, `onHit`, `onCrit`, `onHitTaken`, `onDodge`, `onKill`, `allyFalls`, `hpBelow`, `afterUlt`; options `once`, `cooldown`, `chance`, `stats`, `mods`, `actions`.

## Actions

Each action has a `target` and an `effect`.

- **Enemy targets by position:** `opposite`, `front`, `back`, `pierce`, `splash`, `cross`, `frontRow`, `backRow`, `all`.
- **Enemy targets by condition:** `lowest`, `highest`, `strongest`, `fastest`, `mostMana`, `mostBuffs`, `mostDebuffs`, `role`, `revenge`, `marked`, `random` (`n`, `distinct`), `chain` (`n`, `falloff`). `n` picks the top N.
- **Ally targets:** `self`, `same`, `context`, `contextAttacker`, `lowestAlly`, `allAllies`, `behind`, `inFront`, `neighbours`, `myRow`, `debuffedAlly`, `manaAlly`, `lowestManaAlly`, `fallenAlly`.
- **Effects:** `damage`, `heal`, `shield`, `status`, `cleanse`, `strip`, `steal`, `mana`, `gauge`, `revive`, `mark`, `channel`, `resetCd`.
- **Common fields:** `mult` (times Attack), `value`, `pct`, `casterMaxHp`, `targetMaxHp`, `chance`, `turns`, `stacks`, `pen`, `lifesteal`, `execute`, `bonusVsExposed`, `bonusVs` + `bonus`, `repeatOnKill`.

Statuses: buffs `challenge`, `bulwark`, `bloodrite`, `tailwind`, `grace`, `ward`; debuffs `pyre`, `blight`, `godstruck`, `hush`, `sunder`, `eclipse`. See plan doc 04 for what each does.
