# Shattered Pantheon

A mobile auto-battle gacha game, built in Unity. Design docs live in the project's shared folder (`plan/`).

## What's here

| Folder | What it is |
|---|---|
| `Unity/` | The game. Open this folder in Unity Hub. |
| `Unity/Assets/BattleCore/` | The battle rules: plain C# with no Unity code, shared by the game and the simulator. |
| `Unity/Assets/Resources/BattleData/` | Heroes, enemies, stages and rules as JSON. The one copy both the game and the simulators read. |
| `Unity/Assets/Game/` | The game screens. Right now: the battle screen with placeholder cards. |
| `tools/BattleSim/` | The balance simulator, compiled from the same `BattleCore` files. |
| `sim-js/` | The original JavaScript simulator, kept until the C# one fully replaces it. |
| `reports/latest.md` | The latest balance report. |

## Open the game

1. Install **Unity Hub**, then **Unity 6.3 LTS** with *Android Build Support* (and *iOS Build Support* on a Mac).
2. In Unity Hub: **Add > Add project from disk**, pick the `Unity` folder, and open it with Unity 6.3 LTS. The first open takes a few minutes while Unity imports everything.
3. The battle scene is created automatically (`Assets/Scenes/Battle.unity`). Open it and press **Play**. If it's missing, use the menu **Shattered Pantheon > Rebuild Battle Scene**.
4. In the Game view, pick a portrait phone resolution (for example 1080x1920) to see it as on a phone.

The screen plays a fight on its own: tap the speed button for 1x/2x/3x, **Stage** to switch stage, **Restart** for a new fight.

## Run the simulator

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download). From the repository root:

```
dotnet run --project tools/BattleSim                                    # full report -> reports/latest.md
dotnet run --project tools/BattleSim -- --team hilde,solenne,thessaly,maren,pip --stage saint --runs 500
dotnet run --project tools/BattleSim -- --log hilde,solenne,thessaly,maren,pip --stage saint --seed 1
```

## Keeping the engines in step

Until the JavaScript simulator is retired, every rules change goes into both `BattleCore` and `sim-js/engine.js`, and this must print nothing:

```
node sim-js/parity.js 2000 > js.txt
dotnet run --project tools/BattleSim -c Release -- --parity 2000 > cs.txt
cmp js.txt cs.txt
```
