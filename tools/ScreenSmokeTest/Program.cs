// Plays the battle screen on fake Unity (FakeUnity.cs): every stage to the end, a restart in
// the middle of a fight, the speed button, and the results screen. Fails on any exception the
// screen throws, on a fight that differs from the engine run directly, or on leaked objects.
//   dotnet run --project tools/ScreenSmokeTest
using System;
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

    static int Main()
    {
        string root = FindRoot();
        UnityEngine.Resources.Root = Path.Combine(root, "Unity", "Assets", "Resources");
        var data = GameData.LoadDirectory(Path.Combine(UnityEngine.Resources.Root, "BattleData"));

        var screen = new GameObject("Battle Screen").AddComponent<BattleScreen>();
        Call(screen, "Start");
        Expect(Scheduler.Errors.Count == 0, "screen starts without errors");

        // Restart part-way through a fight, while popups and card animations are running.
        foreach (float wait in new[] { 0.65f, 1.1f, 1.62f, 2.05f, 3.3f, 4.31f })
        {
            RunFor(wait);
            Click("Restart");
        }
        Expect(Scheduler.Errors.Count == 0, "restarting mid-fight throws nothing");

        Click("1x"); Click("2x"); // to 3x
        Expect(Label("1x Button") == "3x", "speed button cycles to 3x");

        for (int s = 0; s < data.Stages.Count; s++)
        {
            var stage = data.Stages[s];
            float t = RunUntil(() => Active("Result"), 3600);
            int seed = (int)Field(screen, "seed");
            var direct = Battle.Run(data, Formation.AutoPlace(data, new[] { "hilde", "solenne", "thessaly", "maren", "pip" }), stage.Id, seed);
            var shown = (Battle)Field(screen, "battle");
            string title = ((Text)Field(screen, "resultTitle")).text;
            Console.WriteLine($"{stage.Name}: \"{title}\" after {shown.Actions} actions ({t:0}s simulated at 3x)");
            Expect(Active("Result"), $"{stage.Name}: results screen appears");
            Expect(shown.Result == direct.Result && shown.Actions == direct.Actions, $"{stage.Name}: fight on screen matches the engine run directly ({direct.Result}, {direct.Actions} actions)");
            Expect(title == (direct.Result == "win" ? "Victory" : direct.Result == "lose" ? "Defeat" : "Time's up"), $"{stage.Name}: result title is right");
            Expect(((Text)Field(screen, "resultBody")).text.Contains("Fight length"), $"{stage.Name}: result details filled in");
            RunFor(3);
            int popups = LiveTexts() - BaselineTexts(shown);
            Expect(popups == 0, $"{stage.Name}: damage numbers are cleaned up ({popups} left)");
            Expect(Scheduler.Running == 0, $"{stage.Name}: no animations left running ({Scheduler.Running})");
            Click(s == data.Stages.Count - 1 ? "Fight again" : "Next stage");
            RunFor(0.5f);
            Expect(!Active("Result"), $"{stage.Name}: results screen closes for the next fight");
        }

        foreach (var e in Scheduler.Errors.Distinct().Take(5)) Console.WriteLine("ERROR " + e);
        Expect(Scheduler.Errors.Count == 0, "no errors during any fight");
        Console.WriteLine(failures == 0 ? "All screen checks passed." : $"{failures} screen check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static void Expect(bool ok, string what) { if (!ok) failures++; Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); }

    static void RunFor(float seconds) { for (float t = 0; t < seconds; t += 1 / 60f) Scheduler.Frame(1 / 60f); }
    static float RunUntil(Func<bool> done, float limit) { float t = 0; while (!done() && t < limit) { Scheduler.Frame(1 / 60f); t += 1 / 60f; } return t; }

    static GameObject Find(string name) => UnityEngine.Object.All.OfType<GameObject>().LastOrDefault(g => g != null && g.name == name);
    static bool Active(string name) { var g = Find(name); return g != null && g.activeSelf; }
    static string Label(string buttonName) => Find(buttonName).transform.Cast<Transform>().Select(t => t.GetComponent<Text>()).First(t => t != null).text;
    static void Click(string label)
    {
        var go = Find(label + " Button") ?? UnityEngine.Object.All.OfType<GameObject>().LastOrDefault(g => g != null && g.name.EndsWith(" Button") && Label(g.name) == label);
        if (go == null) { Expect(false, $"button \"{label}\" exists"); return; }
        go.GetComponent<Button>().onClick.Invoke();
    }

    // Text objects that should exist when no popup is showing: everything except popups, which are direct children of the battlefield.
    static int LiveTexts() => Find("Battlefield").transform.Cast<Transform>().Count(t => t.GetComponent<Text>() != null);
    static int BaselineTexts(Battle b) => 2; // stage label and log line

    static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "Unity", "Assets"))) return d.FullName;
        throw new DirectoryNotFoundException("Run from inside the repository");
    }
}
