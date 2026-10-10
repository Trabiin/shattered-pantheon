// Plays the game's screens on fake Unity (FakeUnity.cs) the way a player would: the campaign screen,
// locked battles, team and formation editing, every realm 1 battle in order (each win opening the
// next), retreating mid-fight, results, retry, closing and reopening the game with its save, and
// resetting progress.
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
    const int MaxTries = 5;
    static readonly string[] Starter = { "hilde", "solenne", "thessaly", "maren", "pip" };
    static int failures;
    static GameApp app;

    static int Main()
    {
        string root = FindRoot();
        UnityEngine.Resources.Root = Path.Combine(root, "Unity", "Assets", "Resources");
        string dir = Path.Combine(UnityEngine.Resources.Root, "BattleData");
        string R(string f) => File.ReadAllText(Path.Combine(dir, f + ".json"));
        var data = GameData.FromJson(R("rules"), R("heroes"), R("enemies"), R("campaign"));
        Application.persistentDataPath = Path.Combine(Path.GetTempPath(), "sp-smoke-" + Guid.NewGuid().ToString("N"));
        string saveFile = Path.Combine(Application.persistentDataPath, SaveStore.FileName);
        var realm = CampaignView.Battles(data);
        Expect(realm.Count == 40 && realm.GroupBy(b => b.StageNumber).Count() == 10 && realm.GroupBy(b => b.StageNumber).All(g => g.Count() == 4), "realm 1 on Normal has 10 stages of 4 battles");
        Expect(realm.All(b => (b.BattleNumber == 4) == (b.Boss != null)) && realm.Count(b => b.Boss == "realm") == 1 && realm.Last().Boss == "realm", "battle 4 of each stage is its boss, and stage 10's is the realm boss");

        app = new GameObject("Game").AddComponent<GameApp>();
        Call(app, "Start");
        Expect(Current is CampaignView, "game opens on the campaign");
        Expect(realm.All(b => Find("Battle: " + b.Id) != null), "every realm 1 battle is listed");
        Expect(Named("Battle: ").Count() == realm.Count, $"only realm 1 battles are listed ({Named("Battle: ").Count()})");
        var testStages = GameData.LoadDirectory(dir).Stages;
        Expect(testStages.All(s => Named(": " + s.Id).Count() == 0 && Named(s.Name).Count() == 0), "the balance simulator's test stages are not shown");

        // A new game opens only the first battle; locked battles can't be started.
        Expect(!IsShownLocked(realm[0]) && realm.Skip(1).All(IsShownLocked), "a new game shows only the first battle open, the rest locked");
        Click("Battle: " + realm[1].Id);
        Expect(Current is CampaignView, "tapping a locked battle doesn't open it");
        app.ShowBattle(realm[1], Formation.AutoPlace(data, Starter), "2-3");
        Expect(Current is CampaignView, "a locked battle can't be started even if a screen asks");

        // Team editing on the first battle.
        var first = realm[0];
        Click("Battle: " + first.Id);
        Expect(Current is TeamView, "tapping a battle opens the team screen");
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
        Click("Formation Button");
        Expect(((TeamView)Current).Formation == "3-2" && Team().Count == 5 && Team().Count(t => t.Row == "front") == 3, "the formation button switches to 3-2 and keeps all five heroes");
        Click("Formation Button");
        Expect(((TeamView)Current).Formation == "2-3" && TeamSet(Team()) == TeamSet(picked), "switching back to 2-3 restores the same placement");
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
        for (int tries = 1; !app.Progress.IsCleared(first.Id) && tries < MaxTries; tries++) { Click("Retry Button"); CheckFight(data, first, picked); }
        Expect(app.Progress.IsCleared(first.Id), $"{first.Name}: won (retrying up to {MaxTries} times) before going on");

        // Realm 1 from start to finish with the starter team: every battle in order from the campaign
        // screen, retrying a lost battle like a player would.
        Click("Stages Button");
        Expect(Current is CampaignView, "Stages returns to the campaign");
        Expect(!IsShownLocked(realm[1]) && realm.Skip(2).All(IsShownLocked), "winning the first battle opens the second, and only that");
        var last = picked;
        foreach (var stage in realm.Skip(1))
        {
            var next = realm.ElementAtOrDefault(realm.IndexOf(stage) + 1);
            string opens = stage.Boss == "stage" ? "the next stage's first battle" : "the next battle";
            Expect(!IsShownLocked(stage) && (next == null || IsShownLocked(next)), $"{stage.Name}: open, and {opens} locked until it's won");
            Click("Battle: " + stage.Id);
            Expect(TeamSet(Team()) == TeamSet(last), $"{stage.Name}: opens with the last team used");
            Click("Clear Button");
            foreach (var id in Starter) Click("Hero: " + id);
            Click("Auto Button");
            var team = last = Team();
            Expect(team.Select(t => t.Id).OrderBy(x => x).SequenceEqual(Starter.OrderBy(x => x)), $"{stage.Name}: the starter team is picked");
            Click("Fight! Button");
            int tries = 1;
            while (!CheckFight(data, stage, team) && tries < MaxTries) { Click("Retry Button"); tries++; }
            Expect(app.Progress.IsCleared(stage.Id), $"{stage.Name}: won within {MaxTries} tries ({tries})");
            Click("Stages Button");
            if (next != null) Expect(!IsShownLocked(next), $"{stage.Name}: the win opens {opens}");
        }
        Expect(realm.All(b => IsShownCleared(b)), "every won battle shows as cleared");
        Expect(realm.All(b => !IsShownLocked(b)), "every won battle stays open to replay");

        // A fight in the 3-2 formation, on the realm boss.
        var wide = realm.Last();
        Click("Battle: " + wide.Id);
        Click("Formation Button");
        var three = Team();
        Expect(three.Count(t => t.Row == "front") == 3, "3-2 puts three heroes in front");
        Click("Fight! Button");
        Expect(((BattleView)Current).Battle.HeroFormation == "3-2", "the fight uses the 3-2 formation");
        CheckFight(data, wide, three);
        Click("Change team Button");
        Expect(((TeamView)Current).Formation == "3-2", "the team screen remembers the 3-2 formation");
        Click("Stages Button".Replace("Stages", "Back"));

        // The first battle remembers its own team.
        Click("Battle: " + first.Id);
        Expect(Team().Select(t => t.Id + "@" + t.Slot).SequenceEqual(picked.Select(t => t.Id + "@" + t.Slot)), "a battle reopens with the team last used on it");
        Click("Back Button");
        Expect(Current is CampaignView, "Back returns to the campaign");

        // Close the game (the phone pauses it first) and open it again: everything comes back from the save.
        app.GetType().GetMethod("OnApplicationPause", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { true });
        string saved = File.ReadAllText(saveFile);
        UnityEngine.Object.Destroy(app.gameObject);
        app = new GameObject("Game").AddComponent<GameApp>();
        Call(app, "Start");
        Expect(Current is CampaignView, "the reopened game starts on the campaign");
        Expect(realm.All(b => IsShownCleared(b)), "after reopening, every won battle still shows as cleared");
        Expect(realm.All(b => !IsShownLocked(b)), "after reopening, every won battle is still open");
        Expect(realm.All(b => app.Progress.Stars(b.Id).SequenceEqual(new[] { 1 })), "after reopening, every won battle has its 1 star");
        Expect(app.Progress.Furthest("normal") == realm.Last().Id, "after reopening, the furthest battle reached is the realm boss");
        Click("Battle: " + first.Id);
        Expect(Team().Select(t => t.Id + "@" + t.Slot).SequenceEqual(picked.Select(t => t.Id + "@" + t.Slot)), "after reopening, a battle still opens with the team last used on it");
        Click("Back Button");
        Expect(File.ReadAllText(saveFile) == saved, "reopening the game doesn't change the save");
        Expect(Debug.Warnings.Count == 0, "no save warnings");

        // Reset progress (testing only) takes two taps and starts a new game.
        Click("Reset progress Button");
        Expect(IsShownCleared(first) && File.Exists(saveFile), "one tap on Reset progress only asks to confirm");
        Click("Reset progress Button");
        Expect(Current is CampaignView && realm.All(b => !IsShownCleared(b)) && !File.Exists(saveFile), "the second tap clears all progress and the save");
        Expect(app.Progress.Furthest("normal") == null, "after a reset nothing has been reached");
        Expect(!IsShownLocked(realm[0]) && realm.Skip(1).All(IsShownLocked), "after a reset only the first battle is open");
        Directory.Delete(Application.persistentDataPath, true);

        foreach (var e in Scheduler.Errors.Distinct().Take(5)) Console.WriteLine("ERROR " + e);
        Expect(Scheduler.Errors.Count == 0, "no errors on any screen");
        Console.WriteLine(failures == 0 ? "All screen checks passed." : $"{failures} screen check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    // Plays the open fight to the end and checks it against the engine; true if it was won.
    static bool CheckFight(GameData data, StageDef stage, List<TeamSlot> team)
    {
        var view = (BattleView)Current;
        float t = RunUntil(() => Current is ResultsView, 3600);
        // Until heroes have levels, they fight at the strength the battle was sim-checked for.
        var strong = team.Select(s => new TeamSlot(s.Id, s.Row, s.Slot) { HpScale = stage.HeroScale, AtkScale = stage.HeroScale }).ToList();
        var direct = Battle.Run(data, strong, stage.Id, view.Seed, new BattleOptions { Formation = view.Battle.HeroFormation });
        var shown = view.Battle;
        var results = Current as ResultsView;
        Console.WriteLine($"{stage.Name}: {results?.Title ?? "no results"} after {shown.Actions} actions ({t:0}s at {GameApp.Speeds[app.SpeedIndex]}x)");
        Expect(results != null, $"{stage.Name}: results screen opens when the fight ends");
        Expect(shown.Result == direct.Result && shown.Actions == direct.Actions, $"{stage.Name}: fight on screen matches the engine run directly ({direct.Result}, {direct.Actions} actions)");
        Expect(TeamSet(shown.Units.Where(u => u.Side == "A").Select(u => new TeamSlot(u.Id, u.Row, u.Slot)).ToList()) == TeamSet(team), $"{stage.Name}: the fight used the picked team and formation");
        Expect(results != null && results.Title == (direct.Result == "win" ? "Victory" : direct.Result == "lose" ? "Defeat" : "Time's up"), $"{stage.Name}: result title is right");
        Expect(((List<TeamSlot>)Field(view, "team")).All(s => s.HpScale == stage.HeroScale && s.AtkScale == stage.HeroScale), $"{stage.Name}: heroes fight at the battle's expected strength ({stage.HeroScale}x)");
        Expect(direct.Result != "win" || app.Progress.IsCleared(stage.Id), $"{stage.Name}: a win marks the battle cleared");
        Expect(SaveData.FromJson(File.ReadAllText(Path.Combine(Application.persistentDataPath, SaveStore.FileName))).Stars.ContainsKey(stage.Id) == app.Progress.IsCleared(stage.Id), $"{stage.Name}: the save on the device matches after the fight");
        Expect(direct.Heroes.All(h => Find("Row: " + h.Id) != null), $"{stage.Name}: every hero has a results row");
        Expect(Scheduler.Running == 0, $"{stage.Name}: no animations left running ({Scheduler.Running})");
        Expect(CanvasChildren() == 2, $"{stage.Name}: old screens are cleaned up ({CanvasChildren()} canvas children)");
        return direct.Result == "win";
    }

    static void Expect(bool ok, string what) { if (!ok) failures++; Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); }

    static object Current => Field(app, "current");
    static List<TeamSlot> Team() => ((TeamView)Current).Team();
    static bool Has(string id, string slot) => Team().Any(t => t.Id == id && t.Slot == slot);
    static string TeamSet(List<TeamSlot> team) => string.Join(",", team.Select(t => t.Id + "@" + t.Row + "/" + t.Slot).OrderBy(x => x));

    static bool IsShownCleared(StageDef b) => TileSays(b, "Cleared");
    static bool IsShownLocked(StageDef b) => TileSays(b, "Locked");
    static bool TileSays(StageDef b, string text) => Find("Battle: " + b.Id).transform.Cast<Transform>().Any(t => t.GetComponent<Text>()?.text.Contains(text) == true);

    static void RunFor(float seconds) { for (float t = 0; t < seconds; t += 1 / 60f) Scheduler.Frame(1 / 60f); }
    static float RunUntil(Func<bool> done, float limit) { float t = 0; while (!done() && t < limit) { Scheduler.Frame(1 / 60f); t += 1 / 60f; } return t; }

    static IEnumerable<GameObject> Named(string part) => UnityEngine.Object.All.OfType<GameObject>().Where(g => g != null && g.name.Contains(part));
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
