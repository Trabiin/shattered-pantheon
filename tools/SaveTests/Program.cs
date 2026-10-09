// Tests for the game's save file: a new game, saving and loading, closing and reopening, damaged and
// unreadable saves, upgrading an older version, and keeping sections a later system adds.
// Each test works in its own temporary folder, standing in for the phone's save folder.
//   dotnet run --project tools/SaveTests
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShatteredPantheon.Battle;
using ShatteredPantheon.Game;

static class Program
{
    static int failures;
    static GameData data;
    static List<StageDef> realm;

    static int Main()
    {
        string dir = Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData");
        string R(string f) => File.ReadAllText(Path.Combine(dir, f + ".json"));
        data = GameData.FromJson(R("rules"), R("heroes"), R("enemies"), R("campaign"));
        realm = data.Stages.Where(s => s.RealmNumber == 1 && s.Difficulty == "normal").OrderBy(s => s.StageNumber).ThenBy(s => s.BattleNumber).ToList();

        Test("A missing save is a new game", NewGame);
        Test("Save, close and reopen restores progress exactly", SaveAndReload);
        Test("Wins, losses and the furthest battle reached", Recording);
        Test("Teams are saved per battle", Teams);
        Test("A damaged save starts a new game and is kept aside", Damaged);
        Test("An older save is upgraded", OlderVersion);
        Test("Sections a later system adds are kept, and missing ones get defaults", Sections);
        Test("Saving replaces the file in one step", Writing);
        Test("Reset progress", Reset);

        Console.WriteLine(failures == 0 ? "All save checks passed." : $"{failures} save check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static void NewGame(string folder)
    {
        var store = new SaveStore(folder);
        var save = store.Load(out string problem);
        Expect(problem == null, "no problem reported");
        Expect(save.Version == SaveData.CurrentVersion && save.Stars.Count == 0 && save.Furthest.Count == 0 && save.Teams.Count == 0 && save.LastTeam == null, "the new save is empty and at the current version");
        Expect(!File.Exists(store.FilePath), "loading doesn't create a file");
    }

    static void SaveAndReload(string folder)
    {
        var progress = Progress(folder, out var warnings);
        foreach (var b in realm.Take(6)) progress.RecordBattle(b, "win");
        progress.RecordBattle(realm[6], "lose");
        var team = Formation.AutoPlace(data, new[] { "hilde", "solenne", "thessaly", "maren", "pip" });
        progress.SaveTeam(realm[2].Id, team, "2-3");
        string written = File.ReadAllText(Path.Combine(folder, SaveStore.FileName));

        var reopened = Progress(folder, out var warnings2);
        Expect(warnings.Count == 0 && warnings2.Count == 0, "no warnings");
        Expect(realm.All(b => reopened.IsCleared(b.Id) == realm.IndexOf(b) < 6), "the same battles are cleared");
        Expect(realm.All(b => reopened.Stars(b.Id).SequenceEqual(progress.Stars(b.Id))), "the same stars on every battle");
        Expect(reopened.Furthest("normal") == realm[5].Id, "the same furthest battle");
        Expect(Slots(reopened.LoadTeam(realm[2].Id)) == Slots(team) && reopened.LoadFormation(realm[2].Id, team) == "2-3", "the same team and formation");
        Expect(reopened.Save.ToJson() == written, "writing the reloaded save gives the identical file");
    }

    static void Recording(string folder)
    {
        var progress = Progress(folder, out _);
        Expect(progress.Furthest("normal") == null && !progress.IsCleared(realm[0].Id), "nothing cleared at first");
        progress.RecordBattle(realm[0], "lose");
        progress.RecordBattle(realm[1], "timeout");
        Expect(!progress.IsCleared(realm[0].Id) && !progress.IsCleared(realm[1].Id) && progress.Furthest("normal") == null, "a loss or a timeout clears nothing");
        Expect(File.Exists(Path.Combine(folder, SaveStore.FileName)), "the save is written after every battle, even a loss");
        progress.RecordBattle(realm[4], "win");
        Expect(progress.Stars(realm[4].Id).SequenceEqual(new[] { 1 }), "a win is 1 star (until challenges exist)");
        Expect(progress.Furthest("normal") == realm[4].Id, "a win further on moves the furthest battle");
        progress.RecordBattle(realm[4], "win");
        Expect(progress.Stars(realm[4].Id).SequenceEqual(new[] { 1 }), "winning again doesn't add the star twice");
        progress.RecordBattle(realm[2], "win");
        Expect(progress.Furthest("normal") == realm[4].Id, "winning an earlier battle keeps the furthest battle");
        progress.RecordBattle(realm[2], "lose");
        Expect(progress.IsCleared(realm[2].Id), "losing a cleared battle keeps it cleared");
        progress.RecordBattle(realm[13], "win");
        Expect(progress.Furthest("normal") == realm[13].Id, "a later stage is further than an earlier one");
        var hard = data.Stages.First(s => s.Difficulty != "normal");
        progress.RecordBattle(hard, "win");
        Expect(progress.Furthest(hard.Difficulty) == hard.Id && progress.Furthest("normal") == realm[13].Id, "each difficulty keeps its own furthest battle");
        Expect(Progress(folder, out _).Furthest("normal") == realm[13].Id, "the furthest battle survives a reload");
    }

    static void Teams(string folder)
    {
        var progress = Progress(folder, out _);
        var fallback = progress.LoadTeam(realm[0].Id);
        Expect(fallback.Count == 5, "a new game offers a default team of 5");
        var a = new List<TeamSlot> { new TeamSlot("hilde", "front0"), new TeamSlot("pip", "back1") };
        var b = new List<TeamSlot> { new TeamSlot("maren", "front1"), new TeamSlot("ysolde", "front2"), new TeamSlot("pip", "back0") };
        progress.SaveTeam(realm[0].Id, a, "2-3");
        progress.SaveTeam(realm[1].Id, b, "3-2");
        var reopened = Progress(folder, out _);
        Expect(Slots(reopened.LoadTeam(realm[0].Id)) == Slots(a) && Slots(reopened.LoadTeam(realm[1].Id)) == Slots(b), "each battle keeps its own team");
        Expect(Slots(reopened.LoadTeam(realm[9].Id)) == Slots(b), "a battle never played opens with the last team used");
        Expect(reopened.LoadFormation(realm[1].Id, b) == "3-2", "the formation is kept with the team");

        // A hero removed from the game since the team was saved: that team is skipped.
        var save = reopened.Save;
        save.Teams[realm[0].Id] = new SavedTeam("2-3", new[] { new TeamSlot("no-such-hero", "front0") });
        new SaveStore(folder).Save(save);
        Expect(Slots(Progress(folder, out _).LoadTeam(realm[0].Id)) == Slots(b), "a saved team with an unknown hero falls back to the last team");
    }

    static void Damaged(string folder)
    {
        var good = new SaveData();
        good.Stars["c0-0"] = new List<int> { 1 };
        string goodJson = good.ToJson();
        var cases = new (string what, string text)[]
        {
            ("not JSON", "this is not a save"),
            ("cut off part-way", goodJson.Substring(0, goodJson.Length / 2)),
            ("empty", ""),
            ("not an object", "[1, 2, 3]"),
            ("no version", "{\"campaign\": {}}"),
            ("version 0", "{\"version\": 0}"),
            ("newer than this game", "{\"version\": " + (SaveData.CurrentVersion + 1) + "}"),
            ("stars of the wrong type", goodJson.Replace("[1]", "\"one\"")),
            ("a star out of range", goodJson.Replace("[1]", "[7]")),
            ("a star twice", goodJson.Replace("[1]", "[1, 1]")),
            ("a team missing its heroes", "{\"version\": 1, \"teams\": {\"last\": {\"formation\": \"2-3\"}}}"),
        };
        foreach (var (what, text) in cases)
        {
            foreach (var f in Directory.GetFiles(folder)) File.Delete(f);
            var store = new SaveStore(folder);
            File.WriteAllText(store.FilePath, text);
            var warnings = new List<string>();
            PlayerProgress progress = null;
            try { progress = new PlayerProgress(data, store, warnings.Add); }
            catch (Exception e) { Expect(false, $"{what}: loading throws {e.GetType().Name}: {e.Message}"); continue; }
            var kept = Directory.GetFiles(folder, "save.damaged-*.json");
            Expect(progress.Save.Stars.Count == 0 && progress.Save.Version == SaveData.CurrentVersion, $"{what}: starts a new game");
            Expect(warnings.Count == 1 && kept.Length == 1 && warnings[0].Contains(Path.GetFileName(kept[0])), $"{what}: reports where the old file was kept ({warnings.FirstOrDefault()})");
            Expect(kept.Length == 1 && File.ReadAllText(kept[0]) == text, $"{what}: the old file is kept unchanged");
            Expect(!File.Exists(store.FilePath), $"{what}: the damaged file no longer sits in the save's place");
            progress.RecordBattle(realm[0], "win");
            Expect(Progress(folder, out var w).IsCleared(realm[0].Id) && w.Count == 0, $"{what}: the new game saves and reloads normally");
        }

        // Two damaged saves in the same second are both kept.
        foreach (var f in Directory.GetFiles(folder)) File.Delete(f);
        var s = new SaveStore(folder);
        File.WriteAllText(s.FilePath, "bad 1"); s.Load(out _);
        File.WriteAllText(s.FilePath, "bad 2"); s.Load(out _);
        Expect(Directory.GetFiles(folder, "save.damaged-*.json").Select(File.ReadAllText).OrderBy(x => x).SequenceEqual(new[] { "bad 1", "bad 2" }), "two damaged saves are both kept");
    }

    static void OlderVersion(string folder)
    {
        // Version 1 is the only format so far, so the test pretends the game is at version 2, whose one
        // upgrade step gives every older save a "heroes" section with the starter hero.
        var v1 = new SaveData();
        v1.Stars["c0-0"] = new List<int> { 1 };
        v1.Stars["c0-1"] = new List<int> { 1 };
        v1.Furthest["normal"] = "c0-1";
        v1.LastTeam = new SavedTeam("2-3", new[] { new TeamSlot("hilde", "front0") });
        string v1Json = v1.ToJson();
        var upgrades = new List<Func<Dictionary<string, object>, Dictionary<string, object>>>
        {
            root => { root["heroes"] = new Dictionary<string, object> { ["hilde"] = new Dictionary<string, object> { ["level"] = 1.0 } }; return root; },
        };
        SaveData ReadV2(string text) => SaveData.FromJson(text, 2, upgrades);

        var upgraded = ReadV2(v1Json);
        Expect(upgraded.Version == 2, "the upgraded save is at the new version");
        Expect(upgraded.Stars.Keys.OrderBy(k => k).SequenceEqual(new[] { "c0-0", "c0-1" }) && upgraded.Furthest["normal"] == "c0-1" && upgraded.LastTeam.Heroes[0].Id == "hilde", "everything in the old save survives the upgrade");
        Expect(upgraded.ToJson().Contains("\"version\": 2") && upgraded.ToJson().Contains("\"heroes\""), "it is written back as the new version, with the new section");

        var store = new SaveStore(folder);
        File.WriteAllText(store.FilePath, v1Json);
        var loaded = store.Load(out string problem, ReadV2);
        Expect(problem == null && loaded.Version == 2 && loaded.Stars.Count == 2, "an older save on the device loads through the upgrade, not as damaged");
        Expect(Directory.GetFiles(folder, "save.damaged-*.json").Length == 0, "nothing is set aside");

        Expect(SaveData.FromJson(v1Json).Version == SaveData.CurrentVersion, "a current-version save needs no upgrade");
    }

    static void Sections(string folder)
    {
        // A save from a later game that has a heroes and an inventory section this version doesn't read.
        var later = new SaveData();
        later.Stars["c0-0"] = new List<int> { 1 };
        string json = later.ToJson().TrimEnd().TrimEnd('}') + ",\n  \"heroes\": {\"hilde\": {\"level\": 12}},\n  \"inventory\": {\"gold\": 1500}\n}\n";
        var read = SaveData.FromJson(json);
        Expect(read.IsCleared("c0-0"), "the parts this version knows are read");
        read.Stars["c0-1"] = new List<int> { 1 };
        var again = Parse(read.ToJson());
        Expect(again.ContainsKey("heroes") && again.ContainsKey("inventory") && Json.Parse(read.ToJson()) is Dictionary<string, object>, "sections this version doesn't know are written back");
        Expect(((Dictionary<string, object>)((Dictionary<string, object>)again["heroes"])["hilde"])["level"] is double lvl && lvl == 12, "and their contents are unchanged");

        var bare = SaveData.FromJson("{\"version\": 1}");
        Expect(bare.Stars.Count == 0 && bare.Furthest.Count == 0 && bare.Teams.Count == 0 && bare.LastTeam == null, "a save with only a version loads with empty sections");
        var partial = SaveData.FromJson("{\"version\": 1, \"campaign\": {\"stars\": {\"c0-3\": [1]}}}");
        Expect(partial.IsCleared("c0-3") && partial.Furthest.Count == 0, "a section missing a field loads with that field empty");
    }

    static void Writing(string folder)
    {
        var store = new SaveStore(Path.Combine(folder, "not-yet-made"));
        var save = new SaveData();
        save.Stars["c0-0"] = new List<int> { 1 };
        store.Save(save);
        Expect(File.Exists(store.FilePath), "the save folder is created if it doesn't exist");
        save.Stars["c0-1"] = new List<int> { 1 };
        store.Save(save);
        Expect(store.Load(out _).Stars.Count == 2, "a second save replaces the first");
        Expect(Directory.GetFiles(store.Folder).Select(Path.GetFileName).SequenceEqual(new[] { SaveStore.FileName }), "no temporary file is left behind");
    }

    static void Reset(string folder)
    {
        var progress = Progress(folder, out _);
        progress.RecordBattle(realm[0], "win");
        progress.SaveTeam(realm[0].Id, new List<TeamSlot> { new TeamSlot("pip", "back0") }, "2-3");
        File.WriteAllText(Path.Combine(folder, "save.damaged-20261009-120000.json"), "kept for checking");
        progress.Reset();
        Expect(!progress.IsCleared(realm[0].Id) && progress.Furthest("normal") == null && progress.LoadTeam(realm[0].Id).Count == 5, "progress and teams are forgotten");
        Expect(!File.Exists(Path.Combine(folder, SaveStore.FileName)), "the save file is removed");
        Expect(File.Exists(Path.Combine(folder, "save.damaged-20261009-120000.json")), "damaged saves kept for checking stay");
        Expect(!Progress(folder, out _).IsCleared(realm[0].Id), "reopening after a reset is a new game");
    }

    static PlayerProgress Progress(string folder, out List<string> warnings)
    {
        var w = warnings = new List<string>();
        return new PlayerProgress(data, new SaveStore(folder), w.Add);
    }

    static bool IsCleared(this SaveData save, string id) => save.Stars.TryGetValue(id, out var s) && s.Contains(1);
    static Dictionary<string, object> Parse(string json) => (Dictionary<string, object>)Json.Parse(json);
    static string Slots(List<TeamSlot> team) => string.Join(",", team.Select(t => t.Id + "@" + t.Slot).OrderBy(x => x));

    static void Test(string name, Action<string> test)
    {
        Console.WriteLine(name);
        string folder = Path.Combine(Path.GetTempPath(), "sp-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { test(folder); }
        catch (Exception e) { Expect(false, $"threw {e.GetType().Name}: {e.Message}"); }
        finally { Directory.Delete(folder, true); }
    }

    static void Expect(bool ok, string what) { if (!ok) failures++; Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); }

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "Unity", "Assets"))) return d.FullName;
        throw new DirectoryNotFoundException("Run from inside the repository");
    }
}
