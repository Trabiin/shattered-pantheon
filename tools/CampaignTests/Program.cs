// Tests for the order campaign battles open in (CampaignOrder), on the real campaign data: a new game,
// battle after battle, a stage boss, a realm boss, realm 10's boss opening the next difficulty,
// replaying cleared battles, and what's open after the game is closed and reopened with its save.
//   dotnet run --project tools/CampaignTests
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
    static CampaignOrder order;

    static int Main()
    {
        string dir = Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData");
        string R(string f) => File.ReadAllText(Path.Combine(dir, f + ".json"));
        data = GameData.FromJson(R("rules"), R("heroes"), R("enemies"), R("campaign"));
        order = new CampaignOrder(data.Stages);

        Test("The order covers the whole campaign", WholeOrder);
        Test("A new game opens only the first battle of realm 1", NewGame);
        Test("Winning a battle opens the next one", NextBattle);
        Test("Beating a stage boss opens the next stage", StageBoss);
        Test("Beating a realm boss opens the next realm", RealmBoss);
        Test("Beating realm 10's boss opens the next difficulty", NextDifficulty);
        Test("Cleared battles can always be replayed", Replay);
        Test("Clearing the campaign in order opens one battle at a time", WalkTheCampaign);
        Test("Battles outside the campaign are never open", OutsideCampaign);
        Test("A loss or a timeout opens nothing", Losses);
        Test("What's open comes back after reopening the game, and isn't saved", ReloadedSave);

        Console.WriteLine(failures == 0 ? "All campaign order checks passed." : $"{failures} campaign order check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static void WholeOrder(string folder)
    {
        var expected = new List<(string, int, int, int)>();
        foreach (var d in CampaignOrder.Difficulties)
            for (int realm = 1; realm <= 10; realm++)
                for (int stage = 1; stage <= 10; stage++)
                    for (int battle = 1; battle <= 4; battle++) expected.Add((d, realm, stage, battle));
        var actual = order.Battles.Select(b => (b.Difficulty, b.RealmNumber, b.StageNumber, b.BattleNumber)).ToList();
        Expect(actual.SequenceEqual(expected), $"Normal, Hard, Nightmare, Godless; realm 1 to 10; stage 1 to 10; battle 1 to 4 ({actual.Count} battles)");
        Expect(order.Battles.All(b => (b.BattleNumber == 4) == (b.Boss != null)), "battle 4 of every stage is its boss");
        Expect(order.Battles.Where(b => b.Boss == "realm").All(b => b.StageNumber == 10), "the realm boss is stage 10's boss");
        Expect(Enumerable.Range(0, order.Battles.Count).All(i => order.IndexOf(order.Battles[i]) == i), "each battle knows its place");
        Expect(order.Before(order.Battles[0]) == null, "nothing comes before the first battle");
    }

    static void NewGame(string folder)
    {
        var open = order.Battles.Where(b => order.IsOpen(b, Cleared())).ToList();
        Expect(open.Count == 1, $"exactly one battle is open ({open.Count})");
        var first = open.FirstOrDefault();
        Expect(first != null && first.Difficulty == "normal" && first.RealmNumber == 1 && first.StageNumber == 1 && first.BattleNumber == 1, "it's Normal, realm 1, stage 1, battle 1");
    }

    static void NextBattle(string folder)
    {
        var b1 = Find("normal", 1, 1, 1); var b2 = Find("normal", 1, 1, 2); var b3 = Find("normal", 1, 1, 3);
        var won = Cleared(b1);
        Expect(order.IsOpen(b2, won), "battle 2 opens when battle 1 is won");
        Expect(!order.IsOpen(b3, won), "battle 3 stays locked");
        won = Cleared(b1, b2);
        Expect(order.IsOpen(b3, won), "battle 3 opens when battle 2 is won");
    }

    static void StageBoss(string folder)
    {
        var boss = Find("normal", 1, 1, 4);
        var nextStage = Find("normal", 1, 2, 1);
        Expect(boss.Boss == "stage", "battle 4 is the stage boss");
        Expect(order.Before(nextStage) == boss, "the next stage's first battle comes after the stage boss");
        var upToBoss = Cleared(Find("normal", 1, 1, 1), Find("normal", 1, 1, 2), Find("normal", 1, 1, 3));
        Expect(order.IsOpen(boss, upToBoss) && !order.IsOpen(nextStage, upToBoss), "with the boss not yet beaten, the next stage is locked");
        var beaten = Cleared(Find("normal", 1, 1, 1), Find("normal", 1, 1, 2), Find("normal", 1, 1, 3), boss);
        Expect(order.IsOpen(nextStage, beaten), "beating the stage boss opens stage 2, battle 1");
        Expect(!order.IsOpen(Find("normal", 1, 2, 2), beaten), "stage 2, battle 2 stays locked");
    }

    static void RealmBoss(string folder)
    {
        var boss = Find("normal", 1, 10, 4);
        var nextRealm = Find("normal", 2, 1, 1);
        Expect(boss.Boss == "realm", "stage 10's boss is the realm boss");
        var realm1 = order.Battles.Where(b => b.Difficulty == "normal" && b.RealmNumber == 1).ToList();
        var allButBoss = Cleared(realm1.Where(b => b != boss).ToArray());
        Expect(order.IsOpen(boss, allButBoss) && !order.IsOpen(nextRealm, allButBoss), "with the realm boss not yet beaten, realm 2 is locked");
        var beaten = Cleared(realm1.ToArray());
        Expect(order.IsOpen(nextRealm, beaten), "beating the realm boss opens realm 2, stage 1, battle 1");
        Expect(order.Battles.Count(b => order.IsOpen(b, beaten) && !beaten(b.Id)) == 1, "and nothing else new");
    }

    static void NextDifficulty(string folder)
    {
        for (int d = 1; d < CampaignOrder.Difficulties.Length; d++)
        {
            string previous = CampaignOrder.Difficulties[d - 1], next = CampaignOrder.Difficulties[d];
            var boss = Find(previous, 10, 10, 4);
            var first = Find(next, 1, 1, 1);
            var before = order.Battles.Where(b => order.IndexOf(b) < order.IndexOf(boss)).ToArray();
            Expect(!order.IsOpen(first, Cleared(before)), $"{next} is locked until {previous} realm 10's boss is beaten");
            Expect(order.IsOpen(first, Cleared(before.Append(boss).ToArray())), $"beating {previous} realm 10's boss opens {next} realm 1, stage 1, battle 1");
        }
        var last = order.Battles.Last();
        Expect(last.Difficulty == "godless" && last.RealmNumber == 10 && last.Boss == "realm", "Godless realm 10's boss is the last battle");
    }

    static void Replay(string folder)
    {
        var played = order.Battles.Take(9).ToArray();
        var won = Cleared(played);
        Expect(played.All(b => order.IsOpen(b, won)), "every cleared battle is still open");
        // A save from before battles opened in order (E7-F2-S1 let any battle be played) can have gaps.
        var gap = Find("normal", 1, 5, 3);
        var odd = Cleared(gap);
        Expect(order.IsOpen(gap, odd), "a cleared battle is open even if the one before it isn't cleared");
        Expect(order.IsOpen(Find("normal", 1, 5, 4), odd), "and so is the battle after it");
        Expect(!order.IsOpen(Find("normal", 1, 5, 2), odd), "but not the uncleared battle before it");
    }

    static void WalkTheCampaign(string folder)
    {
        var cleared = new HashSet<string>();
        bool oneAtATime = true;
        int opened = 0;
        foreach (var battle in order.Battles)
        {
            // Before this battle is won, it's the only open battle not yet cleared.
            var openNotCleared = order.Battles.Where(b => !cleared.Contains(b.Id) && order.IsOpen(b, cleared.Contains)).ToList();
            if (openNotCleared.Count != 1 || openNotCleared[0] != battle) { oneAtATime = false; break; }
            cleared.Add(battle.Id);
            opened++;
        }
        Expect(oneAtATime && opened == order.Battles.Count, $"each win opens exactly the next battle, all {order.Battles.Count} of them ({opened})");
        Expect(order.Battles.All(b => order.IsOpen(b, cleared.Contains)), "with everything cleared, everything is open");
    }

    static void OutsideCampaign(string folder)
    {
        string dir = Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData");
        var testStages = GameData.LoadDirectory(dir).Stages;
        Expect(testStages.All(s => order.IndexOf(s) < 0 && !order.IsOpen(s, id => true)), "the balance simulator's test stages are never open");
        Expect(new CampaignOrder(data.Stages.Concat(testStages)).Battles.Count == order.Battles.Count, "test stages mixed in are left out of the order");
    }

    static void Losses(string folder)
    {
        var progress = new PlayerProgress(data, new SaveStore(folder), _ => { });
        var b1 = Find("normal", 1, 1, 1); var b2 = Find("normal", 1, 1, 2);
        progress.RecordBattle(b1, "lose");
        progress.RecordBattle(b1, "timeout");
        Expect(progress.IsOpen(b1) && !progress.IsOpen(b2), "battle 2 stays locked after a loss and a timeout on battle 1");
        progress.RecordBattle(b1, "win");
        Expect(progress.IsOpen(b2), "and opens with the win");
    }

    static void ReloadedSave(string folder)
    {
        var progress = new PlayerProgress(data, new SaveStore(folder), _ => { });
        var played = order.Battles.Where(b => b.Difficulty == "normal" && (b.RealmNumber == 1 || (b.RealmNumber == 2 && b.StageNumber == 1 && b.BattleNumber <= 2))).ToList();
        foreach (var b in played) progress.RecordBattle(b, "win");
        var openBefore = order.Battles.Where(progress.IsOpen).Select(b => b.Id).ToList();

        var warnings = new List<string>();
        var reopened = new PlayerProgress(data, new SaveStore(folder), warnings.Add);
        var openAfter = order.Battles.Where(reopened.IsOpen).Select(b => b.Id).ToList();
        Expect(warnings.Count == 0, "the save loads without warnings");
        Expect(openAfter.SequenceEqual(openBefore), $"the same {openAfter.Count} battles are open after reopening");
        Expect(reopened.IsOpen(Find("normal", 2, 1, 3)) && !reopened.IsOpen(Find("normal", 2, 1, 4)), "realm 2, stage 1: battle 3 is next and battle 4 is locked");
        Expect(played.All(b => reopened.IsOpen(b)), "every battle won before closing can be replayed");

        string json = File.ReadAllText(Path.Combine(folder, SaveStore.FileName));
        var root = (Dictionary<string, object>)Json.Parse(json);
        var campaign = (Dictionary<string, object>)root["campaign"];
        Expect(root.Keys.OrderBy(k => k).SequenceEqual(new[] { "campaign", "teams", "version" }) && campaign.Keys.OrderBy(k => k).SequenceEqual(new[] { "furthest", "stars" }), "the save holds cleared battles only; what's open isn't stored");
    }

    static StageDef Find(string difficulty, int realm, int stage, int battle) =>
        order.Battles.Single(b => b.Difficulty == difficulty && b.RealmNumber == realm && b.StageNumber == stage && b.BattleNumber == battle);

    static Func<string, bool> Cleared(params StageDef[] battles)
    {
        var ids = new HashSet<string>(battles.Select(b => b.Id));
        return ids.Contains;
    }

    static void Test(string name, Action<string> test)
    {
        Console.WriteLine(name);
        string folder = Path.Combine(Path.GetTempPath(), "sp-campaign-" + Guid.NewGuid().ToString("N"));
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
