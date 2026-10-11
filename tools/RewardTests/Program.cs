// Tests for the campaign's rewards (CampaignRewards), on the real progression.json: first wins and stage
// clears, replays, challenge stars, stage chests, the star track and rolling whole amounts, against
// values worked out by hand from doc 06 section 1.4, doc 08 section 4 and doc 10 section 4, and a changed
// progression.json changing the result. The game and the progression simulator both hand out these.
//   dotnet run --project tools/RewardTests
using System;
using System.IO;
using System.Linq;
using ShatteredPantheon.Battle;

static class Program
{
    static int failures;
    static string json;
    static CampaignRewards rewards;
    static CampaignCurve curve;

    static int Main()
    {
        json = File.ReadAllText(Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData", "progression.json"));
        rewards = CampaignRewards.FromJson(json);
        curve = rewards.Curve;

        Test("A first win", FirstWin);
        Test("A stage boss adds the stage clear and its Vessel chance", StageClear);
        Test("A replay gives the farm reward and its difficulty's drops", Replay);
        Test("Challenge stars", Stars);
        Test("Stage chests at 6, 12 and 20 stars", StageChests);
        Test("The star track every 100 stars", StarTrack);
        Test("Whole amounts: XP and gold rounded, fractions a chance", Whole);
        Test("A change in progression.json changes the rewards", FollowsTheFile);

        Console.WriteLine(failures == 0 ? "All reward checks passed." : $"{failures} reward check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    // Normal's first battle: a level-up is worth 30 XP (level 1), and a first clear 0.8 of one in XP and gold.
    static void FirstWin()
    {
        var l = rewards.FirstWin(0, 0);
        Close(l.Xp, 24, "Normal's first battle: 0.8 x 30 = 24 XP");
        Close(l.Gold, 12, "and 0.8 x 30 x 0.5 gold per XP = 12 gold");
        Expect(l.Godshards == 5 && l.GearMats == 1 && l.SkillTomes == 0 && l.Vessels.Count == 0, "5 Godshards and a gear material, no Vessel");
        var hard = rewards.FirstWin(1, 0);
        Close(hard.Xp, 0.8 * 47547, "Hard's first battle: 0.8 x 47547 (level 100) XP");
        Close(hard.Gold, 0.8 * 47547 * 0.5, "and gold to match");
        Close(rewards.FirstWin(1, 40).Xp, 0.8 * curve.Growth.XpToNext(curve.RecLevel(1, 40)) * 0.2, "40 battles into Hard, rewards are at Hard's scale of 0.2");
        Expect(rewards.FirstWin(0, 39).Xp > rewards.FirstWin(0, 38).Xp, "later battles give more");
        var campaign = Campaign();
        Expect(rewards.FirstClearXp(campaign.Stages.First(s => s.Id == "c0-0")) == 24 && rewards.FirstClearXp(campaign.Stages.First(s => s.Id == "c1-0")) == 38038, "a battle's first-clear battle XP, rounded: 24 on Normal's first, 38038 on Hard's");
    }

    // Every 4th battle is a stage boss: 20 Godshards, 2.4 gear materials and 2.4 skill tomes, and a Vessel at
    // 40%: 2★ on Normal, 3★ on Hard, 4★ (half the time) on Nightmare, 4★ and a chance of 5★ on Godless.
    static void StageClear()
    {
        Expect(!rewards.IsStageBoss(0) && !rewards.IsStageBoss(2) && rewards.IsStageBoss(3) && rewards.IsStageBoss(399), "battle 4 of each stage is its boss");
        var boss = rewards.FirstWin(0, 3);
        Expect(boss.Godshards == 25, "5 + 20 Godshards");
        Close(boss.GearMats, 3.4, "1 + 2.4 gear materials");
        Close(boss.SkillTomes, 2.4, "2.4 skill tomes");
        Expect(boss.Vessels.Count == 1 && boss.Vessels[2] == 0.4, "a 40% chance of a 2★ Vessel on Normal");
        Expect(rewards.FirstWin(1, 3).Vessels.Single().Key == 3 && rewards.FirstWin(2, 3).Vessels.Single().Key == 4, "3★ on Hard, 4★ on Nightmare");
        var godless = rewards.FirstWin(3, 399).Vessels;
        Expect(godless.Count == 2 && godless[4] == 0.2 && godless[5] == 0.08, "4★ at 20% and 5★ at 8% on Godless");
        Close(rewards.FirstWin(1, 3).GearMats, 3.4 * 0.94, "gear and tomes follow the reward scale: x0.94 three battles into Hard's easing");
        Expect(rewards.FirstWin(0, 2).Godshards == 5, "other battles give no stage clear");
    }

    // Replays: 0.2 level-ups of XP and 0.25 of gold, a gear material, and Normal's farm drops (a 30% 1★
    // Vessel, half a gear material, 0.3 tomes, a 1% hero shard). Valued at the player's current battle.
    static void Replay()
    {
        double lc = curve.Growth.XpToNext(curve.RecLevel(0, 20));
        var r = rewards.Replay(0, 0, 20);
        Close(r.Xp, 0.2 * lc, "0.2 level-ups of XP at the current battle");
        Close(r.Gold, 0.25 * lc * 0.5, "0.25 level-ups of gold");
        Close(r.GearMats, 1.5, "1 + 0.5 gear materials");
        Close(r.SkillTomes, 0.3, "0.3 skill tomes");
        Close(r.HeroShards, 0.01, "a 1% hero shard");
        Expect(r.Godshards == 0 && r.Vessels.Count == 1 && r.Vessels[1] == 0.3, "no Godshards, a 30% chance of a 1★ Vessel");
        Close(rewards.Replay(0, 0, 0).Xp, 0.2 * 30, "replaying battle 1 or battle 21 gives the same at the same current battle");
        var late = rewards.Replay(2, 2, 100);
        double lateLc = curve.Growth.XpToNext(curve.RecLevel(2, 100));
        Close(late.Xp, 0.2 * lateLc * 0.1 + 0.03 * lateLc, "on Nightmare the farm XP is at 0.1, and its drops add 0.03 level-ups at full value");
        Expect(late.Vessels[3] == 0.15 && late.Vessels[4] == 0.06 && late.Vessels[5] == 0.01, "Nightmare's farm drops: 3★ to 5★ Vessels");
        Expect(rewards.ReplayDevotion == 8 && rewards.Farm.Devotion == 0, "a replay costs 8 Devotion and gives none");
    }

    static void Stars()
    {
        Expect(rewards.Stars(1, 0, 0).Godshards == 1 && rewards.Stars(3, 2, 50).Godshards == 3, "a Godshard for each challenge star");
        Expect(rewards.Stars(0, 0, 0).Godshards == 0, "no challenge stars, nothing");
    }

    static void StageChests()
    {
        Expect(rewards.StageChestStars.SequenceEqual(new[] { 6, 12, 20 }), "chests at 6, 12 and 20 of a stage's 20 stars");
        Expect(new[] { 0, 5, 6, 11, 12, 19, 20 }.Select(rewards.StageChestsAt).SequenceEqual(new[] { 0, 0, 1, 1, 2, 2, 3 }), "stars open them in order: 5 none, 6 the first, 12 the second, 20 all three");
        var small = rewards.StageChest(0, 0, 0);
        Expect(small.Xp == 9 && small.Gold == 4.5 && small.Godshards == 0, "the small chest: 0.3 level-ups of XP and gold (9 XP, 4.5 gold at level 1)");
        Expect(rewards.StageChest(1, 0, 0).Godshards == 20, "the medium chest: 20 Godshards");
        var large = rewards.StageChest(2, 0, 0);
        Expect(large.GearMats == 3 && large.SkillTomes == 2, "the large chest: 3 gear materials and 2 skill tomes");
        Close(rewards.StageChest(2, 1, 40).GearMats, 0.6, "on Hard (scale 0.2) the large chest's gear is 0.6");
    }

    static void StarTrack()
    {
        Expect(rewards.StarChestEvery == 100 && rewards.StarChestsAt(99) == 0 && rewards.StarChestsAt(100) == 1 && rewards.StarChestsAt(250) == 2, "a chest every 100 stars on a difficulty: 99 none, 100 one, 250 two");
        var c = rewards.StarTrackChest(0, 0);
        Expect(c.Godshards == 100 && c.GearMats == 10 && c.SummonTickets == 1 && c.Vessels.Count == 1 && c.Vessels[3] == 1, "each holds 100 Godshards, 10 gear materials, a summon ticket and a 3★ Vessel");
    }

    static void Whole()
    {
        var boss = rewards.FirstWin(0, 3);
        var a = boss.Whole(new Random(5));
        var b = boss.Whole(new Random(5));
        Expect(a.GearMats == b.GearMats && a.SkillTomes == b.SkillTomes && a.Vessels[2] == b.Vessels[2], "the same seed gives the same reward");
        Expect(a.Xp == Math.Round(boss.Xp) && a.Gold == Math.Round(boss.Gold) && a.Godshards == 25, "XP and gold are rounded; whole counts stay as they are");
        var rng = new Random(1);
        var rolls = Enumerable.Range(0, 20000).Select(_ => boss.Whole(rng)).ToList();
        Expect(rolls.All(r => r.GearMats == 3 || r.GearMats == 4) && rolls.All(r => r.Vessels[2] == 0 || r.Vessels[2] == 1), "3.4 gear materials is 3 or 4; a 40% Vessel is 0 or 1");
        double mats = rolls.Average(r => r.GearMats), vessels = rolls.Average(r => r.Vessels[2]);
        Expect(Math.Abs(mats - 3.4) < 0.02 && Math.Abs(vessels - 0.4) < 0.02, $"on average they're what progression.json lists ({mats:0.000} gear materials, {vessels:0.000} Vessels)");
    }

    static void FollowsTheFile()
    {
        var changed = CampaignRewards.FromJson(json.Replace("\"godshards\": 5,", "\"godshards\": 7,").Replace("\"starChestEvery\": 100", "\"starChestEvery\": 50"));
        Expect(json.Contains("\"godshards\": 5,") && json.Contains("\"starChestEvery\": 100"), "progression.json has the values this check changes");
        Expect(changed.FirstWin(0, 0).Godshards == 7 && changed.StarChestsAt(100) == 2, "first-clear Godshards 7 and a star chest every 50: both follow");
    }

    static GameData Campaign()
    {
        string R(string f) => File.ReadAllText(Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData", f + ".json"));
        return GameData.FromJson(R("rules"), R("heroes"), R("enemies"), R("campaign"));
    }

    static void Test(string name, Action test)
    {
        Console.WriteLine(name);
        try { test(); }
        catch (Exception e) { Expect(false, $"threw {e.GetType().Name}: {e.Message}"); }
    }

    static void Close(double actual, double expected, string what) => Expect(Math.Abs(actual - expected) < 1e-6, $"{what} ({actual:0.######})");

    static void Expect(bool ok, string what) { if (!ok) failures++; Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); }

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "Unity", "Assets"))) return d.FullName;
        throw new DirectoryNotFoundException("Run from inside the repository");
    }
}
