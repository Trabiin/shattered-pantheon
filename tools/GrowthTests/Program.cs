// Tests for the hero growth and cost rules (Growth) and the campaign's recommended power and battle XP
// (CampaignCurve), on the real progression.json: each formula against values worked out by hand from
// doc 07 sections 1 and 2, the generated campaign against the curve, and a changed progression.json
// changing the result.
//   dotnet run --project tools/GrowthTests
using System;
using System.IO;
using System.Linq;
using ShatteredPantheon.Battle;

static class Program
{
    static int failures;
    static string json;
    static Growth growth;
    static CampaignCurve curve;

    static int Main()
    {
        json = File.ReadAllText(Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData", "progression.json"));
        growth = Growth.FromJson(json);
        curve = CampaignCurve.FromJson(json);

        Test("Health and attack multipliers", StatScale);
        Test("Starting star rank by rarity, and the highest rank", Stars);
        Test("Level cap for each star rank", LevelCaps);
        Test("XP to the next level", LevelCosts);
        Test("Fodder to ascend", Fodder);
        Test("Skill tomes to the next skill level", Tomes);
        Test("Gear materials and gold to the next tier", GearCosts);
        Test("Recommended power through the campaign", Recommended);
        Test("Rewards ease into each difficulty", RewardScale);
        Test("Battle XP of a first win", FirstClearXp);
        Test("The generated campaign follows the curve", CampaignMatches);
        Test("A change in progression.json changes the rules", FollowsTheFile);

        Console.WriteLine(failures == 0 ? "All growth checks passed." : $"{failures} growth check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    // Rarity x (1 + 3% per level above 1) x (1 + 10% per star above 3) x (1 + 5% per gear tier) x (1 + 2% per skill level above 1).
    static void StatScale()
    {
        Close(growth.StatScale("Epic", 1, 3, 0, 1), 1.0, "Epic, level 1, 3 stars, no gear, skill 1: x1");
        Close(growth.StatScale("Common", 1, 1, 0, 1), 0.75 * 0.8, "Common at its start (1 star): 0.75 x 0.8 = 0.6");
        Close(growth.StatScale("Uncommon", 1, 2, 0, 1), 0.80 * 0.9, "Uncommon at its start (2 stars): 0.8 x 0.9 = 0.72");
        Close(growth.StatScale("Rare", 1, 3, 0, 1), 0.85, "Rare at its start (3 stars): 0.85");
        Close(growth.StatScale("Epic", 1, 4, 0, 1), 1.1, "Epic at its start (4 stars): 1.1");
        Close(growth.StatScale("Legendary", 1, 5, 0, 1), 1.15 * 1.2, "Legendary at its start (5 stars): 1.15 x 1.2 = 1.38");
        Close(growth.StatScale("Rare", 60, 3, 0, 1), 0.85 * 2.77, "Rare at level 60: 0.85 x 2.77 = 2.3545");
        Close(growth.StatScale("Epic", 10, 4, 2, 3), 1.27 * 1.1 * 1.1 * 1.04, "Epic, level 10, 4 stars, gear 2, skill 3: 1.27 x 1.1 x 1.1 x 1.04 = 1.598168");
        Close(growth.StatScale("Legendary", 200, 6, 12, 10), 1.15 * 6.97 * 1.3 * 1.6 * 1.18, "Legendary maxed (level 200, 6 stars, gear 12, skill 10): 19.673");
        Close(growth.StatScale("Epic", 50.5, 4.5, 3, 2.5), 2.485 * 1.15 * 1.15 * 1.03, "fractional steps for recommended power: level 50.5, 4.5 stars, gear 3, skill 2.5");
    }

    static void Stars()
    {
        string[] rarities = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
        for (int i = 0; i < rarities.Length; i++) Expect(growth.StartStars(rarities[i]) == i + 1, $"{rarities[i]} starts at {i + 1} star(s)");
        Expect(growth.MaxStars == 6, "every hero can reach 6 stars");
    }

    static void LevelCaps()
    {
        int[] caps = { 30, 45, 60, 110, 170, 200 };
        for (int s = 1; s <= 6; s++) Expect(growth.LevelCap(s) == caps[s - 1], $"{s} star(s): level {caps[s - 1]} ({growth.LevelCap(s)})");
    }

    // XP = 30 x level^1.6, rounded; levels cost no gold.
    static void LevelCosts()
    {
        (int level, double xp)[] cases = { (1, 30), (2, 91), (10, 1194), (59, 20440), (100, 47547), (199, 142984) };
        foreach (var (level, xp) in cases)
        {
            Expect(growth.XpToNext(level) == xp, $"level {level} to {level + 1}: {xp} XP ({growth.XpToNext(level)})");
        }
    }

    static void Fodder()
    {
        int[] fodder = { 1, 1, 2, 3, 4 };
        for (int s = 1; s <= 5; s++) Expect(growth.FodderToAscend(s) == fodder[s - 1], $"{s} to {s + 1} stars: {fodder[s - 1]} fodder ({growth.FodderToAscend(s)})");
    }

    // 5 tomes x the current skill level.
    static void Tomes()
    {
        Expect(growth.TomesToNext(1) == 5, "skill 1 to 2: 5 tomes");
        Expect(growth.TomesToNext(5) == 25, "skill 5 to 6: 25 tomes");
        Expect(growth.TomesToNext(9) == 45, "skill 9 to 10: 45 tomes");
        Expect(growth.MaxSkill == 10, "skills go up to level 10");
    }

    // 5 materials x the next tier; gold is a quarter of the XP of the hero's next level, at half a gold per XP.
    static void GearCosts()
    {
        Expect(growth.GearMatsToNext(0) == 5, "tier 0 to 1: 5 materials");
        Expect(growth.GearMatsToNext(11) == 60, "tier 11 to 12: 60 materials");
        Expect(growth.GearGoldToNext(10) == 0.25 * 1194 * 0.5, "a level 10 hero pays 149.25 gold");
        Expect(growth.GearGoldToNext(0) == growth.GearGoldToNext(1), "a level below 1 pays as level 1");
        Expect(growth.MaxGear == 12, "gear goes up to tier 12");
    }

    // Doc 07 section 2: Normal goes from level 1 to 100 at 4 stars, gear 0 to 6 and skill 1 to 5, then Hard from level 100.
    static void Recommended()
    {
        Expect(curve.Difficulties.Select(d => d.Name).SequenceEqual(new[] { "Normal", "Hard", "Nightmare", "Godless" }) && curve.BattlesPerDifficulty == 400, "4 difficulties of 400 battles");
        Expect(curve.Recommended(0, 0) == (1, 4, 0, 1), "Normal's first battle: level 1, 4 stars, no gear, skill 1");
        Expect(curve.Recommended(0, 399) == (100, 4, 6, 5), "Normal's last battle: level 100, 4 stars, gear 6, skill 5");
        Expect(curve.Recommended(1, 0) == (100, 4, 6, 5) && curve.Recommended(3, 399) == (200, 6, 12, 10), "Hard starts where Normal ends; Godless ends at level 200, 6 stars, gear 12, skill 10");
        Close(curve.Recommended(0, 199).level, 1 + 99 * Math.Pow(199.0 / 399, 0.85), "Normal's 200th battle: level 1 + 99 x (199/399)^0.85 = 55.81 (the curve climbs faster early)");
        Expect(curve.RecLevel(0, 199) == 56, "rounded to level 56");
        Close(curve.RecScale(0, 0), 1.1, "the first battle expects a new Epic hero: x1.1");
        Close(curve.RecScale(1, 0), 3.97 * 1.1 * 1.3 * 1.08, "Hard's first battle: level 100 (x3.97), 4 stars (x1.1), gear 6 (x1.3), skill 5 (x1.08)");
    }

    // Hard gives 0.2 of Normal's rewards, easing in over its first 10% (40 battles).
    static void RewardScale()
    {
        Close(curve.RewardScale(0, 0), 1, "Normal: full rewards");
        Close(curve.RewardScale(1, 0), 1, "Hard's first battle still pays as Normal");
        Close(curve.RewardScale(1, 20), 0.6, "halfway through the easing: 0.6");
        Close(curve.RewardScale(1, 40), 0.2, "after 40 battles: Hard's 0.2");
        Close(curve.RewardScale(2, 399), 0.1, "Nightmare: 0.1");
        Close(curve.LevelCost(1, 40), growth.XpToNext(curve.RecLevel(1, 40)) * 0.2, "a level-up at a battle is that battle's next level's XP times the reward scale");
    }

    // 0.8 level-ups at the battle's recommended level.
    static void FirstClearXp()
    {
        var data = Campaign();
        StageDef Battle(string id) => data.Stages.First(s => s.Id == id);
        Expect(curve.FirstClearXpLevels == 0.8, "a first clear is worth 0.8 level-ups of XP");
        Expect(curve.FirstClearXp(Battle("c0-0")) == 24, "Normal's first battle: 0.8 x 30 = 24 XP");
        Expect(curve.FirstClearXp(Battle("c1-0")) == 38038, "Hard's first battle: 0.8 x 47547 (level 100) = 38038 XP");
        Expect(curve.FirstClearXp(Battle("c0-39")) > curve.FirstClearXp(Battle("c0-38")), "later battles give more");
    }

    static void CampaignMatches()
    {
        var data = Campaign();
        var battles = data.Stages.Where(s => s.RealmNumber > 0).ToList();
        Expect(battles.Count == 1600, "1600 campaign battles");
        Expect(battles.All(b => $"c{curve.Position(b).diff}-{curve.Position(b).k}" == b.Id), "each battle's difficulty, realm, stage and number give its place in the campaign");
        Expect(battles.All(b => Math.Abs(curve.RecScale(curve.Position(b).diff, curve.Position(b).k) - b.HeroScale) < 0.0005), "each battle's heroScale in campaign.json is the curve's recommended strength");
    }

    static GameData Campaign()
    {
        string R(string f) => File.ReadAllText(Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData", f + ".json"));
        return GameData.FromJson(R("rules"), R("heroes"), R("enemies"), R("campaign"));
    }

    static void FollowsTheFile()
    {
        var changed = Growth.FromJson(json.Replace("\"levelGain\": 0.03", "\"levelGain\": 0.04").Replace("\"perLevel\": 5", "\"perLevel\": 7"));
        Expect(json.Contains("\"levelGain\": 0.03") && json.Contains("\"perLevel\": 5"), "progression.json has the values this check changes");
        Close(changed.StatScale("Epic", 11, 3, 0, 1), 1.4, "levelGain 0.04: level 11 gives x1.4");
        Expect(changed.TomesToNext(2) == 14, "7 tomes per level: skill 2 to 3 costs 14");
        var richer = CampaignCurve.FromJson(json.Replace("\"xpLevels\": 0.8,", "\"xpLevels\": 1.6,"));
        Expect(richer.FirstClearXpLevels == 1.6 && richer.LevelCost(0, 0) * 1.6 == 48, "first-clear xpLevels 1.6: Normal's first battle gives 48 XP");
    }

    static void Test(string name, Action test)
    {
        Console.WriteLine(name);
        try { test(); }
        catch (Exception e) { Expect(false, $"threw {e.GetType().Name}: {e.Message}"); }
    }

    static void Close(double actual, double expected, string what) => Expect(Math.Abs(actual - expected) < 1e-9, $"{what} ({actual:0.######})");

    static void Expect(bool ok, string what) { if (!ok) failures++; Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); }

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "Unity", "Assets"))) return d.FullName;
        throw new DirectoryNotFoundException("Run from inside the repository");
    }
}
