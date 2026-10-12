# Battle Balance Report

*Generated 2026-10-12 11:26 UTC by `dotnet run --project tools/BattleSim`. Rules: battle system draft 3 (doc 04) with the 10 factions and 10 types of doc 09. 400 random 5-hero teams × 12 fights per stage, auto-placed (tanks and warriors in front). Each slot is any hero, so about 4 teams in 10 hold copies of a hero (a team may field up to 5). Fight length assumes 1s per action at 1x.*

## The Ash Road (easy)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 100% | at most 100% |
| Median team win rate | 100% | |
| Median winning fight length | 0:31 | 0:00 to 1:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +0 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Jink, Thessaly, Jink, Seraphine, Maren | Jink front0, Thessaly front1, Jink back0, Seraphine back1, Maren back2 | 100% | 100% | 100% | 100% |
| Korvald, Somna, Thessaly, Thessaly, Maren | Korvald front0, Somna front1, Thessaly back0, Thessaly back1, Maren back2 | 100% | 100% | 100% | 100% |
| Dagna, Pip, Thessaly, Mordecai, Grub | Dagna front0, Pip front1, Thessaly back0, Mordecai back1, Grub back2 | 100% | 100% | 100% | 100% |
| Maren, Jink, Jink, Jink, Hilde | Maren front0, Jink front1, Jink back0, Jink back1, Hilde back2 | 100% | 100% | 100% | 100% |
| Ysolde, Wren, Vesna, Ysolde, Wren | Ysolde front0, Wren front1, Vesna back0, Ysolde back1, Wren back2 | 100% | 100% | 100% | 100% |
| Solenne, Solenne, Somna, Korvald, Brannoc | Solenne front0, Solenne front1, Somna back0, Korvald back1, Brannoc back2 | 100% | 100% | 100% | 100% |
| Gorran, Somna, Wren, Jink, Gorran | Gorran front0, Somna front1, Wren back0, Jink back1, Gorran back2 | 100% | 100% | 100% | 100% |
| Brannoc, Dagna, Jink, Hilde, Mordecai | Brannoc front0, Dagna front1, Jink back0, Hilde back1, Mordecai back2 | 100% | 100% | 100% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Kesh, Kesh, Ysolde, Kesh, Solenne | 100% | 0:17 |
| Kesh, Korvald, Korvald, Ysolde, Vesna | 100% | 0:18 |
| Korvald, Kesh, Korvald, Grub, Thessaly | 100% | 0:21 |

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
| Random teams that win at least 80% | 77% | at most 90% |
| Median team win rate | 100% | |
| Median winning fight length | 2:06 | 1:00 to 3:00 |
| Fights that hit the time limit | 3% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +66 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Grub, Brannoc, Brannoc, Vesna, Grub | Grub front0, Brannoc front1, Brannoc back0, Vesna back1, Grub back2 | 0% | 0% | 0% | 0% |
| Kesh, Jink, Grub, Solenne, Mordecai | Kesh front0, Jink front1, Grub back0, Solenne back1, Mordecai back2 | 100% | 100% | 50% | 100% |
| Somna, Wren, Hilde, Jink, Gorran | Somna front0, Wren front1, Hilde back0, Jink back1, Gorran back2 | 100% | 100% | 50% | 100% |
| Pip, Maren, Hilde, Dagna, Pip | Pip front0, Maren front1, Hilde back0, Pip back1, Dagna back2 | 100% | 75% | 0% | 75% |
| Quill, Gorran, Mordecai, Grub, Thessaly | Gorran front0, Grub front1, Quill front2, Thessaly back0, Mordecai back1 | 100% | 25% | 0% | 25% |
| Quill, Brannoc, Hilde, Brannoc, Ysolde | Quill front0, Brannoc front1, Hilde back0, Ysolde back1, Brannoc back2 | 100% | 75% | 25% | 75% |
| Ysolde, Oriel, Grub, Oriel, Korvald | Ysolde front0, Oriel front1, Grub back0, Oriel back1, Korvald back2 | 100% | 100% | 25% | 100% |
| Seraphine, Wren, Gorran, Oriel, Ysolde | Seraphine front0, Wren front1, Gorran back0, Oriel back1, Ysolde back2 | 100% | 100% | 25% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Kesh, Kesh, Ysolde, Kesh, Solenne | 100% | 0:50 |
| Korvald, Jink, Kesh, Kesh, Solenne | 100% | 0:54 |
| Pip, Kesh, Kesh, Kesh, Dagna | 100% | 0:54 |

