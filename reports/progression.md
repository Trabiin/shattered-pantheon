# Progression Report

*Generated 2026-10-08 13:21 UTC by `dotnet run --project tools/ProgressionSim -c Release`. 9 player types × 6 players each, 365 days. Every campaign battle is a real fight on the battle engine (276,669 fights); rewards, upgrades, summons and star challenges follow `progression.json`. Roster: the 20 test kits as 50 heroes (Rare, Epic and Legendary versions). Numbers are medians across the players of each type.*

## When each player type gets there

| Player | Normal 10 | Normal 20 | Normal 40 | Hard 20 | Hard 40 | Nightmare 40 | Godless 40 | Where on day 365 |
|---|---|---|---|---|---|---|---|---|
| Casual Free | 11 | 24 | 42 | 54 | 94 | 321 | – | Godless 9-4 |
| Casual Light | 9 | 20 | 30 | 42 | 81 | 249 | – | Godless 16-10 |
| Casual Heavy | 7 | 13 | 25 | 36 | 50 | 188 | – | Godless 21-3 |
| Regular Free | 5 | 13 | 23 | 34 | 57 | 142 | – | all done |
| Regular Light | 5 | 11 | 16 | 23 | 44 | 123 | – | all done |
| Regular Heavy | 3 | 6 | 11 | 15 | 25 | 112 | – | all done |
| Dedicated Free | 3 | 9 | 18 | 24 | 35 | 105 | – | all done |
| Dedicated Light | 3 | 8 | 12 | 17 | 37 | 85 | 305 | all done |
| Dedicated Heavy | 2 | 4 | 7 | 10 | 18 | 70 | 108 | all done |

Targets (doc 10 section 9): Normal 40 by day 40 (Casual), 21 (Regular), 12 (Dedicated); Hard 40 by day 120 (Casual), 75 (Regular), 50 (Dedicated). A dash means most players of that type hadn't reached it by day 365.

## Reward rhythm

| Player | First Legendary | Longest gap between new heroes | Longest gap without a big moment | Longest wall (Normal, Hard) | Walls over 3 days | Longest wall (Nightmare, Godless) | Godshards per day (first 30 days) |
|---|---|---|---|---|---|---|---|
| Casual Free | day 2 | 80 days | 2 days | 13 days | 3 | 57 days | 851 |
| Casual Light | day 2 | 71 days | 2 days | 13 days | 2 | 66 days | 1048 |
| Casual Heavy | day 2 | 45 days | 2 days | 1 days | 0 | 75 days | 1658 |
| Regular Free | day 1 | 69 days | 2 days | 6 days | 2 | 110 days | 1307 |
| Regular Light | day 1 | 59 days | 2 days | 6 days | 1 | 85 days | 1479 |
| Regular Heavy | day 1 | 31 days | 2 days | 1 days | 0 | 114 days | 2080 |
| Dedicated Free | day 1 | 82 days | 2 days | 3 days | 0 | 57 days | 1897 |
| Dedicated Light | day 1 | 50 days | 2 days | 4 days | 1 | 77 days | 2067 |
| Dedicated Heavy | day 1 | 28 days | 2 days | 2 days | 0 | 24 days | 2653 |

A **big moment** is a new Epic or Legendary hero, a stage chest, a star chest, an ascension or a large Codex reward. A **wall** is a stretch of days with no new campaign battle cleared.

Free players' walls by difficulty and stage band: Hard 31-40 (13), Hard 21-30 (8), Hard 1-10 (4), Hard 11-20 (2), Normal 11-20 (2), Normal 31-40 (1).

## Energy (Devotion)

| Player | Days out of energy with time left (while campaign remains) | First day out | Farm fights per day | Campaign fights per day (first 30 days) |
|---|---|---|---|---|
| Casual Free | 0% | not reached | 3.5 | 19.2 |
| Casual Light | 0% | not reached | 3.2 | 19.9 |
| Casual Heavy | 0% | not reached | 3.0 | 21.7 |
| Regular Free | 0% | not reached | 45.4 | 34.6 |
| Regular Light | 0% | not reached | 47.7 | 36.9 |
| Regular Heavy | 0% | not reached | 47.6 | 41.4 |
| Dedicated Free | 76% | day 65 | 55.5 | 49.5 |
| Dedicated Light | 72% | day 69 | 55.5 | 50.6 |
| Dedicated Heavy | 0% | not reached | 85.5 | 53.7 |

Campaign fights never cost energy (doc 10 section 4). Energy runs out only when a player has time left after the campaign and the daily round and wants to keep farming.

## One regular free player, day by day

