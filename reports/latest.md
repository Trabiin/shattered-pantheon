# Battle Balance Report

*Generated 2026-10-10 05:08 UTC by `dotnet run --project tools/BattleSim`. Rules: battle system draft 3 (doc 04) with the 10 factions and 10 types of doc 09. 400 random 5-hero teams × 12 fights per stage, auto-placed (tanks and warriors in front). Fight length assumes 1s per action at 1x.*

## The Ash Road (easy)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 100% | at most 100% |
| Median team win rate | 100% | |
| Median winning fight length | 0:32 | 0:00 to 1:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +0 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Pip, Oriel, Grub, Somna, Wren | Pip front0, Oriel front1, Grub back0, Somna back1, Wren back2 | 100% | 100% | 100% | 100% |
| Maren, Kesh, Jink, Oriel, Gorran | Maren front0, Kesh front1, Jink back0, Oriel back1, Gorran back2 | 100% | 100% | 100% | 100% |
| Quill, Wren, Solenne, Brannoc, Grub | Quill front0, Wren front1, Solenne back0, Brannoc back1, Grub back2 | 100% | 100% | 100% | 100% |
| Vesna, Oriel, Korvald, Grub, Kesh | Vesna front0, Oriel front1, Korvald back0, Grub back1, Kesh back2 | 100% | 100% | 100% | 100% |
| Quill, Dagna, Solenne, Hilde, Thessaly | Quill front0, Dagna front1, Solenne back0, Hilde back1, Thessaly back2 | 100% | 100% | 100% | 100% |
| Vesna, Grub, Oriel, Kesh, Wren | Vesna front0, Grub front1, Oriel back0, Kesh back1, Wren back2 | 100% | 100% | 100% | 100% |
| Seraphine, Korvald, Hilde, Quill, Gorran | Seraphine front0, Korvald front1, Hilde back0, Quill back1, Gorran back2 | 100% | 100% | 100% | 100% |
| Maren, Quill, Brannoc, Dagna, Somna | Maren front0, Quill front1, Brannoc back0, Dagna back1, Somna back2 | 100% | 100% | 100% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Ysolde, Quill, Seraphine, Kesh, Solenne | 100% | 0:20 |
| Kesh, Somna, Korvald, Ysolde, Thessaly | 100% | 0:21 |
| Hilde, Kesh, Korvald, Ysolde, Thessaly | 100% | 0:22 |

