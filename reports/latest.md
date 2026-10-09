# Battle Balance Report

*Generated 2026-10-09 17:57 UTC by `dotnet run --project tools/BattleSim`. Rules: battle system draft 3 (doc 04) with the 10 factions and 10 types of doc 09. 400 random 5-hero teams × 12 fights per stage, auto-placed (tanks and warriors in front). Fight length assumes 1s per action at 1x.*

## The Ash Road (easy)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 100% | at most 100% |
| Median team win rate | 100% | |
| Median winning fight length | 0:33 | 0:00 to 1:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +0 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Pip, Oriel, Grub, Somna, Wren | Pip front0, Oriel front1, Grub back0, Somna back1, Wren back2 | 100% | 100% | 100% | 100% |
| Maren, Kesh, Pell, Oriel, Gorran | Maren front0, Kesh front1, Pell back0, Oriel back1, Gorran back2 | 100% | 100% | 100% | 100% |
| Quill, Wren, Solenne, Brannoc, Grub | Quill front0, Wren front1, Solenne back0, Brannoc back1, Grub back2 | 100% | 100% | 100% | 100% |
| Vesna, Oriel, Varkhul, Grub, Kesh | Vesna front0, Oriel front1, Varkhul back0, Grub back1, Kesh back2 | 100% | 100% | 100% | 100% |
| Quill, Dagna, Solenne, Hilde, Thessaly | Quill front0, Dagna front1, Solenne back0, Hilde back1, Thessaly back2 | 100% | 100% | 100% | 100% |
| Vesna, Grub, Oriel, Kesh, Wren | Vesna front0, Grub front1, Oriel back0, Kesh back1, Wren back2 | 100% | 100% | 100% | 100% |
| Seraphine, Varkhul, Hilde, Quill, Gorran | Seraphine front0, Varkhul front1, Hilde back0, Quill back1, Gorran back2 | 100% | 100% | 100% | 100% |
| Maren, Quill, Brannoc, Dagna, Somna | Maren front0, Quill front1, Brannoc back0, Dagna back1, Somna back2 | 100% | 100% | 100% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Ysolde, Quill, Seraphine, Kesh, Solenne | 100% | 0:20 |
| Hilde, Kesh, Varkhul, Ysolde, Thessaly | 100% | 0:22 |
| Kesh, Somna, Varkhul, Ysolde, Thessaly | 100% | 0:22 |