<details><summary>Win rate of teams that include each hero (average 84%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Jink the Gambler | 94% | +9 pts |
| Ysolde Nightglass | 93% | +9 pts |
| Oriel of the Last Light | 92% | +8 pts |
| Kesh Swiftclaw | 92% | +7 pts |
| Dagna Ironvow | 91% | +7 pts |
| Mordecai the Ferryman | 90% | +6 pts |
| Brannoc Thickhide | 89% | +5 pts |
| Pip the Tinker Golem | 88% | +4 pts |
| Wren Twofaces | 87% | +3 pts |
| Grandmother Hilde Ashanvil | 87% | +3 pts |
| Solenne of the Noon | 86% | +1 pts |
| Vesna Gravebloom | 84% | +0 pts |
| Seraphine of the Deep | 84% | -1 pts |
| Old Thessaly | 82% | -2 pts |
| Gorran Bramblehorn | 79% | -5 pts |
| Maren Saltsong | 78% | -6 pts |
| Somna the Dreamwarden | 75% | -9 pts |
| Korvald the Ashen | 74% | -11 pts |
| Big Grub Marrow | 73% | -11 pts |
| Quill the Inkwright | 67% | -17 pts |

</details>

## The Thornwood (normal)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 82% | at most 90% |
| Median team win rate | 100% | |
| Median winning fight length | 1:14 | 1:00 to 3:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +75 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Wren, Jink, Quill, Seraphine, Vesna | Wren front0, Jink front1, Seraphine back0, Quill back1, Vesna back2 | 100% | 75% | 0% | 100% |
| Quill, Vesna, Hilde, Maren, Mordecai | Quill front0, Vesna front1, Hilde back0, Maren back1, Mordecai back2 | 100% | 100% | 50% | 100% |
| Jink, Gorran, Korvald, Quill, Korvald | Jink front0, Gorran front1, Korvald back0, Quill back1, Korvald back2 | 100% | 100% | 50% | 100% |
| Solenne, Jink, Quill, Maren, Somna | Solenne front0, Maren front1, Somna back0, Jink back1, Quill back2 | 100% | 50% | 0% | 50% |
| Korvald, Dagna, Gorran, Pip, Kesh | Korvald front0, Dagna front1, Gorran back0, Pip back1, Kesh back2 | 100% | 100% | 0% | 100% |
| Maren, Grub, Dagna, Maren, Solenne | Solenne front0, Maren front1, Dagna back0, Grub back1, Maren back2 | 100% | 75% | 0% | 25% |
| Wren, Hilde, Kesh, Hilde, Somna | Wren front0, Hilde front1, Kesh back0, Hilde back1, Somna back2 | 100% | 100% | 75% | 100% |
| Somna, Hilde, Vesna, Grub, Grub | Somna front0, Vesna front1, Hilde back0, Grub back1, Grub back2 | 100% | 100% | 25% | 100% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Korvald, Jink, Kesh, Kesh, Solenne | 100% | 0:44 |
| Kesh, Kesh, Ysolde, Kesh, Solenne | 100% | 0:43 |
| Korvald, Korvald, Quill, Wren, Jink | 100% | 0:46 |

<details><summary>Win rate of teams that include each hero (average 90%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Grandmother Hilde Ashanvil | 98% | +8 pts |
| Oriel of the Last Light | 97% | +7 pts |
| Korvald the Ashen | 95% | +5 pts |
| Ysolde Nightglass | 93% | +3 pts |
| Kesh Swiftclaw | 93% | +3 pts |
| Dagna Ironvow | 93% | +3 pts |
| Seraphine of the Deep | 92% | +2 pts |
| Wren Twofaces | 92% | +2 pts |
| Old Thessaly | 91% | +1 pts |
| Solenne of the Noon | 91% | +1 pts |
| Mordecai the Ferryman | 90% | +0 pts |
| Gorran Bramblehorn | 90% | +0 pts |
| Brannoc Thickhide | 90% | +0 pts |
| Vesna Gravebloom | 89% | -1 pts |
| Jink the Gambler | 88% | -2 pts |
| Big Grub Marrow | 87% | -3 pts |
| Maren Saltsong | 85% | -5 pts |
| Quill the Inkwright | 85% | -5 pts |
| Somna the Dreamwarden | 82% | -8 pts |
| Pip the Tinker Golem | 74% | -15 pts |

</details>

## The Cold Foundry (hard)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 26% | at most 50% |
| Median team win rate | 42% | |
| Median winning fight length | 1:30 | 1:00 to 3:00 |
| Fights that hit the time limit | 0% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +75 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Wren, Quill, Mordecai, Solenne, Korvald | Korvald front0, Quill front1, Mordecai back0, Solenne back1, Wren back2 | 100% | 0% | 0% | 25% |
| Dagna, Kesh, Hilde, Vesna, Hilde | Dagna front0, Hilde front1, Kesh back0, Vesna back1, Hilde back2 | 100% | 0% | 0% | 100% |
| Grub, Dagna, Grub, Vesna, Hilde | Dagna front0, Hilde front1, Grub back0, Grub back1, Vesna back2 | 100% | 0% | 0% | 50% |
| Ysolde, Vesna, Brannoc, Dagna, Solenne | Vesna front0, Dagna front1, Brannoc back0, Ysolde back1, Solenne back2 | 25% | 0% | 0% | 0% |
| Korvald, Korvald, Wren, Oriel, Oriel | Korvald front0, Korvald front1, Wren back0, Oriel back1, Oriel back2 | 100% | 75% | 25% | 50% |
| Vesna, Somna, Maren, Maren, Gorran | Vesna front0, Somna front1, Maren back0, Maren back1, Gorran back2 | 75% | 0% | 0% | 25% |
| Quill, Somna, Maren, Hilde, Maren | Quill front0, Somna front1, Maren back0, Maren back1, Hilde back2 | 100% | 50% | 0% | 100% |
| Ysolde, Seraphine, Quill, Gorran, Grub | Seraphine front0, Gorran front1, Quill back0, Grub back1, Ysolde back2 | 25% | 0% | 0% | 0% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Pip, Kesh, Kesh, Kesh, Dagna | 100% | 0:46 |
| Grub, Ysolde, Wren, Kesh, Kesh | 98% | 0:55 |
| Kesh, Kesh, Ysolde, Kesh, Solenne | 97% | 0:47 |

<details><summary>Win rate of teams that include each hero (average 47%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Kesh Swiftclaw | 72% | +25 pts |
| Oriel of the Last Light | 68% | +21 pts |
| Grandmother Hilde Ashanvil | 68% | +21 pts |
| Mordecai the Ferryman | 62% | +15 pts |
| Dagna Ironvow | 56% | +9 pts |
| Somna the Dreamwarden | 49% | +3 pts |
| Brannoc Thickhide | 48% | +1 pts |
| Ysolde Nightglass | 47% | -0 pts |
| Solenne of the Noon | 46% | -1 pts |
| Jink the Gambler | 45% | -1 pts |
| Wren Twofaces | 45% | -2 pts |
| Maren Saltsong | 45% | -2 pts |
| Big Grub Marrow | 44% | -3 pts |
| Pip the Tinker Golem | 42% | -5 pts |
| Old Thessaly | 40% | -7 pts |
| Quill the Inkwright | 37% | -10 pts |
| Vesna Gravebloom | 33% | -14 pts |
| Gorran Bramblehorn | 32% | -14 pts |
| Seraphine of the Deep | 30% | -17 pts |
| Korvald the Ashen | 26% | -20 pts |

</details>

## The Hollow Saint (hard)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 39% | at most 50% |
| Median team win rate | 67% | |
| Median winning fight length | 2:23 | 1:00 to 3:00 |
| Fights that hit the time limit | 5% | |
| Best sampled team, re-run 300 times | 98% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +84 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Jink, Quill, Gorran, Hilde, Gorran | Jink front0, Gorran front1, Gorran front2, Hilde back0, Quill back1 | 100% | 50% | 0% | 50% |
| Oriel, Oriel, Solenne, Somna, Kesh | Oriel front0, Oriel front1, Solenne back0, Somna back1, Kesh back2 | 100% | 100% | 100% | 100% |
| Ysolde, Jink, Mordecai, Vesna, Somna | Ysolde front0, Jink front1, Mordecai back0, Vesna back1, Somna back2 | 100% | 100% | 0% | 100% |
| Jink, Thessaly, Jink, Ysolde, Jink | Jink front0, Jink front1, Thessaly front2, Jink back0, Ysolde back1 | 100% | 50% | 0% | 25% |
| Maren, Somna, Mordecai, Hilde, Vesna | Maren front0, Somna front1, Mordecai back0, Hilde back1, Vesna back2 | 100% | 100% | 25% | 100% |
| Somna, Maren, Hilde, Korvald, Jink | Somna front0, Hilde front1, Maren back0, Korvald back1, Jink back2 | 100% | 100% | 0% | 100% |
| Thessaly, Somna, Korvald, Grub, Quill | Thessaly front0, Korvald front1, Somna back0, Grub back1, Quill back2 | 100% | 75% | 0% | 75% |
| Brannoc, Somna, Korvald, Vesna, Thessaly | Somna front0, Thessaly front1, Korvald front2, Vesna back0, Brannoc back1 | 100% | 25% | 0% | 25% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Kesh, Dagna, Grub, Mordecai, Kesh | 98% | 1:17 |
| Jink, Somna, Solenne, Mordecai, Kesh | 97% | 1:12 |
| Jink, Mordecai, Jink, Mordecai, Seraphine | 93% | 1:07 |

<details><summary>Win rate of teams that include each hero (average 58%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Oriel of the Last Light | 85% | +27 pts |
| Somna the Dreamwarden | 75% | +17 pts |
| Big Grub Marrow | 75% | +17 pts |
| Mordecai the Ferryman | 68% | +10 pts |
| Maren Saltsong | 68% | +10 pts |
| Seraphine of the Deep | 68% | +10 pts |
| Solenne of the Noon | 65% | +7 pts |
| Grandmother Hilde Ashanvil | 65% | +7 pts |
| Jink the Gambler | 60% | +2 pts |
| Gorran Bramblehorn | 55% | -3 pts |
| Wren Twofaces | 54% | -4 pts |
| Kesh Swiftclaw | 53% | -5 pts |
| Ysolde Nightglass | 53% | -5 pts |
| Vesna Gravebloom | 51% | -7 pts |
| Dagna Ironvow | 51% | -7 pts |
| Old Thessaly | 48% | -10 pts |
| Korvald the Ashen | 46% | -12 pts |
| Brannoc Thickhide | 46% | -12 pts |
| Quill the Inkwright | 45% | -13 pts |
| Pip the Tinker Golem | 44% | -14 pts |

</details>

## The Hollow Saint (Nightmare) (nightmare)

| Measure | Value | Target |
|---|---|---|
| Random teams that win at least 80% | 14% | at most 20% |
| Median team win rate | 0% | |
| Median winning fight length | 8:44 | 5:00 to 15:00 |
| Fights that hit the time limit | 38% | |
| Best sampled team, re-run 300 times | 100% | at least 85% |

Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by +56 pts on average.

| Team | Best arrangement | Best | Median | Worst | Auto-placed |
|---|---|---|---|---|---|
| Somna, Somna, Thessaly, Seraphine, Quill | Thessaly front0, Somna front1, Somna back0, Seraphine back1, Quill back2 | 100% | 50% | 0% | 75% |
| Hilde, Maren, Solenne, Ysolde, Mordecai | Ysolde front0, Solenne front1, Hilde back0, Maren back1, Mordecai back2 | 100% | 0% | 0% | 25% |
| Somna, Seraphine, Oriel, Grub, Oriel | Oriel front0, Grub front1, Oriel front2, Somna back0, Seraphine back1 | 50% | 0% | 0% | 0% |
| Somna, Jink, Quill, Solenne, Seraphine | Somna front0, Solenne front1, Jink back0, Quill back1, Seraphine back2 | 100% | 50% | 0% | 25% |
| Grub, Ysolde, Dagna, Gorran, Thessaly | Grub front0, Dagna front1, Ysolde back0, Thessaly back1, Gorran back2 | 25% | 0% | 0% | 0% |
| Maren, Vesna, Gorran, Maren, Maren | Maren front0, Vesna front1, Gorran back0, Maren back1, Maren back2 | 0% | 0% | 0% | 0% |
| Thessaly, Seraphine, Ysolde, Pip, Seraphine | Thessaly front0, Ysolde front1, Seraphine front2, Pip back0, Seraphine back1 | 25% | 0% | 0% | 0% |
| Quill, Somna, Quill, Quill, Ysolde | Quill front0, Somna front1, Ysolde front2, Quill back0, Quill back1 | 50% | 0% | 0% | 0% |

Best sampled teams (300 fights each):

| Team | Win rate | Avg winning fight |
|---|---|---|
| Kesh, Dagna, Grub, Mordecai, Kesh | 100% | 4:09 |
| Jink, Somna, Solenne, Mordecai, Kesh | 96% | 3:51 |
| Gorran, Seraphine, Solenne, Mordecai, Seraphine | 81% | 4:02 |

<details><summary>Win rate of teams that include each hero (average 25%)</summary>

| Hero | Win rate with hero | Difference |
|---|---|---|
| Mordecai the Ferryman | 51% | +26 pts |
| Seraphine of the Deep | 41% | +16 pts |
| Oriel of the Last Light | 34% | +9 pts |
| Jink the Gambler | 31% | +5 pts |
| Somna the Dreamwarden | 31% | +5 pts |
| Big Grub Marrow | 29% | +4 pts |
| Solenne of the Noon | 29% | +4 pts |
| Kesh Swiftclaw | 28% | +2 pts |
| Vesna Gravebloom | 26% | +1 pts |
| Grandmother Hilde Ashanvil | 25% | +0 pts |
| Maren Saltsong | 25% | -0 pts |
| Old Thessaly | 24% | -1 pts |
| Quill the Inkwright | 24% | -2 pts |
| Gorran Bramblehorn | 21% | -4 pts |
| Dagna Ironvow | 19% | -6 pts |
| Wren Twofaces | 17% | -8 pts |
| Ysolde Nightglass | 17% | -8 pts |
| Brannoc Thickhide | 14% | -11 pts |
| Korvald the Ashen | 14% | -11 pts |
| Pip the Tinker Golem | 11% | -15 pts |

</details>

## Heroes across all stages

Average difference in win rate when a hero is in the team. Big positive numbers suggest a hero is too strong, big negative ones too weak. Heroes fight at their listed stats here, whatever their rarity: rarity's stat multiplier is a progression rule (`progression.json`), so this compares kits.

| Hero | Rarity | Faction | Type | Role | Difference |
|---|---|---|---|---|---|
| Oriel of the Last Light | Epic | Sun | Radiant | Support | +12 pts |
| Mordecai the Ferryman | Legendary | Grave | Umbral | Controller | +10 pts |
| Grandmother Hilde Ashanvil | Rare | Hearth | Stone | Tank | +6 pts |
| Kesh Swiftclaw | Common | Wild | Storm | Ranger | +5 pts |
| Jink the Gambler | Uncommon | Trickster | Venom | Ranger | +2 pts |
| Solenne of the Noon | Legendary | Sun | Radiant | Warrior | +2 pts |
| Seraphine of the Deep | Rare | Sea | Frost | Controller | +2 pts |
| Somna the Dreamwarden | Common | Night | Tide | Support | +1 pts |
| Dagna Ironvow | Epic | Forge | Stone | Tank | +1 pts |
| Big Grub Marrow | Rare | Hearth | Verdant | Support | +1 pts |
| Ysolde Nightglass | Rare | Night | Frost | Ranger | -0 pts |
| Maren Saltsong | Uncommon | Sea | Tide | Support | -0 pts |
| Wren Twofaces | Epic | Trickster | Umbral | Warrior | -1 pts |
| Brannoc Thickhide | Common | War | Metal | Tank | -3 pts |
| Old Thessaly | Epic | Arcana | Flame | Caster | -3 pts |
| Vesna Gravebloom | Uncommon | Grave | Venom | Warrior | -3 pts |
| Gorran Bramblehorn | Common | Wild | Verdant | Warrior | -4 pts |
| Pip the Tinker Golem | Uncommon | Forge | Metal | Controller | -7 pts |
| Quill the Inkwright | Uncommon | Arcana | Storm | Caster | -8 pts |
| Korvald the Ashen | Rare | War | Flame | Warrior | -8 pts |

## Rarities

For each rarity, the stages where one of its heroes lifts a team's win rate more than any other hero does (stages that every team wins don't count).

| Rarity | Heroes | Best answer on |
|---|---|---|
| Common | 4 | The Cold Foundry (Kesh Swiftclaw) |
| Uncommon | 5 | The Drowned Chapel (Jink the Gambler) |
| Rare | 5 | The Thornwood (Grandmother Hilde Ashanvil) |
| Epic | 4 | The Hollow Saint (Oriel of the Last Light) |
| Legendary | 2 | The Hollow Saint (Nightmare) (Mordecai the Ferryman) |

## Five copies of one hero

A team may field up to 5 copies of one hero (decided 2026-10-11). Copies share a faction and a type, so 5 of them always get the 4-hero faction doctrine and the 3-hero type bonus. Each hero's five-copy team fights 8 campaign battles per group (plain battles, stage bosses and the realm boss) 10 times each, at 70% of the strength the campaign expects there (`campaign.json` `heroScale`; at full strength nearly every team wins), against 100 random teams of 5 different heroes (2 fights per battle). A five-copy team is flagged when it beats 90% of the mixed teams.

| Team | Normal realm 1 | Nightmare realm 10 | Godless realm 10 |
|---|---|---|---|
| Mixed teams: average | 65% | 19% | 13% |
| Mixed teams: best 10% start at | 88% | 50% | 38% |
| 5 × Mordecai the Ferryman (Controller, Grave, Umbral) | **99%** | **96%** | **91%** |
| 5 × Kesh Swiftclaw (Ranger, Wild, Storm) | 85% | 43% | 30% |
| 5 × Seraphine of the Deep (Controller, Sea, Frost) | **90%** | 34% | 29% |
| 5 × Ysolde Nightglass (Ranger, Night, Frost) | 68% | 28% | 18% |
| 5 × Jink the Gambler (Ranger, Trickster, Venom) | 78% | 21% | 3% |
| 5 × Oriel of the Last Light (Support, Sun, Radiant) | 38% | 31% | 23% |
| 5 × Korvald the Ashen (Warrior, War, Flame) | 33% | 15% | 13% |
| 5 × Dagna Ironvow (Tank, Forge, Stone) | 38% | 3% | 9% |
| 5 × Gorran Bramblehorn (Warrior, Wild, Verdant) | 45% | 0% | 1% |
| 5 × Solenne of the Noon (Warrior, Sun, Radiant) | 40% | 0% | 1% |
| 5 × Wren Twofaces (Warrior, Trickster, Umbral) | 30% | 0% | 0% |
| 5 × Old Thessaly (Caster, Arcana, Flame) | 29% | 0% | 1% |
| 5 × Vesna Gravebloom (Warrior, Grave, Venom) | 23% | 6% | 0% |
| 5 × Somna the Dreamwarden (Support, Night, Tide) | 26% | 0% | 0% |
| 5 × Big Grub Marrow (Support, Hearth, Verdant) | 25% | 0% | 0% |
| 5 × Maren Saltsong (Support, Sea, Tide) | 25% | 0% | 0% |
| 5 × Quill the Inkwright (Caster, Arcana, Storm) | 15% | 0% | 0% |
| 5 × Pip the Tinker Golem (Controller, Forge, Metal) | 3% | 0% | 0% |
| 5 × Brannoc Thickhide (Tank, War, Metal) | 0% | 0% | 0% |
| 5 × Grandmother Hilde Ashanvil (Tank, Hearth, Stone) | 0% | 0% | 0% |

## Flags

- **The Cold Foundry:** Kesh Swiftclaw looks like a must-have (+25 pts). Check that other answers exist.
- **The Hollow Saint:** Oriel of the Last Light looks like a must-have (+27 pts). Check that other answers exist.
- **The Hollow Saint (Nightmare):** Mordecai the Ferryman looks like a must-have (+26 pts). Check that other answers exist.
- **Five copies:** 5 × Mordecai the Ferryman wins 99% on Normal realm 1, more than 90% of mixed teams (average 65%).
- **Five copies:** 5 × Mordecai the Ferryman wins 96% on Nightmare realm 10, more than 90% of mixed teams (average 19%).
- **Five copies:** 5 × Mordecai the Ferryman wins 91% on Godless realm 10, more than 90% of mixed teams (average 13%).
- **Five copies:** 5 × Seraphine of the Deep wins 90% on Normal realm 1, more than 90% of mixed teams (average 65%).