| Day | Next battle | Team level | Star rank | Gear tier | Skill level | Team power vs recommended | Heroes | Stars earned | Godshards earned that day |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Normal 4-5 | 11 | 4.4 | 1.0 | 1.8 | 107% | 18 | 64 | 2736 |
| 2 | Normal 8-8 | 20 | 4.4 | 2.0 | 2.4 | 105% | 19 | 107 | 1356 |
| 3 | Normal 12-10 | 30 | 4.4 | 2.0 | 3.0 | 102% | 22 | 149 | 1406 |
| 5 | Normal 18-8 | 44 | 4.4 | 3.0 | 3.6 | 104% | 28 | 243 | 2011 |
| 7 | Normal 26-10 | 61 | 4.4 | 4.0 | 4.2 | 102% | 35 | 333 | 4336 |
| 10 | Normal 39-3 | 85 | 4.4 | 6.0 | 5.0 | 102% | 38 | 460 | 1436 |
| 14 | Hard 12-8 | 99 | 4.4 | 7.0 | 5.4 | 98% | 39 | 668 | 1251 |
| 21 | Hard 23-10 | 108 | 4.4 | 8.0 | 5.6 | 95% | 40 | 984 | 1056 |
| 30 | Hard 33-4 | 118 | 5.0 | 8.6 | 5.8 | 97% | 45 | 1440 | 821 |
| 45 | Nightmare 6-5 | 128 | 5.0 | 9.0 | 6.0 | 93% | 47 | 2177 | 731 |
| 60 | Nightmare 9-10 | 131 | 5.0 | 9.2 | 6.2 | 93% | 47 | 2960 | 646 |
| 90 | Nightmare 14-2 | 141 | 5.0 | 9.8 | 6.2 | 97% | 48 | 3769 | 216 |
| 120 | Nightmare 23-10 | 151 | 5.0 | 10.4 | 6.4 | 98% | 50 | 4178 | 201 |
| 150 | Nightmare 38-10 | 163 | 5.0 | 11.0 | 6.4 | 95% | 50 | 4701 | 396 |
| 180 | Godless 15-10 | 171 | 6.0 | 11.4 | 6.6 | 99% | 50 | 5551 | 761 |
| 210 | Godless 20-4 | 178 | 6.0 | 11.8 | 6.6 | 101% | 50 | 5875 | 611 |
| 240 | Godless 33-10 | 185 | 6.0 | 12.0 | 6.8 | 98% | 50 | 6280 | 201 |
| 300 | all done | 200 | 6.0 | 12.0 | 6.8 | 126% | 50 | 6759 | 296 |
| 365 | all done | 200 | 6.0 | 12.0 | 7.0 | 126% | 50 | 6781 | 296 |

## One casual free player, day by day

| Day | Next battle | Team level | Star rank | Gear tier | Skill level | Team power vs recommended | Heroes | Stars earned | Godshards earned that day |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Normal 3-7 | 7 | 4.0 | 1.0 | 1.6 | 96% | 14 | 26 | 1418 |
| 2 | Normal 4-10 | 11 | 4.4 | 1.0 | 2.0 | 105% | 16 | 39 | 798 |
| 3 | Normal 4-10 | 13 | 4.4 | 1.0 | 2.0 | 111% | 19 | 52 | 883 |
| 5 | Normal 6-10 | 19 | 4.4 | 1.0 | 2.4 | 110% | 22 | 79 | 523 |
| 7 | Normal 10-5 | 27 | 4.4 | 2.0 | 3.0 | 110% | 25 | 114 | 1568 |
| 10 | Normal 14-1 | 36 | 4.4 | 2.0 | 3.4 | 109% | 31 | 163 | 2998 |
| 14 | Normal 19-10 | 50 | 4.4 | 3.0 | 4.0 | 109% | 31 | 233 | 773 |
| 21 | Normal 31-9 | 75 | 4.4 | 5.0 | 5.0 | 108% | 36 | 370 | 1598 |
| 30 | Hard 2-10 | 102 | 4.4 | 7.0 | 5.8 | 115% | 39 | 526 | 848 |
| 45 | Hard 18-8 | 113 | 5.0 | 8.0 | 6.2 | 111% | 41 | 814 | 798 |
| 60 | Hard 30-10 | 123 | 5.0 | 9.0 | 6.4 | 106% | 41 | 1080 | 343 |
| 90 | Nightmare 4-5 | 137 | 5.0 | 9.6 | 6.8 | 103% | 45 | 1617 | 428 |
| 120 | Nightmare 4-5 | 143 | 5.0 | 9.8 | 7.0 | 108% | 46 | 2233 | 348 |
| 150 | Nightmare 9-10 | 146 | 5.0 | 10.0 | 7.0 | 106% | 46 | 2797 | 343 |
| 180 | Nightmare 12-10 | 150 | 5.0 | 10.0 | 7.0 | 105% | 47 | 3344 | 338 |
| 210 | Nightmare 15-10 | 153 | 5.0 | 10.0 | 7.2 | 105% | 50 | 3892 | 743 |
| 240 | Nightmare 17-10 | 156 | 5.0 | 10.4 | 7.2 | 107% | 50 | 4294 | 338 |
| 300 | Nightmare 32-2 | 165 | 5.0 | 10.8 | 7.4 | 102% | 50 | 4732 | 128 |
| 365 | Godless 9-4 | 174 | 6.0 | 11.2 | 7.6 | 105% | 50 | 5628 | 408 |

## Flags

- **Dedicated Free** finishes Normal on day 18; the target is about day 12.
- **Dedicated Free** finishes Hard on day 35, much sooner than the target of about day 50.
- **Casual Free:** stuck on one battle for up to 13 days; the limit is 3.
- **Casual Light:** stuck on one battle for up to 13 days; the limit is 3.
- **Regular Free:** stuck on one battle for up to 6 days; the limit is 3.
- **Regular Light:** stuck on one battle for up to 6 days; the limit is 3.
- **Dedicated Light:** stuck on one battle for up to 4 days; the limit is 3.
