// Plays the game's screens on fake Unity (FakeUnity.cs) the way a player would: stage select,
// team and formation editing, fights on every stage, retreating mid-fight, results, retry.
// Fails on any exception a screen throws, on a fight that differs from the engine run directly,
// on screens or animations left behind, or on a team that isn't what the player picked.
//   dotnet run --project tools/ScreenSmokeTest
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredPantheon.Battle;
using ShatteredPantheon.Game;
using UnityEngine;
using UnityEngine.UI;

static class Program
{
    static int failures;
    static GameApp app;

    static int Main()
    {
        string root = FindRoot();
        UnityEngine.Resources.Root = Path.Combine(root, "Unity", "Assets", "Resources");
        var data = GameData.LoadDirectory(Path.Combine(UnityEngine.Resources.Root, "BattleData"));

        app = new GameObject("Game").AddComponent<GameApp>();
        Call(app, "Start");
        Expect(Current is StageSelectView, "game opens on stage select");
        Expect(data.Stages.All(s => Find("Stage: " + s.Id) != null), "every stage is listed");

        // Team editing on the first stage.
        var first = data.Stages[0];
        Click("Stage: " + first.Id);
        Expect(Current is TeamView, "tapping a stage opens the team screen");
        Expect(Team().Count == 5, "a default team of 5 is ready");
        Click("Clear Button");
        Expect(Team().Count == 0, "Clear empties the team");
        Click("Fight! Button");
        Expect(Current is TeamView, "can't start a fight with no heroes");
        Click("Auto Button");
        Expect(Team().Count == 5, "Auto fills the default team");
        Click("Slot: back2"); Click("Slot: back2");
        Expect(Team().Count == 4 && Team().All(t => t.Slot != "back2"), "tapping a selected slot empties it");
        Click("Hero: ysolde");
        Expect(Has("ysolde", "back2"), "tapping a hero puts them in the selected slot");
        string frontHero = Team().First(t => t.Slot == "front0").Id;
        Click("Hero: " + frontHero);
        Expect(Has(frontHero, "back2") && Has("ysolde", "front0"), "tapping a hero already in the team swaps them into the selected slot");
        var picked = Team();
        Expect(Scheduler.Errors.Count == 0, "team editing throws nothing");

        // Fight with that team, then check results.
        Click("Fight! Button");
        Expect(Current is BattleView, "Fight! opens the battle");
        CheckFight(data, first, picked);

        // Retry, and retreat part-way through fights while popups and animations are running.
        Click("Retry Button");
        foreach (float wait in new[] { 0.65f, 1.1f, 1.62f, 2.05f, 3.3f, 4.31f })
        {
            Expect(Current is BattleView, "battle running before retreat");
            RunFor(wait);
            Click("Retreat Button");
            Expect(Current is TeamView, $"Retreat after {wait}s returns to the team screen");
            Expect(TeamSet(Team()) == TeamSet(picked), "the team screen keeps the player's team after retreating");
            Click("Fight! Button");
        }
        Expect(Scheduler.Errors.Count == 0, "retreating mid-fight throws nothing");
        Click("Speed Button"); Click("Speed Button");
        Expect(Label(Find("Speed Button")) == "3x", "speed button cycles to 3x");
        CheckFight(data, first, picked);

        // Every stage with its default team, via the stage list.
        Click("Stages Button");
        Expect(Current is StageSelectView, "Stages returns to stage select");
        foreach (var stage in data.Stages.Skip(1))
        {
            Click("Stage: " + stage.Id);
            Expect(TeamSet(Team()) == TeamSet(picked), $"{stage.Name}: opens with the last team used");
            Click("Clear Button"); Click("Auto Button");
            var team = Team();
            Click("Fight! Button");
            CheckFight(data, stage, team);
            Click("Stages Button");
        }

        // The first stage remembers its own team; cleared stages are marked.
        Click("Stage: " + first.Id);
        Expect(Team().Select(t => t.Id + "@" + t.Slot).SequenceEqual(picked.Select(t => t.Id + "@" + t.Slot)), "a stage reopens with the team last used on it");
        Click("Back Button");
        Expect(Current is StageSelectView, "Back returns to stage select");

        foreach (var e in Scheduler.Errors.Distinct().Take(5)) Console.WriteLine("ERROR " + e);
        Expect(Scheduler.Errors.Count == 0, "no errors on any screen");
        Console.WriteLine(failures == 0 ? "All screen checks passed." : $"{failures} screen check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static void CheckFight(GameData data, StageDef stage, List<TeamSlot> team)
    {
        var view = (BattleView)Current;
        float t = RunUntil(() => Current is ResultsView, 3600);
        var direct = Battle.Run(data, team, stage.Id, view.Seed);
        var shown = view.Battle;
        var results = Current as ResultsView;
        Console.WriteLine($"{stage.Name}: {results?.Title ?? "no results"} after {shown.Actions} actions ({t:0}s at {GameApp.Speeds[app.SpeedIndex]}x)");
        Expect(results != null, $"{stage.Name}: results screen opens when the fight ends");
        Expect(shown.Result == direct.Result && shown.Actions == direct.Actions, $"{stage.Name}: fight on screen matches the engine run directly ({direct.Result}, {direct.Actions} actions)");
        Expect(TeamSet(shown.Units.Where(u => u.Side == "A").Select(u => new TeamSlot(u.Id, u.Row, u.Slot)).ToList()) == TeamSet(team), $"{stage.Name}: the fight used the picked team and formation");
        Expect(results != null && results.Title == (direct.Result == "win" ? "Victory" : direct.Result == "lose" ? "Defeat" : "Time's up"), $"{stage.Name}: result title is right");
        Expect(direct.Result != "win" || PlayerPrefs.GetInt("cleared." + stage.Id) == 1, $"{stage.Name}: a win marks the stage cleared");
        Expect(direct.Heroes.All(h => Find("Row: " + h.Id) != null), $"{stage.Name}: every hero has a results row");
        Expect(Scheduler.Running == 0, $"{stage.Name}: no animations left running ({Scheduler.Running})");
        Expect(CanvasChildren() == 2, $"{stage.Name}: old screens are cleaned up ({CanvasChildren()} canvas children)");
    }

    static void Expect(bool ok, string what) { if (!ok) failures++; Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); }

    static object Current => Field(app, "current");
    static List<TeamSlot> Team() => ((TeamView)Current).Team();
    static bool Has(string id, string slot) => Team().Any(t => t.Id == id && t.Slot == slot);
    static string TeamSet(List<TeamSlot> team) => string.Join(",", team.Select(t => t.Id + "@" + t.Row + "/" + t.Slot).OrderBy(x => x));

    static void RunFor(float seconds) { for (float t = 0; t < seconds; t += 1 / 60f) Scheduler.Frame(1 / 60f); }
    static float RunUntil(Func<bool> done, float limit) { float t = 0; while (!done() && t < limit) { Scheduler.Frame(1 / 60f); t += 1 / 60f; } return t; }

    static GameObject Find(string name) => UnityEngine.Object.All.OfType<GameObject>().LastOrDefault(g => g != null && g.name == name);
    static string Label(GameObject go) => go.transform.Cast<Transform>().Select(t => t.GetComponent<Text>()).First(t => t != null).text;
    static void Click(string name)
    {
        var go = Find(name);
        if (go == null) { Expect(false, $"\"{name}\" exists to tap"); return; }
        go.GetComponent<Button>().onClick.Invoke();
    }
    static int CanvasChildren() => Find("Canvas").transform.Cast<Transform>().Count();

    static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "Unity", "Assets"))) return d.FullName;
        throw new DirectoryNotFoundException("Run from inside the repository");
    }
}