<details><summary>Win rate of teams that include each hero (average 100%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Solenne of the Noon | 100% | +0 pts |
| Oriel of the Last Light | 100% | +0 pts |
| Mordecai the Ferryman | 100% | +0 pts |
| Vesna Gravebloom | 100% | +0 pts |
| Korvald the Ashen | 100% | +0 pts |
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
| Jink the Gambler | 100% | +0 pts |
| Wren Twofaces | 100% | +0 pts |
| Ysolde Nightglass | 100% | +0 pts |
| Somna the Dreamwarden | 100% | +0 pts |

</details>

## The Drowned Chapel (normal)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 80% | at most 90% |
| Median team win rate | 100% | |
| Median winning fight length | 2:11 | 1:00 to 3:00 |
| Fights that hit the time limit | 1% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +56 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Seraphine, Vesna, Pip, Maren, Thessaly | Vesna front0, Seraphine front1, Pip back0, Maren back1, Thessaly back2 | 100% | 50% | 0% | 100% |
| Korvald, Brannoc, Seraphine, Quill, Oriel | Korvald front0, Brannoc front1, Seraphine back0, Quill back1, Oriel back2 | 100% | 100% | 50% | 100% |
| Gorran, Dagna, Brannoc, Pip, Jink | Gorran front0, Dagna front1, Brannoc back0, Pip back1, Jink back2 | 100% | 100% | 100% | 100% |
| Mordecai, Korvald, Vesna, Brannoc, Wren | Mordecai front0, Korvald front1, Brannoc back0, Vesna back1, Wren back2 | 100% | 100% | 50% | 100% |
| Kesh, Gorran, Hilde, Grub, Jink | Kesh front0, Gorran front1, Hilde back0, Grub back1, Jink back2 | 100% | 100% | 50% | 100% |
| Pip, Jink, Dagna, Grub, Maren | Pip front0, Jink front1, Dagna back0, Grub back1, Maren back2 | 100% | 100% | 0% | 100% |
| Kesh, Thessaly, Wren, Hilde, Seraphine | Kesh front0, Thessaly front1, Hilde back0, Seraphine back1, Wren back2 | 100% | 100% | 25% | 75% |
| Brannoc, Seraphine, Gorran, Mordecai, Pip | Brannoc front0, Seraphine front1, Gorran back0, Mordecai back1, Pip back2 | 100% | 100% | 75% | 75% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Dagna, Kesh, Jink, Ysolde, Wren | 100% | 1:09 |
| Ysolde, Gorran, Seraphine, Jink, Mordecai | 100% | 1:07 |
| Mordecai, Vesna, Kesh, Ysolde, Quill | 100% | 1:08 |

<details><summary>Win rate of teams that include each hero (average 87%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Jink the Gambler | 95% | +8 pts |
| Mordecai the Ferryman | 94% | +7 pts |
| Dagna Ironvow | 92% | +5 pts |
| Kesh Swiftclaw | 91% | +4 pts |
| Pip the Tinker Golem | 91% | +4 pts |
| Solenne of the Noon | 91% | +3 pts |
| Oriel of the Last Light | 90% | +3 pts |
| Grandmother Hilde Ashanvil | 90% | +3 pts |
| Gorran Bramblehorn | 89% | +2 pts |
| Ysolde Nightglass | 89% | +1 pts |
| Brannoc Thickhide | 88% | +1 pts |
| Wren Twofaces | 88% | +0 pts |
| Seraphine of the Deep | 87% | -1 pts |
| Vesna Gravebloom | 86% | -1 pts |
| Somna the Dreamwarden | 84% | -3 pts |
| Korvald the Ashen | 83% | -4 pts |
| Maren Saltsong | 82% | -6 pts |
| Big Grub Marrow | 81% | -7 pts |
| Old Thessaly | 79% | -8 pts |
| Quill the Inkwright | 79% | -8 pts |

</details>

## The Thornwood (normal)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 79% | at most 90% |
| Median team win rate | 100% | |
| Median winning fight length | 1:17 | 1:00 to 3:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +56 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Gorran, Hilde, Somna, Vesna, Solenne | Gorran front0, Hilde front1, Vesna back0, Solenne back1, Somna back2 | 100% | 100% | 25% | 100% |
| Seraphine, Gorran, Brannoc, Dagna, Maren | Seraphine front0, Gorran front1, Brannoc back0, Dagna back1, Maren back2 | 100% | 100% | 50% | 100% |
| Ysolde, Somna, Jink, Solenne, Dagna | Ysolde front0, Somna front1, Solenne back0, Jink back1, Dagna back2 | 100% | 100% | 0% | 100% |
| Ysolde, Oriel, Grub, Kesh, Thessaly | Ysolde front0, Oriel front1, Grub back0, Kesh back1, Thessaly back2 | 100% | 100% | 50% | 100% |
| Hilde, Korvald, Oriel, Maren, Grub | Hilde front0, Korvald front1, Oriel back0, Maren back1, Grub back2 | 100% | 100% | 100% | 100% |
| Maren, Brannoc, Pip, Ysolde, Wren | Maren front0, Wren front1, Pip back0, Brannoc back1, Ysolde back2 | 100% | 50% | 0% | 75% |
| Maren, Solenne, Dagna, Hilde, Korvald | Maren front0, Solenne front1, Dagna back0, Hilde back1, Korvald back2 | 100% | 100% | 75% | 100% |
| Solenne, Seraphine, Korvald, Grub, Mordecai | Solenne front0, Seraphine front1, Korvald back0, Grub back1, Mordecai back2 | 100% | 100% | 50% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Thessaly, Gorran, Korvald, Jink, Kesh | 100% | 0:43 |
| Kesh, Brannoc, Thessaly, Vesna, Korvald | 100% | 0:50 |
| Ysolde, Quill, Seraphine, Kesh, Solenne | 96% | 0:51 |

<details><summary>Win rate of teams that include each hero (average 88%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Grandmother Hilde Ashanvil | 97% | +9 pts |
| Korvald the Ashen | 95% | +8 pts |
| Dagna Ironvow | 95% | +7 pts |
| Oriel of the Last Light | 94% | +6 pts |
| Kesh Swiftclaw | 93% | +6 pts |
| Old Thessaly | 91% | +3 pts |
| Seraphine of the Deep | 91% | +3 pts |
| Solenne of the Noon | 90% | +3 pts |
| Gorran Bramblehorn | 90% | +2 pts |
| Wren Twofaces | 89% | +1 pts |
| Ysolde Nightglass | 89% | +1 pts |
| Mordecai the Ferryman | 89% | +1 pts |
| Vesna Gravebloom | 86% | -2 pts |
| Jink the Gambler | 86% | -2 pts |
| Brannoc Thickhide | 85% | -3 pts |
| Quill the Inkwright | 83% | -5 pts |
| Maren Saltsong | 83% | -5 pts |
| Somna the Dreamwarden | 81% | -7 pts |
| Big Grub Marrow | 81% | -7 pts |
| Pip the Tinker Golem | 71% | -17 pts |

</details>

## The Cold Foundry (hard)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 19% | at most 50% |
| Median team win rate | 33% | |
| Median winning fight length | 1:34 | 1:00 to 3:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +100 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Thessaly, Hilde, Grub, Jink, Maren | Thessaly front0, Hilde front1, Grub back0, Maren back1, Jink back2 | 100% | 0% | 0% | 75% |
| Jink, Seraphine, Brannoc, Kesh, Vesna | Brannoc front0, Vesna front1, Seraphine back0, Kesh back1, Jink back2 | 100% | 25% | 0% | 25% |
| Grub, Wren, Dagna, Seraphine, Jink | Grub front0, Dagna front1, Jink back0, Seraphine back1, Wren back2 | 100% | 0% | 0% | 50% |
| Oriel, Grub, Seraphine, Korvald, Solenne | Solenne front0, Grub front1, Oriel back0, Korvald back1, Seraphine back2 | 100% | 0% | 0% | 75% |
| Gorran, Dagna, Quill, Kesh, Hilde | Gorran front0, Dagna front1, Quill back0, Kesh back1, Hilde back2 | 100% | 25% | 0% | 100% |
| Wren, Pip, Seraphine, Somna, Oriel | Wren front0, Pip front1, Seraphine back0, Somna back1, Oriel back2 | 100% | 50% | 0% | 75% |
| Solenne, Grub, Jink, Wren, Kesh | Solenne front0, Grub front1, Kesh back0, Jink back1, Wren back2 | 100% | 50% | 0% | 100% |
| Korvald, Seraphine, Hilde, Mordecai, Maren | Korvald front0, Hilde front1, Seraphine back0, Maren back1, Mordecai back2 | 100% | 0% | 0% | 50% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Thessaly, Solenne, Kesh, Oriel, Brannoc | 100% | 1:08 |
| Solenne, Kesh, Gorran, Ysolde, Mordecai | 92% | 1:02 |
| Brannoc, Quill, Korvald, Mordecai, Kesh | 89% | 0:57 |

<details><summary>Win rate of teams that include each hero (average 41%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Kesh Swiftclaw | 62% | +21 pts |
| Oriel of the Last Light | 58% | +18 pts |
| Grandmother Hilde Ashanvil | 58% | +17 pts |
| Mordecai the Ferryman | 54% | +14 pts |
| Dagna Ironvow | 51% | +10 pts |
| Solenne of the Noon | 46% | +6 pts |
| Wren Twofaces | 45% | +4 pts |
| Gorran Bramblehorn | 43% | +2 pts |
| Maren Saltsong | 42% | +1 pts |
| Somna the Dreamwarden | 41% | +0 pts |
| Jink the Gambler | 39% | -1 pts |
| Ysolde Nightglass | 38% | -3 pts |
| Brannoc Thickhide | 36% | -4 pts |
| Big Grub Marrow | 35% | -6 pts |
| Quill the Inkwright | 32% | -9 pts |
| Old Thessaly | 31% | -10 pts |
| Seraphine of the Deep | 29% | -12 pts |
| Pip the Tinker Golem | 29% | -12 pts |
| Korvald the Ashen | 28% | -13 pts |
| Vesna Gravebloom | 24% | -16 pts |

</details>

## The Hollow Saint (hard)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 40% | at most 50% |
| Median team win rate | 67% | |
| Median winning fight length | 2:37 | 1:00 to 3:00 |
| Fights that hit the time limit | 6% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +91 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Maren, Hilde, Vesna, Mordecai, Pip | Maren front0, Hilde front1, Vesna back0, Mordecai back1, Pip back2 | 100% | 100% | 0% | 100% |
| Ysolde, Solenne, Quill, Gorran, Vesna | Ysolde front0, Quill front1, Solenne front2, Vesna back0, Gorran back1 | 50% | 0% | 0% | 0% |
| Ysolde, Kesh, Pip, Quill, Thessaly | Thessaly front0, Quill front1, Ysolde front2, Pip back0, Kesh back1 | 75% | 0% | 0% | 0% |
| Pip, Brannoc, Wren, Solenne, Hilde | Pip front0, Wren front1, Brannoc back0, Solenne back1, Hilde back2 | 100% | 25% | 0% | 50% |
| Thessaly, Dagna, Pip, Ysolde, Brannoc | Pip front0, Thessaly front1, Brannoc back0, Dagna back1, Ysolde back2 | 100% | 0% | 0% | 0% |
| Vesna, Maren, Seraphine, Ysolde, Dagna | Vesna front0, Ysolde front1, Maren back0, Seraphine back1, Dagna back2 | 100% | 100% | 0% | 75% |
| Grub, Korvald, Solenne, Gorran, Dagna | Grub front0, Solenne front1, Gorran back0, Korvald back1, Dagna back2 | 100% | 75% | 0% | 75% |
| Maren, Solenne, Ysolde, Vesna, Wren | Maren front0, Solenne front1, Ysolde back0, Wren back1, Vesna back2 | 100% | 50% | 0% | 50% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Seraphine, Ysolde, Kesh, Grub, Mordecai | 100% | 1:31 |
| Jink, Oriel, Grub, Kesh, Thessaly | 97% | 1:34 |
| Seraphine, Oriel, Thessaly, Ysolde, Jink | 91% | 1:43 |

<details><summary>Win rate of teams that include each hero (average 59%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Oriel of the Last Light | 81% | +22 pts |
| Somna the Dreamwarden | 77% | +19 pts |
| Solenne of the Noon | 73% | +14 pts |
| Grandmother Hilde Ashanvil | 71% | +12 pts |
| Big Grub Marrow | 70% | +11 pts |
| Seraphine of the Deep | 64% | +6 pts |
| Mordecai the Ferryman | 63% | +5 pts |
| Maren Saltsong | 62% | +3 pts |
| Jink the Gambler | 58% | -0 pts |
| Gorran Bramblehorn | 56% | -3 pts |
| Wren Twofaces | 56% | -3 pts |
| Dagna Ironvow | 55% | -3 pts |
| Vesna Gravebloom | 52% | -6 pts |
| Kesh Swiftclaw | 52% | -7 pts |
| Ysolde Nightglass | 50% | -8 pts |
| Old Thessaly | 49% | -10 pts |
| Brannoc Thickhide | 48% | -11 pts |
| Korvald the Ashen | 47% | -11 pts |
| Pip the Tinker Golem | 46% | -12 pts |
| Quill the Inkwright | 43% | -15 pts |

</details>

## The Hollow Saint (Nightmare) (nightmare)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 12% | at most 20% |
| Median team win rate | 0% | |
| Median winning fight length | 8:48 | 5:00 to 15:00 |
| Fights that hit the time limit | 42% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +69 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Jink, Thessaly, Seraphine, Vesna, Korvald | Jink front0, Thessaly front1, Seraphine back0, Vesna back1, Korvald back2 | 0% | 0% | 0% | 0% |
| Solenne, Maren, Mordecai, Korvald, Quill | Maren front0, Solenne front1, Mordecai back0, Korvald back1, Quill back2 | 100% | 25% | 0% | 75% |
| Seraphine, Kesh, Somna, Grub, Brannoc | Seraphine front0, Kesh front1, Somna back0, Brannoc back1, Grub back2 | 100% | 100% | 25% | 75% |
| Quill, Dagna, Solenne, Vesna, Korvald | Quill front0, Vesna front1, Solenne front2, Dagna back0, Korvald back1 | 50% | 0% | 0% | 0% |
| Hilde, Gorran, Solenne, Vesna, Wren | Vesna front0, Hilde front1, Solenne front2, Wren back0, Gorran back1 | 100% | 0% | 0% | 0% |
| Korvald, Hilde, Jink, Solenne, Dagna | Korvald front0, Jink front1, Solenne front2, Dagna back0, Hilde back1 | 50% | 0% | 0% | 0% |
| Dagna, Brannoc, Jink, Kesh, Korvald | Jink front0, Kesh front1, Dagna back0, Brannoc back1, Korvald back2 | 75% | 0% | 0% | 0% |
| Vesna, Hilde, Wren, Grub, Jink | Vesna front0, Hilde front1, Wren back0, Grub back1, Jink back2 | 100% | 75% | 0% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Solenne, Mordecai, Seraphine, Hilde, Jink | 100% | 4:46 |
| Jink, Hilde, Mordecai, Kesh, Maren | 100% | 6:41 |
| Oriel, Kesh, Seraphine, Gorran, Mordecai | 95% | 4:43 |

<details><summary>Win rate of teams that include each hero (average 24%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Mordecai the Ferryman | 40% | +16 pts |
| Solenne of the Noon | 39% | +15 pts |
| Seraphine of the Deep | 37% | +13 pts |
| Oriel of the Last Light | 35% | +11 pts |
| Somna the Dreamwarden | 33% | +10 pts |
| Jink the Gambler | 30% | +6 pts |
| Grandmother Hilde Ashanvil | 23% | -0 pts |
| Kesh Swiftclaw | 23% | -1 pts |
| Old Thessaly | 23% | -1 pts |
| Dagna Ironvow | 23% | -1 pts |
| Vesna Gravebloom | 20% | -4 pts |
| Quill the Inkwright | 20% | -4 pts |
| Ysolde Nightglass | 19% | -5 pts |
| Korvald the Ashen | 19% | -5 pts |
| Gorran Bramblehorn | 19% | -5 pts |
| Wren Twofaces | 19% | -5 pts |
| Maren Saltsong | 18% | -5 pts |
| Big Grub Marrow | 18% | -6 pts |
| Pip the Tinker Golem | 14% | -10 pts |
| Brannoc Thickhide | 9% | -15 pts |

</details>

## Heroes across all stages

Average difference in win rate when a hero is in the team. Big positive numbers suggest a hero is too strong, big negative ones too weak. Heroes fight at their listed stats here, whatever their rarity: rarity's stat multiplier is a progression rule (`progression.json`), so this compares kits.

| Hero | Rarity | Faction | Type | Role | Difference |
|---|---|---|---|---|---|
| Oriel of the Last Light | Epic | Sun | Radiant | Support | +10 pts |
| Mordecai the Ferryman | Legendary | Grave | Umbral | Controller | +7 pts |
| Grandmother Hilde Ashanvil | Rare | Hearth | Stone | Tank | +7 pts |
| Solenne of the Noon | Legendary | Sun | Radiant | Warrior | +7 pts |
| Kesh Swiftclaw | Common | Wild | Storm | Ranger | +4 pts |
| Somna the Dreamwarden | Common | Night | Tide | Support | +3 pts |
| Dagna Ironvow | Epic | Forge | Stone | Tank | +3 pts |
| Jink the Gambler | Uncommon | Trickster | Venom | Ranger | +2 pts |
| Seraphine of the Deep | Rare | Sea | Frost | Controller | +2 pts |
| Wren Twofaces | Epic | Trickster | Umbral | Warrior | -0 pts |
| Gorran Bramblehorn | Common | Wild | Verdant | Warrior | -0 pts |
| Maren Saltsong | Uncommon | Sea | Tide | Support | -2 pts |
| Ysolde Nightglass | Rare | Night | Frost | Ranger | -2 pts |
| Big Grub Marrow | Rare | Hearth | Verdant | Support | -2 pts |
| Old Thessaly | Epic | Arcana | Flame | Caster | -4 pts |
| Korvald the Ashen | Rare | War | Flame | Warrior | -4 pts |
| Vesna Gravebloom | Uncommon | Grave | Venom | Warrior | -5 pts |
| Brannoc Thickhide | Common | War | Metal | Tank | -5 pts |
| Quill the Inkwright | Uncommon | Arcana | Storm | Caster | -7 pts |
| Pip the Tinker Golem | Uncommon | Forge | Metal | Controller | -8 pts |

## Rarities

For each rarity, the stages where one of its heroes lifts a team's win rate more than any other hero does (stages that every team wins don't count).

| Rarity | Heroes | Best answer on |
|---|---|---|
| Common | 4 | The Cold Foundry (Kesh Swiftclaw) |
| Uncommon | 5 | The Drowned Chapel (Jink the Gambler) |
| Rare | 5 | The Thornwood (Grandmother Hilde Ashanvil) |
| Epic | 4 | The Hollow Saint (Oriel of the Last Light) |
| Legendary | 2 | The Hollow Saint (Nightmare) (Mordecai the Ferryman) |

## Flags

- None.