<details><summary>Win rate of teams that include each hero (average 100%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Solenne of the Noon | 100% | +0 pts |
| Oriel of the Last Light | 100% | +0 pts |
| Mordecai the Ferryman | 100% | +0 pts |
| Vesna Gravebloom | 100% | +0 pts |
| Varkhul the Ashen | 100% | +0 pts |
| Brannoc Thickhide | 100% | +0 pts |
| Grandmother Hilde Ashanvil | 100% | +0 pts |
| Big Grub Marrow | 100% | +0 pts |
| Kesh Swiftclaw | 100% | +0 pts |
| Gorran Bramblehorn | 100% | +0 pts |
| Pip the Tinker Golem | 100% | +0 pts |
| Dagna Ironvow | 100% | +0 pts |
| Maren Saltsong | 100% | +0 pts |
| Seraphine of the Deep | 100% | +0 pts |
| Old Thessaly | 100% | +0 pts |
| Quill the Inkwright | 100% | +0 pts |
| Pell the Gambler | 100% | +0 pts |
| Wren Twofaces | 100% | +0 pts |
| Ysolde Nightglass | 100% | +0 pts |
| Somna the Dreamwarden | 100% | +0 pts |

</details>

## The Drowned Chapel (normal)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 77% | at most 90% |
| Median team win rate | 100% | |
| Median winning fight length | 2:15 | 1:00 to 3:00 |
| Fights that hit the time limit | 2% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +63 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Seraphine, Vesna, Pip, Maren, Thessaly | Vesna front0, Seraphine front1, Pip back0, Thessaly back1, Maren back2 | 100% | 50% | 0% | 100% |
| Varkhul, Brannoc, Seraphine, Quill, Oriel | Varkhul front0, Brannoc front1, Seraphine back0, Quill back1, Oriel back2 | 100% | 100% | 50% | 100% |
| Gorran, Dagna, Brannoc, Pip, Pell | Gorran front0, Dagna front1, Brannoc back0, Pip back1, Pell back2 | 100% | 100% | 75% | 100% |
| Mordecai, Varkhul, Vesna, Brannoc, Wren | Mordecai front0, Varkhul front1, Vesna back0, Brannoc back1, Wren back2 | 100% | 100% | 50% | 100% |
| Kesh, Gorran, Hilde, Grub, Pell | Kesh front0, Gorran front1, Hilde back0, Grub back1, Pell back2 | 100% | 100% | 25% | 100% |
| Pip, Pell, Dagna, Grub, Maren | Pip front0, Pell front1, Dagna back0, Grub back1, Maren back2 | 100% | 100% | 0% | 100% |
| Kesh, Thessaly, Wren, Hilde, Seraphine | Kesh front0, Wren front1, Hilde back0, Thessaly back1, Seraphine back2 | 100% | 100% | 0% | 75% |
| Brannoc, Seraphine, Gorran, Mordecai, Pip | Brannoc front0, Seraphine front1, Gorran back0, Mordecai back1, Pip back2 | 100% | 100% | 100% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Dagna, Kesh, Pell, Ysolde, Wren | 100% | 1:15 |
| Mordecai, Vesna, Kesh, Ysolde, Quill | 100% | 1:09 |
| Pip, Kesh, Vesna, Quill, Ysolde | 96% | 1:12 |

<details><summary>Win rate of teams that include each hero (average 87%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Pell the Gambler | 94% | +6 pts |
| Mordecai the Ferryman | 92% | +5 pts |
| Dagna Ironvow | 92% | +5 pts |
| Oriel of the Last Light | 91% | +4 pts |
| Ysolde Nightglass | 90% | +3 pts |
| Solenne of the Noon | 90% | +2 pts |
| Grandmother Hilde Ashanvil | 89% | +2 pts |
| Wren Twofaces | 89% | +2 pts |
| Seraphine of the Deep | 89% | +2 pts |
| Brannoc Thickhide | 89% | +1 pts |
| Kesh Swiftclaw | 89% | +1 pts |
| Pip the Tinker Golem | 89% | +1 pts |
| Gorran Bramblehorn | 88% | +1 pts |
| Vesna Gravebloom | 86% | -1 pts |
| Varkhul the Ashen | 83% | -4 pts |
| Maren Saltsong | 83% | -4 pts |
| Quill the Inkwright | 82% | -5 pts |
| Somna the Dreamwarden | 82% | -6 pts |
| Old Thessaly | 81% | -6 pts |
| Big Grub Marrow | 80% | -7 pts |

</details>

## The Thornwood (normal)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 78% | at most 90% |
| Median team win rate | 100% | |
| Median winning fight length | 1:19 | 1:00 to 3:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +59 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Gorran, Hilde, Somna, Vesna, Solenne | Gorran front0, Hilde front1, Somna back0, Vesna back1, Solenne back2 | 100% | 100% | 50% | 100% |
| Seraphine, Gorran, Brannoc, Dagna, Maren | Seraphine front0, Gorran front1, Brannoc back0, Dagna back1, Maren back2 | 100% | 100% | 25% | 75% |
| Ysolde, Somna, Pell, Solenne, Dagna | Somna front0, Pell front1, Solenne back0, Ysolde back1, Dagna back2 | 100% | 100% | 0% | 100% |
| Ysolde, Oriel, Grub, Kesh, Thessaly | Ysolde front0, Oriel front1, Grub back0, Kesh back1, Thessaly back2 | 100% | 100% | 50% | 100% |
| Hilde, Varkhul, Oriel, Maren, Grub | Hilde front0, Varkhul front1, Oriel back0, Maren back1, Grub back2 | 100% | 100% | 100% | 100% |
| Maren, Brannoc, Pip, Ysolde, Wren | Maren front0, Wren front1, Pip back0, Brannoc back1, Ysolde back2 | 100% | 50% | 0% | 50% |
| Maren, Solenne, Dagna, Hilde, Varkhul | Maren front0, Solenne front1, Dagna back0, Hilde back1, Varkhul back2 | 100% | 100% | 50% | 100% |
| Solenne, Seraphine, Varkhul, Grub, Mordecai | Solenne front0, Seraphine front1, Varkhul back0, Grub back1, Mordecai back2 | 100% | 100% | 50% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Thessaly, Gorran, Varkhul, Pell, Kesh | 100% | 0:46 |
| Kesh, Brannoc, Thessaly, Vesna, Varkhul | 100% | 0:50 |
| Mordecai, Vesna, Kesh, Ysolde, Quill | 97% | 0:53 |

<details><summary>Win rate of teams that include each hero (average 87%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Grandmother Hilde Ashanvil | 97% | +10 pts |
| Dagna Ironvow | 95% | +8 pts |
| Varkhul the Ashen | 95% | +8 pts |
| Seraphine of the Deep | 93% | +7 pts |
| Oriel of the Last Light | 93% | +6 pts |
| Kesh Swiftclaw | 91% | +4 pts |
| Old Thessaly | 91% | +4 pts |
| Gorran Bramblehorn | 89% | +3 pts |
| Wren Twofaces | 89% | +2 pts |
| Solenne of the Noon | 88% | +1 pts |
| Ysolde Nightglass | 88% | +1 pts |
| Mordecai the Ferryman | 87% | +0 pts |
| Vesna Gravebloom | 85% | -2 pts |
| Pell the Gambler | 85% | -2 pts |
| Brannoc Thickhide | 84% | -3 pts |
| Quill the Inkwright | 84% | -3 pts |
| Big Grub Marrow | 79% | -8 pts |
| Somna the Dreamwarden | 79% | -8 pts |
| Maren Saltsong | 78% | -9 pts |
| Pip the Tinker Golem | 68% | -18 pts |

</details>

## The Cold Foundry (hard)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 28% | at most 50% |
| Median team win rate | 50% | |
| Median winning fight length | 1:39 | 1:00 to 3:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 96% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +100 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Thessaly, Hilde, Grub, Pell, Maren | Thessaly front0, Hilde front1, Maren back0, Grub back1, Pell back2 | 100% | 0% | 0% | 0% |
| Pell, Seraphine, Brannoc, Kesh, Vesna | Brannoc front0, Vesna front1, Seraphine back0, Pell back1, Kesh back2 | 100% | 25% | 0% | 100% |
| Grub, Wren, Dagna, Seraphine, Pell | Wren front0, Dagna front1, Grub back0, Pell back1, Seraphine back2 | 100% | 0% | 0% | 50% |
| Oriel, Grub, Seraphine, Varkhul, Solenne | Varkhul front0, Grub front1, Solenne back0, Seraphine back1, Oriel back2 | 100% | 0% | 0% | 75% |
| Gorran, Dagna, Quill, Kesh, Hilde | Gorran front0, Dagna front1, Quill back0, Kesh back1, Hilde back2 | 100% | 50% | 0% | 100% |
| Wren, Pip, Seraphine, Somna, Oriel | Wren front0, Pip front1, Seraphine back0, Somna back1, Oriel back2 | 100% | 75% | 0% | 100% |
| Solenne, Grub, Pell, Wren, Kesh | Solenne front0, Grub front1, Wren back0, Pell back1, Kesh back2 | 100% | 50% | 0% | 50% |
| Varkhul, Seraphine, Hilde, Mordecai, Maren | Varkhul front0, Hilde front1, Mordecai back0, Seraphine back1, Maren back2 | 100% | 0% | 0% | 50% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Brannoc, Quill, Varkhul, Mordecai, Kesh | 96% | 0:58 |
| Hilde, Kesh, Varkhul, Ysolde, Thessaly | 91% | 1:00 |
| Oriel, Pell, Varkhul, Quill, Kesh | 83% | 0:54 |

<details><summary>Win rate of teams that include each hero (average 49%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Kesh Swiftclaw | 71% | +22 pts |
| Grandmother Hilde Ashanvil | 65% | +16 pts |
| Oriel of the Last Light | 65% | +15 pts |
| Dagna Ironvow | 60% | +10 pts |
| Mordecai the Ferryman | 57% | +8 pts |
| Gorran Bramblehorn | 56% | +7 pts |
| Quill the Inkwright | 56% | +6 pts |
| Solenne of the Noon | 53% | +4 pts |
| Pell the Gambler | 53% | +4 pts |
| Maren Saltsong | 51% | +2 pts |
| Wren Twofaces | 50% | +1 pts |
| Brannoc Thickhide | 48% | -1 pts |
| Somna the Dreamwarden | 47% | -3 pts |
| Ysolde Nightglass | 42% | -7 pts |
| Big Grub Marrow | 42% | -7 pts |
| Seraphine of the Deep | 39% | -10 pts |
| Pip the Tinker Golem | 39% | -11 pts |
| Old Thessaly | 36% | -13 pts |
| Varkhul the Ashen | 34% | -15 pts |
| Vesna Gravebloom | 30% | -20 pts |

</details>

## The Hollow Saint (hard)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 39% | at most 50% |
| Median team win rate | 67% | |
| Median winning fight length | 2:38 | 1:00 to 3:00 |
| Fights that hit the time limit | 6% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +97 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Maren, Hilde, Vesna, Mordecai, Pip | Maren front0, Hilde front1, Vesna back0, Mordecai back1, Pip back2 | 100% | 100% | 0% | 100% |
| Ysolde, Solenne, Quill, Gorran, Vesna | Vesna front0, Ysolde front1, Solenne front2, Gorran back0, Quill back1 | 100% | 0% | 0% | 0% |
| Ysolde, Kesh, Pip, Quill, Thessaly | Thessaly front0, Kesh front1, Quill front2, Pip back0, Ysolde back1 | 75% | 0% | 0% | 0% |
| Pip, Brannoc, Wren, Solenne, Hilde | Pip front0, Solenne front1, Brannoc back0, Wren back1, Hilde back2 | 100% | 25% | 0% | 0% |
| Thessaly, Dagna, Pip, Ysolde, Brannoc | Pip front0, Ysolde front1, Brannoc back0, Thessaly back1, Dagna back2 | 100% | 0% | 0% | 0% |
| Vesna, Maren, Seraphine, Ysolde, Dagna | Vesna front0, Seraphine front1, Maren back0, Ysolde back1, Dagna back2 | 100% | 100% | 0% | 100% |
| Grub, Varkhul, Solenne, Gorran, Dagna | Grub front0, Varkhul front1, Solenne back0, Gorran back1, Dagna back2 | 100% | 75% | 0% | 75% |
| Maren, Solenne, Ysolde, Vesna, Wren | Maren front0, Solenne front1, Ysolde back0, Vesna back1, Wren back2 | 100% | 25% | 0% | 50% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Solenne, Mordecai, Seraphine, Hilde, Pell | 100% | 1:38 |
| Seraphine, Ysolde, Kesh, Grub, Mordecai | 99% | 1:33 |
| Ysolde, Seraphine, Solenne, Gorran, Maren | 99% | 1:36 |

<details><summary>Win rate of teams that include each hero (average 60%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Oriel of the Last Light | 84% | +24 pts |
| Somna the Dreamwarden | 72% | +12 pts |
| Maren Saltsong | 72% | +12 pts |
| Grandmother Hilde Ashanvil | 71% | +11 pts |
| Seraphine of the Deep | 71% | +11 pts |
| Solenne of the Noon | 69% | +9 pts |
| Big Grub Marrow | 67% | +7 pts |
| Mordecai the Ferryman | 62% | +2 pts |
| Dagna Ironvow | 60% | -0 pts |
| Pell the Gambler | 58% | -2 pts |
| Gorran Bramblehorn | 58% | -2 pts |
| Wren Twofaces | 55% | -5 pts |
| Kesh Swiftclaw | 53% | -7 pts |
| Varkhul the Ashen | 53% | -7 pts |
| Ysolde Nightglass | 53% | -7 pts |
| Vesna Gravebloom | 53% | -7 pts |
| Brannoc Thickhide | 50% | -10 pts |
| Old Thessaly | 49% | -11 pts |
| Quill the Inkwright | 48% | -12 pts |
| Pip the Tinker Golem | 45% | -15 pts |

</details>

## The Hollow Saint (Nightmare) (nightmare)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 17% | at most 20% |
| Median team win rate | 8% | |
| Median winning fight length | 9:18 | 5:00 to 15:00 |
| Fights that hit the time limit | 38% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +63 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Pell, Thessaly, Seraphine, Vesna, Varkhul | Pell front0, Thessaly front1, Seraphine back0, Vesna back1, Varkhul back2 | 0% | 0% | 0% | 0% |
| Solenne, Maren, Mordecai, Varkhul, Quill | Quill front0, Varkhul front1, Mordecai back0, Maren back1, Solenne back2 | 100% | 25% | 0% | 50% |
| Seraphine, Kesh, Somna, Grub, Brannoc | Seraphine front0, Kesh front1, Somna back0, Brannoc back1, Grub back2 | 100% | 100% | 25% | 100% |
| Quill, Dagna, Solenne, Vesna, Varkhul | Quill front0, Vesna front1, Solenne front2, Dagna back0, Varkhul back1 | 50% | 0% | 0% | 0% |
| Hilde, Gorran, Solenne, Vesna, Wren | Wren front0, Hilde front1, Solenne front2, Vesna back0, Gorran back1 | 100% | 0% | 0% | 25% |
| Varkhul, Hilde, Pell, Solenne, Dagna | Varkhul front0, Hilde front1, Pell back0, Solenne back1, Dagna back2 | 0% | 0% | 0% | 0% |
| Dagna, Brannoc, Pell, Kesh, Varkhul | Pell front0, Varkhul front1, Kesh front2, Brannoc back0, Dagna back1 | 75% | 0% | 0% | 0% |
| Vesna, Hilde, Wren, Grub, Pell | Vesna front0, Hilde front1, Wren back0, Grub back1, Pell back2 | 100% | 75% | 0% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Solenne, Mordecai, Seraphine, Hilde, Pell | 100% | 4:45 |
| Ysolde, Seraphine, Solenne, Gorran, Maren | 99% | 4:17 |
| Maren, Wren, Seraphine, Solenne, Ysolde | 99% | 4:20 |

<details><summary>Win rate of teams that include each hero (average 29%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Seraphine of the Deep | 52% | +23 pts |
| Oriel of the Last Light | 42% | +13 pts |
| Mordecai the Ferryman | 41% | +12 pts |
| Maren Saltsong | 39% | +10 pts |
| Solenne of the Noon | 38% | +9 pts |
| Somna the Dreamwarden | 34% | +5 pts |
| Pell the Gambler | 32% | +3 pts |
| Dagna Ironvow | 28% | -1 pts |
| Grandmother Hilde Ashanvil | 28% | -2 pts |
| Old Thessaly | 27% | -2 pts |
| Vesna Gravebloom | 26% | -3 pts |
| Quill the Inkwright | 26% | -3 pts |
| Kesh Swiftclaw | 25% | -4 pts |
| Varkhul the Ashen | 24% | -5 pts |
| Big Grub Marrow | 24% | -5 pts |
| Wren Twofaces | 24% | -6 pts |
| Ysolde Nightglass | 23% | -6 pts |
| Gorran Bramblehorn | 22% | -7 pts |
| Brannoc Thickhide | 17% | -13 pts |
| Pip the Tinker Golem | 13% | -16 pts |

</details>

## Heroes across all stages

Average difference in win rate when a hero is in the team. Big positive numbers suggest a hero is too strong, big negative ones too weak.

| Hero | Faction | Type | Role | Difference |
|---|---|---|---|---|
| Oriel of the Last Light | Sun | Radiant | Support | +10 pts |
| Grandmother Hilde Ashanvil | Hearth | Stone | Tank | +6 pts |
| Seraphine of the Deep | Sea | Frost | Controller | +5 pts |
| Mordecai the Ferryman | Grave | Umbral | Controller | +5 pts |
| Solenne of the Noon | Sun | Radiant | Warrior | +4 pts |
| Dagna Ironvow | Forge | Stone | Tank | +4 pts |
| Kesh Swiftclaw | Wild | Storm | Ranger | +3 pts |
| Maren Saltsong | Sea | Tide | Support | +2 pts |
| Pell the Gambler | Trickster | Venom | Ranger | +1 pts |
| Gorran Bramblehorn | Wild | Verdant | Warrior | +0 pts |
| Somna the Dreamwarden | Night | Tide | Support | +0 pts |
| Wren Twofaces | Trickster | Umbral | Warrior | -1 pts |
| Ysolde Nightglass | Night | Frost | Ranger | -3 pts |
| Quill the Inkwright | Arcana | Storm | Caster | -3 pts |
| Big Grub Marrow | Hearth | Verdant | Support | -3 pts |
| Varkhul the Ashen | War | Flame | Warrior | -4 pts |
| Brannoc Thickhide | War | Metal | Tank | -4 pts |
| Old Thessaly | Arcana | Flame | Caster | -5 pts |
| Vesna Gravebloom | Grave | Venom | Warrior | -5 pts |
| Pip the Tinker Golem | Forge | Metal | Controller | -10 pts |

## Flags

- None.
