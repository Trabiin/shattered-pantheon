// Tests for the game's save file: a new game, saving and loading, closing and reopening, damaged and
// unreadable saves, upgrading an older version, keeping sections a later system adds, the hero cards
// and items the player owns, the wallet every change to them goes through, battle XP, and copies.
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
    static CampaignCurve curve;
    static List<StageDef> realm;
    static readonly string[] Five = { "hilde", "solenne", "thessaly", "maren", "pip" };

    static int Main()
    {
        string dir = Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData");
        string R(string f) => File.ReadAllText(Path.Combine(dir, f + ".json"));
        data = GameData.FromJson(R("rules"), R("heroes"), R("enemies"), R("campaign"));
        curve = CampaignCurve.FromJson(R("progression"));
        realm = data.Stages.Where(s => s.RealmNumber == 1 && s.Difficulty == "normal").OrderBy(s => s.StageNumber).ThenBy(s => s.BattleNumber).ToList();

        Test("A missing save is a new game", NewGame);
        Test("Save, close and reopen restores progress exactly", SaveAndReload);
        Test("Wins, losses and the furthest battle reached", Recording);
        Test("Teams are saved per battle", Teams);
        Test("A damaged save starts a new game and is kept aside", Damaged);
        Test("An older save is upgraded", OlderVersion);
        Test("A version 1 save keeps renamed heroes in its teams", RenamedHeroes);
        Test("Sections a later system adds are kept, and missing ones get defaults", Sections);
        Test("Saving replaces the file in one step", Writing);
        Test("Reset progress", Reset);
        Test("A new game owns the starting heroes and no items", StartingHeroes);
        Test("A save from before heroes were kept gets the starting heroes", SaveWithoutHeroes);
        Test("The wallet: earning, spending and refusing to overspend", WalletItems);
        Test("Battle XP after a first win", BattleXp);
        Test("Battle XP stops at the level cap", LevelCaps);
        Test("Heroes fight at their own strength", Strength);
        Test("Heroes and items survive closing and reopening", HeroesReload);
        Test("Every copy of a hero is its own card, and copies fight side by side", Copies);

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
        foreach (var b in realm.Take(6)) progress.RecordBattle(b, "win", Five);
        progress.RecordBattle(realm[6], "lose", Five);
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
        progress.RecordBattle(realm[0], "lose", Five);
        progress.RecordBattle(realm[1], "timeout", Five);
        Expect(!progress.IsCleared(realm[0].Id) && !progress.IsCleared(realm[1].Id) && progress.Furthest("normal") == null, "a loss or a timeout clears nothing");
        Expect(File.Exists(Path.Combine(folder, SaveStore.FileName)), "the save is written after every battle, even a loss");
        progress.RecordBattle(realm[4], "win", Five);
        Expect(progress.Stars(realm[4].Id).SequenceEqual(new[] { 1 }), "a win is 1 star (until challenges exist)");
        Expect(progress.Furthest("normal") == realm[4].Id, "a win further on moves the furthest battle");
        progress.RecordBattle(realm[4], "win", Five);
        Expect(progress.Stars(realm[4].Id).SequenceEqual(new[] { 1 }), "winning again doesn't add the star twice");
        progress.RecordBattle(realm[2], "win", Five);
        Expect(progress.Furthest("normal") == realm[4].Id, "winning an earlier battle keeps the furthest battle");
        progress.RecordBattle(realm[2], "lose", Five);
        Expect(progress.IsCleared(realm[2].Id), "losing a cleared battle keeps it cleared");
        progress.RecordBattle(realm[13], "win", Five);
        Expect(progress.Furthest("normal") == realm[13].Id, "a later stage is further than an earlier one");
        var hard = data.Stages.First(s => s.Difficulty != "normal");
        progress.RecordBattle(hard, "win", Five);
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
            ("a hero at level 0", "{\"version\": 2, \"heroes\": {\"hilde\": {\"level\": 0}}}"),
            ("a hero with negative XP", "{\"version\": 2, \"heroes\": {\"hilde\": {\"level\": 3, \"xp\": -1}}}"),
            ("a hero that isn't an object", "{\"version\": 2, \"heroes\": {\"hilde\": 3}}"),
            ("a card whose hero isn't a name", "{\"version\": 2, \"heroes\": {\"hilde#2\": {\"hero\": 7}}}"),
            ("a negative amount of gold", "{\"version\": 2, \"inventory\": {\"gold\": -5}}"),
            ("part of a skill tome", "{\"version\": 2, \"inventory\": {\"skillTomes\": 1.5}}"),
        };
        foreach (var (what, text) in cases)
        {
            foreach (var f in Directory.GetFiles(folder)) File.Delete(f);
            var store = new SaveStore(folder);
            File.WriteAllText(store.FilePath, text);
            var warnings = new List<string>();
            PlayerProgress progress = null;
            try { progress = new PlayerProgress(data, curve, store, warnings.Add); }
            catch (Exception e) { Expect(false, $"{what}: loading throws {e.GetType().Name}: {e.Message}"); continue; }
            var kept = Directory.GetFiles(folder, "save.damaged-*.json");
            Expect(progress.Save.Stars.Count == 0 && progress.Save.Version == SaveData.CurrentVersion, $"{what}: starts a new game");
            Expect(warnings.Count == 1 && kept.Length == 1 && warnings[0].Contains(Path.GetFileName(kept[0])), $"{what}: reports where the old file was kept ({warnings.FirstOrDefault()})");
            Expect(warnings.Count == 1 && System.Text.RegularExpressions.Regex.IsMatch(warnings[0], @"[.!?] Started a new game;"), $"{what}: the reader's message ends with a full stop before \"Started a new game\"");
            Expect(kept.Length == 1 && File.ReadAllText(kept[0]) == text, $"{what}: the old file is kept unchanged");
            Expect(!File.Exists(store.FilePath), $"{what}: the damaged file no longer sits in the save's place");
            progress.RecordBattle(realm[0], "win", Five);
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
        // The test pretends the game is one version newer than it is, with an extra upgrade step that
        // gives every older save a "heroes" section with the starter hero.
        int next = SaveData.CurrentVersion + 1;
        var old = new SaveData();
        old.Stars["c0-0"] = new List<int> { 1 };
        old.Stars["c0-1"] = new List<int> { 1 };
        old.Furthest["normal"] = "c0-1";
        old.LastTeam = new SavedTeam("2-3", new[] { new TeamSlot("hilde", "front0") });
        string oldJson = old.ToJson();
        var upgrades = new List<Func<Dictionary<string, object>, Dictionary<string, object>>>(SaveData.Upgrades)
        {
            root => { root["heroes"] = new Dictionary<string, object> { ["hilde"] = new Dictionary<string, object> { ["level"] = 1.0 } }; return root; },
        };
        SaveData ReadNext(string text) => SaveData.FromJson(text, next, upgrades);

        var upgraded = ReadNext(oldJson);
        Expect(upgraded.Version == next, "the upgraded save is at the new version");
        Expect(upgraded.Stars.Keys.OrderBy(k => k).SequenceEqual(new[] { "c0-0", "c0-1" }) && upgraded.Furthest["normal"] == "c0-1" && upgraded.LastTeam.Heroes[0].Id == "hilde", "everything in the old save survives the upgrade");
        Expect(upgraded.ToJson().Contains($"\"version\": {next}") && upgraded.ToJson().Contains("\"heroes\""), "it is written back as the new version, with the new section");

        var store = new SaveStore(folder);
        File.WriteAllText(store.FilePath, oldJson);
        var loaded = store.Load(out string problem, ReadNext);
        Expect(problem == null && loaded.Version == next && loaded.Stars.Count == 2, "an older save on the device loads through the upgrade, not as damaged");
        Expect(Directory.GetFiles(folder, "save.damaged-*.json").Length == 0, "nothing is set aside");

        Expect(SaveData.FromJson(oldJson).Version == SaveData.CurrentVersion, "a current-version save needs no upgrade");
    }

    static void RenamedHeroes(string folder)
    {
        // Version 2 renamed Pell the Gambler to Jink and Varkhul the Ashen to Korvald (E4-F1-S1).
        string v1 = "{\"version\": 1, \"campaign\": {\"stars\": {\"c0-0\": [1]}, \"furthest\": {\"normal\": \"c0-0\"}}, \"teams\": {" +
            "\"last\": {\"formation\": \"2-3\", \"heroes\": [{\"hero\": \"varkhul\", \"slot\": \"front0\"}, {\"hero\": \"hilde\", \"slot\": \"front1\"}, {\"hero\": \"pell\", \"slot\": \"back0\"}, {\"hero\": \"grub\", \"slot\": \"back1\"}, {\"hero\": \"maren\", \"slot\": \"back2\"}]}, " +
            "\"battles\": {\"c0-0\": {\"formation\": \"2-3\", \"heroes\": [{\"hero\": \"pell\", \"slot\": \"back0\"}]}}}}";
        var store = new SaveStore(folder);
        File.WriteAllText(store.FilePath, v1);
        var progress = Progress(folder, out var warnings);
        Expect(warnings.Count == 0 && progress.Save.Version == SaveData.CurrentVersion && progress.IsCleared("c0-0"), "it loads at the current version with its progress");
        Expect(Slots(progress.Save.LastTeam.Heroes) == "grub@back1,hilde@front1,jink@back0,korvald@front0,maren@back2" && progress.Save.Teams["c0-0"].Heroes[0].Id == "jink", "both renamed heroes keep their slots, in every saved team");
        Expect(Slots(progress.LoadTeam(realm[1].Id)) == Slots(progress.Save.LastTeam.Heroes), "the game puts the saved team back on the team screen");
        progress.Write();
        Expect(!File.ReadAllText(store.FilePath).Contains("pell") && !File.ReadAllText(store.FilePath).Contains("varkhul"), "the old ids are gone from the save written back");
    }

    static void Sections(string folder)
    {
        // A save from a later game that has a daily section and an item this version doesn't read.
        var later = new SaveData();
        later.Stars["c0-0"] = new List<int> { 1 };
        later.Items["godshards"] = 300;
        string json = later.ToJson().TrimEnd().TrimEnd('}') + ",\n  \"daily\": {\"shrine\": {\"collected\": 12}}\n}\n";
        var read = SaveData.FromJson(json);
        Expect(read.IsCleared("c0-0"), "the parts this version knows are read");
        read.Stars["c0-1"] = new List<int> { 1 };
        var again = Parse(read.ToJson());
        Expect(again.ContainsKey("daily"), "sections this version doesn't know are written back");
        Expect(((Dictionary<string, object>)((Dictionary<string, object>)again["daily"])["shrine"])["collected"] is double c && c == 12, "and their contents are unchanged");
        Expect(((Dictionary<string, object>)again["inventory"])["godshards"] is double g && g == 300, "items this version doesn't know are kept");

        var bare = SaveData.FromJson("{\"version\": 1}");
        Expect(bare.Stars.Count == 0 && bare.Furthest.Count == 0 && bare.Teams.Count == 0 && bare.LastTeam == null && bare.Heroes.Count == 0 && Item.All.All(i => bare.Items[i] == 0), "a save with only a version loads with empty sections");
        var hero = SaveData.FromJson("{\"version\": 2, \"heroes\": {\"pip\": {\"level\": 4}}}").Heroes["pip"];
        Expect(hero.Hero == "pip" && hero.Level == 4 && hero.Xp == 0 && hero.Stars == 1 && hero.Skill == 1, "a hero missing a field loads with that field's default (a card without its hero is the hero its id names)");
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
        progress.RecordBattle(realm[0], "win", Five);
        progress.SaveTeam(realm[0].Id, new List<TeamSlot> { new TeamSlot("pip", "back0") }, "2-3");
        File.WriteAllText(Path.Combine(folder, "save.damaged-20261009-120000.json"), "kept for checking");
        progress.Reset();
        Expect(!progress.IsCleared(realm[0].Id) && progress.Furthest("normal") == null && progress.LoadTeam(realm[0].Id).Count == 5, "progress and teams are forgotten");
        Expect(data.Heroes.All(h => progress.Wallet.Hero(h.Id)?.Level == 1), "the heroes are back to a new game's");
        Expect(!File.Exists(Path.Combine(folder, SaveStore.FileName)), "the save file is removed");
        Expect(File.Exists(Path.Combine(folder, "save.damaged-20261009-120000.json")), "damaged saves kept for checking stay");
        Expect(!Progress(folder, out _).IsCleared(realm[0].Id), "reopening after a reset is a new game");
    }

    static void StartingHeroes(string folder)
    {
        var progress = Progress(folder, out var warnings);
        var growth = curve.Growth;
        // TODO(E9-F2-S1-T5): a new game owns only the 3 starters once summons exist.
        Expect(data.Heroes.All(h => progress.Wallet.Owns(h.Id)) && progress.Save.Heroes.Count == data.Heroes.Count, "every test hero is owned (until summons exist)");
        Expect(data.Heroes.All(h => progress.Wallet.Hero(h.Id) is var o && o.Hero == h.Id && o.Level == 1 && o.Xp == 0 && o.Skill == 1 && o.Stars == growth.StartStars(h.Rarity)),
            "one card each, under the hero's own id, at level 1 with no XP, skill level 1 and their rarity's starting stars (Common 1★ to Legendary 5★)");
        Expect(Item.All.All(i => progress.Wallet.Count(i) == 0), "the inventory is empty");
        Expect(warnings.Count == 0 && !File.Exists(Path.Combine(folder, SaveStore.FileName)), "nothing is written until something happens");
        var team = progress.StrongestTeam();
        Expect(team.Count == 5 && team.Distinct().Count() == 5 && team.Any(id => data.Hero(id).Role == "Tank") && team.Any(id => data.Hero(id).Role == "Support"), $"the default team is 5 strong heroes with a Tank and a Support ({string.Join(", ", team)})");
        Expect(data.Heroes.Where(h => h.Rarity == "Legendary").All(h => team.Contains(h.Id)), "it includes the Legendaries");
        Expect(Slots(progress.LoadTeam(realm[0].Id)) == Slots(Formation.AutoPlace(data, team)), "a new game's team screen opens with it");
    }

    static void SaveWithoutHeroes(string folder)
    {
        string v2 = "{\"version\": 2, \"campaign\": {\"stars\": {\"c0-0\": [1], \"c0-1\": [1]}, \"furthest\": {\"normal\": \"c0-1\"}}}";
        File.WriteAllText(Path.Combine(folder, SaveStore.FileName), v2);
        var progress = Progress(folder, out var warnings);
        Expect(warnings.Count == 0 && progress.IsCleared("c0-1"), "it loads with its progress");
        Expect(data.Heroes.All(h => progress.Wallet.Hero(h.Id)?.Level == 1), "and the starting heroes at level 1");
    }

    static void WalletItems(string folder)
    {
        var progress = Progress(folder, out _);
        var wallet = progress.Wallet;
        wallet.Earn(new Dictionary<string, long> { [Item.Gold] = 500, [Item.SkillTomes] = 3, [Item.Vessel(3)] = 2 });
        Expect(wallet.Count(Item.Gold) == 500 && wallet.Count(Item.SkillTomes) == 3 && wallet.Count(Item.Vessel(3)) == 2 && wallet.Count(Item.Vessel(4)) == 0, "earning adds each item");
        Expect(Progress(folder, out _).Wallet.Count(Item.Gold) == 500, "earning is saved straight away");
        wallet.Earn(new Dictionary<string, long> { [Item.Gold] = 250 });
        Expect(wallet.Count(Item.Gold) == 750, "earning more adds to what's there");

        Expect(wallet.Spend(new Dictionary<string, long> { [Item.Gold] = 300, [Item.SkillTomes] = 1 }) && wallet.Count(Item.Gold) == 450 && wallet.Count(Item.SkillTomes) == 2, "spending what you have takes it");
        Expect(Progress(folder, out _).Wallet.Count(Item.Gold) == 450, "spending is saved straight away");
        Expect(wallet.Spend(new Dictionary<string, long> { [Item.Gold] = 450 }) && wallet.Count(Item.Gold) == 0, "spending everything leaves zero");

        string before = File.ReadAllText(Path.Combine(folder, SaveStore.FileName));
        Expect(!wallet.Spend(new Dictionary<string, long> { [Item.Gold] = 1 }), "can't spend gold you don't have");
        Expect(!wallet.Spend(new Dictionary<string, long> { [Item.SkillTomes] = 1, [Item.Vessel(3)] = 3 }) && wallet.Count(Item.SkillTomes) == 2 && wallet.Count(Item.Vessel(3)) == 2, "a cost you can only partly pay takes nothing");
        Expect(!wallet.Spend(new Dictionary<string, long> { [Item.RebirthScrolls] = 1 }), "can't spend an item you never had");
        Expect(File.ReadAllText(Path.Combine(folder, SaveStore.FileName)) == before, "a refused spend changes nothing in the save");

        Expect(Throws(() => wallet.Earn(new Dictionary<string, long> { ["diamonds"] = 1 })), "an unknown item is refused");
        Expect(Throws(() => wallet.Earn(new Dictionary<string, long> { [Item.Gold] = -10 })) && Throws(() => wallet.Spend(new Dictionary<string, long> { [Item.Gold] = -10 })), "a negative amount is refused both ways");
        Expect(wallet.Count(Item.Gold) == 0, "and changes nothing");
    }

    static void BattleXp(string folder)
    {
        var progress = Progress(folder, out _);
        var growth = curve.Growth;
        long xp = curve.FirstClearXp(realm[0]);
        Expect(xp == Math.Round(curve.FirstClearXpLevels * growth.XpToNext(1)) && xp > 0, $"realm 1's first battle gives {xp} XP: progression.json's first-clear XP at its recommended level");
        long late = curve.FirstClearXp(realm[39]);
        Expect(late == Math.Round(curve.FirstClearXpLevels * growth.XpToNext(curve.RecLevel(0, 39))) && late > xp, $"later battles give more ({late} XP at the realm boss)");

        var three = new[] { "hilde", "grub", "korvald" };
        progress.RecordBattle(realm[0], "lose", three);
        Expect(three.All(id => Total(progress, id) == 0), "a loss gives no XP");
        progress.RecordBattle(realm[0], "win", three);
        Expect(three.All(id => Total(progress, id) == xp / 3), $"a first win shares the XP evenly by the heroes who fought ({xp / 3} each)");
        Expect(data.Heroes.Where(h => !three.Contains(h.Id)).All(h => Total(progress, h.Id) == 0), "heroes who didn't fight get none");
        progress.RecordBattle(realm[0], "win", three);
        Expect(three.All(id => Total(progress, id) == xp / 3), "replaying a cleared battle gives none (until farming arrives)");
        Expect(Progress(folder, out _).Wallet.Hero("hilde").Xp == progress.Wallet.Hero("hilde").Xp, "battle XP is saved");

        // Levelling up: every time XP reaches the next level, without gold.
        var wallet = progress.Wallet;
        var pip = wallet.Hero("pip");
        wallet.ShareBattleXp(new[] { "pip" }, (long)growth.XpToNext(1) - 1);
        Expect(pip.Level == 1 && pip.Xp == growth.XpToNext(1) - 1, "one XP short of the next level stays at level 1");
        wallet.ShareBattleXp(new[] { "pip" }, 1);
        Expect(pip.Level == 2 && pip.Xp == 0, "reaching it levels up, with nothing left over");
        wallet.ShareBattleXp(new[] { "pip" }, (long)(growth.XpToNext(2) + growth.XpToNext(3) + growth.XpToNext(4)) + 7);
        Expect(pip.Level == 5 && pip.Xp == 7, "enough XP for several levels gives them all at once, and keeps the rest");
        Expect(wallet.Count(Item.Gold) == 0, "levelling up costs no gold");
        wallet.ShareBattleXp(new[] { "pip", "pip", "no-such-hero" }, 10);
        Expect(pip.Xp == 17, "a hero listed twice gets one share, and an unknown hero none");
    }

    static void LevelCaps(string folder)
    {
        var progress = Progress(folder, out _);
        var growth = curve.Growth;
        var wallet = progress.Wallet;
        var kesh = wallet.Hero("kesh");   // Common: 1★, capped at level 30
        int cap = growth.LevelCap(kesh.Stars);
        Expect(kesh.Stars == 1 && cap == 30 && wallet.LevelCap(kesh) == 30, "a 1★ hero's cap is level 30");
        long toCap = Enumerable.Range(1, cap - 1).Sum(l => (long)growth.XpToNext(l));
        wallet.ShareBattleXp(new[] { "kesh" }, toCap + 5000);
        Expect(kesh.Level == cap && kesh.Xp == 0, "XP past the cap stops at the cap and isn't kept");
        wallet.ShareBattleXp(new[] { "kesh", "maren" }, 1000);
        Expect(kesh.Level == cap && kesh.Xp == 0 && Total(progress, "maren") == 1000, "a capped hero takes no share: the others get it all");
        wallet.ShareBattleXp(new[] { "kesh" }, 1000);
        Expect(kesh.Level == cap && kesh.Xp == 0, "a team of capped heroes gets nothing");
    }

    static void Strength(string folder)
    {
        var progress = Progress(folder, out _);
        var growth = curve.Growth;
        Expect(progress.HeroScale("thessaly") == growth.StatScale("Epic", 1, 4, 0, 1) && Math.Abs(progress.HeroScale("thessaly") - 1.1) < 1e-9, "a new Epic hero fights at 1.1x their listed stats (4★, level 1)");
        Expect(Math.Abs(progress.HeroScale("kesh") - 0.6) < 1e-9 && Math.Abs(progress.HeroScale("solenne") - 1.38) < 1e-9, "a new Common at 0.6x, a new Legendary at 1.38x");
        progress.Wallet.ShareBattleXp(new[] { "thessaly" }, (long)(growth.XpToNext(1) + growth.XpToNext(2)));
        Expect(Math.Abs(progress.HeroScale("thessaly") - 1.1 * 1.06) < 1e-9, "two levels later: +3% a level");
        Expect(Math.Abs(progress.HeroScale("thessaly") - curve.RecScale(0, 0) * 1.06) < 1e-9, "the first battle's recommended strength is a new Epic's");
        Expect(Throws(() => progress.HeroScale("no-such-hero")), "a hero the player doesn't own can't fight");
    }

    static void HeroesReload(string folder)
    {
        var progress = Progress(folder, out _);
        foreach (var b in realm.Take(10)) progress.RecordBattle(b, "win", Five);
        progress.Wallet.Earn(new Dictionary<string, long> { [Item.XpPotions] = 1200, [Item.Vessel(5)] = 1, [Item.RebirthScrolls] = 1 });
        string written = File.ReadAllText(Path.Combine(folder, SaveStore.FileName));
        var reopened = Progress(folder, out var warnings);
        Expect(warnings.Count == 0, "no warnings");
        Expect(Five.All(id => reopened.Wallet.Hero(id).Level > 1) && data.Heroes.All(h => Same(reopened.Wallet.Hero(h.Id), progress.Wallet.Hero(h.Id))), "every card's hero, level, XP, stars and skill come back");
        Expect(Item.All.All(i => reopened.Wallet.Count(i) == progress.Wallet.Count(i)) && reopened.Wallet.Count(Item.XpPotions) == 1200, "every item comes back");
        Expect(Five.All(id => reopened.HeroScale(id) == progress.HeroScale(id)), "heroes fight at the same strength after reopening");
        Expect(reopened.Save.ToJson() == written, "writing the reloaded save gives the identical file");
    }

    static void Copies(string folder)
    {
        var progress = Progress(folder, out _);
        var wallet = progress.Wallet;
        var second = wallet.NewCard("hilde");
        var third = wallet.NewCard("hilde");
        Expect(second.Id == "hilde#2" && third.Id == "hilde#3" && second.Hero == "hilde" && wallet.Hero("hilde").Hero == "hilde", "a new copy gets the next card id: hilde, hilde#2, hilde#3");
        Expect(second.Level == 1 && second.Xp == 0 && second.Stars == curve.Growth.StartStars(data.Hero("hilde").Rarity) && second.Skill == 1, "a new copy starts at level 1 and its rarity's starting stars");
        Expect(Throws(() => wallet.NewCard("no-such-hero")), "a card for an unknown hero is refused");
        var hildes = progress.Cards.Where(c => c.Hero == "hilde").Select(c => c.Id);
        Expect(hildes.SequenceEqual(new[] { "hilde", "hilde#2", "hilde#3" }) && progress.Cards.Count == data.Heroes.Count + 2, "the cards list every copy, after the first");
        Expect(progress.HeroOf("hilde#2").Id == "hilde", "a copy is the hero it copies");

        // A team with two copies of Hilde: each card grows and fights at its own strength.
        var cards = new[] { "hilde", "hilde#2", "grub", "korvald", "maren" };
        wallet.ShareBattleXp(new[] { "hilde#2" }, (long)curve.Growth.XpToNext(1));
        Expect(wallet.Hero("hilde#2").Level == 2 && wallet.Hero("hilde").Level == 1, "XP for one copy levels only that copy");
        var team = progress.AutoPlace(cards);
        Expect(team.Select(t => t.Id).OrderBy(x => x).SequenceEqual(cards.OrderBy(x => x)) && team.Select(t => t.Slot).Distinct().Count() == 5, "auto-placing keeps both copies, each in its own slot");
        var fighting = progress.FightingTeam(team);
        Expect(fighting.Count(t => t.Id == "hilde") == 2 && fighting.All(t => data.Hero(t.Id) != null), "the battle gets Hilde twice");
        Expect(fighting.Zip(team, (f, c) => f.Slot == c.Slot && f.HpScale == progress.HeroScale(c.Id) && f.AtkScale == f.HpScale).All(x => x) && progress.HeroScale("hilde#2") > progress.HeroScale("hilde"), "each copy at its own card's strength");
        var fight = new Battle(data, fighting, realm[0].Id, 1, new BattleOptions());
        while (!fight.Over) fight.Step();
        Expect(fight.Units.Count(u => u.Side == "A" && u.Id == "hilde") == 2, $"the fight plays with both ({fight.Result})");
        long xp = curve.FirstClearXp(realm[0]);
        long before = Total(progress, "hilde");
        progress.RecordBattle(realm[0], "win", cards);
        Expect(Total(progress, "hilde") == before + xp / 5 && Total(progress, "hilde#2") == curve.Growth.XpToNext(1) + xp / 5, "battle XP gives each copy its own share");
        progress.SaveTeam(realm[1].Id, team, Battle.FormationOf(team.Select(t => t.Slot)));
        Expect(Slots(progress.LoadTeam(realm[1].Id)) == Slots(team), "a team with copies is saved and put back");

        var reopened = Progress(folder, out var warnings);
        Expect(warnings.Count == 0 && Same(reopened.Wallet.Hero("hilde#2"), second) && Same(reopened.Wallet.Hero("hilde#3"), third), "copies come back after reopening");
        Expect(Slots(reopened.LoadTeam(realm[1].Id)) == Slots(team), "and so does the team that fields them");

        // A card whose hero left the game data (renamed or removed) isn't listed and can't be fielded.
        reopened.Save.Heroes["ghost"] = new OwnedHero { Id = "ghost", Hero = "no-such-hero" };
        reopened.SaveTeam(realm[2].Id, new List<TeamSlot> { new TeamSlot("ghost", "front0"), new TeamSlot("hilde", "front1") }, "2-3");
        Expect(reopened.Cards.All(c => c.Id != "ghost") && reopened.LoadTeam(realm[2].Id).All(t => t.Id != "ghost"), "a card for a hero the game no longer has is left out");
        reopened.SaveTeam(realm[3].Id, new List<TeamSlot> { new TeamSlot("hilde", "front0"), new TeamSlot("hilde", "front1") }, "2-3");
        Expect(Slots(reopened.LoadTeam(realm[3].Id)) != "hilde@front0,hilde@front1", "the same card can't stand in two slots");
    }

    static long Total(PlayerProgress p, string id)
    {
        var h = p.Wallet.Hero(id);
        return Enumerable.Range(1, h.Level - 1).Sum(l => (long)curve.Growth.XpToNext(l)) + h.Xp;
    }

    static bool Same(OwnedHero a, OwnedHero b) => a.Id == b.Id && a.Hero == b.Hero && a.Level == b.Level && a.Xp == b.Xp && a.Stars == b.Stars && a.Skill == b.Skill;

    static bool Throws(Action a)
    {
        try { a(); return false; }
        catch (ArgumentException) { return true; }
    }

    static PlayerProgress Progress(string folder, out List<string> warnings)
    {
        var w = warnings = new List<string>();
        return new PlayerProgress(data, curve, new SaveStore(folder), w.Add);
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
