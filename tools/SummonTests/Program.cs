// Tests for the summon rules (Summon), on the real progression.json: the cost and rates of doc 08
// section 2, a million summons checked against those rates and the three guarantees, counters that
// carry over, the first-ten rule, and the same seed always giving the same summons.
//   dotnet run --project tools/SummonTests -c Release
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShatteredPantheon.Battle;

static class Program
{
    const int Million = 1_000_000;
    static readonly string[] Rarities = { "Legendary", "Epic", "Rare", "Uncommon", "Common" };
    static int failures;
    static string json;
    static Summon summon;

    static int Main()
    {
        json = File.ReadAllText(Path.Combine(FindRoot(), "Unity", "Assets", "Resources", "BattleData", "progression.json"));
        summon = Summon.FromJson(json);

        Test("Cost, rates and guarantees from progression.json", Rules);
        Test("Paying: a ticket first, then 300 Godshards", Paying);
        Test("A million summons without guarantees roll the doc's rates", RawRates);
        Test("A million summons never miss a guarantee", Guarantees);
        Test("The counters carry over between sessions", CarryOver);
        Test("The 10th summon is Epic or better for a player with none", FirstTen);
        Test("The same seed gives the same summons", SameSeed);
        Test("A change in progression.json changes the rules", FollowsTheFile);

        Console.WriteLine(failures == 0 ? "All summon checks passed." : $"{failures} summon check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    // Doc 08 section 2: 0.8 / 6 / 14 / 30 / 49.2%, Rare or better every 10, Epic or better every 40, Legendary every 160.
    static void Rules()
    {
        Expect(summon.Cost == 300, $"a summon costs 300 Godshards ({summon.Cost})");
        Expect(summon.Rarities.SequenceEqual(Rarities), "rarities rarest first: " + string.Join(", ", summon.Rarities));
        double[] rates = { 0.008, 0.06, 0.14, 0.30, 0.492 };
        for (int i = 0; i < Rarities.Length; i++) Close(summon.Rates[Rarities[i]], rates[i], $"{Rarities[i]} {rates[i] * 100:0.#}%");
        Close(summon.Rates.Values.Sum(), 1, "the rates add up to 100%");
        Expect(summon.Guarantees.SequenceEqual(new[] { ("Rare", 10), ("Epic", 40), ("Legendary", 160) }),
            "guarantees: " + string.Join(", ", summon.Guarantees.Select(g => $"{g.Rarity}{(g.Rarity == "Legendary" ? "" : " or better")} every {g.Every}")));
        Expect(summon.FirstTenEpic, "the first ten summons hold an Epic or better");
        var pity = summon.NewPity();
        Expect(pity.Pulls == 0 && pity.Since.Count == 3 && pity.Since.Values.All(v => v == 0), "a new player's counters start at 0");
    }

    static void Paying()
    {
        Expect(summon.HowToPay(1, 1000) == SummonPayment.Ticket, "a ticket is used before Godshards");
        Expect(summon.HowToPay(0, 300) == SummonPayment.Godshards, "300 Godshards pay for a summon");
        Expect(summon.HowToPay(0, 299) == SummonPayment.None, "299 Godshards and no ticket can't summon");
        Expect(summon.HowToPay(0.5, 0) == SummonPayment.None, "half a ticket can't summon");
    }

    // With the guarantees switched off, what comes out is the rates alone: each within 5 standard errors.
    static void RawRates()
    {
        var raw = Summon.FromJson(json);
        raw.Guarantees.Clear(); raw.FirstTenEpic = false;
        var counts = Count(raw, raw.NewPity(), new Random(1), Million);
        foreach (var r in Rarities)
        {
            double p = raw.Rates[r], got = (double)counts[r] / Million, se = Math.Sqrt(p * (1 - p) / Million);
            Expect(Math.Abs(got - p) < 5 * se, $"{r}: {got * 100:0.000}% (doc {p * 100:0.#}%)");
        }
    }

    // With the guarantees on: the gap between two summons of at least each guaranteed rarity is never
    // longer than its guarantee, and the Legendary rate is the one its guarantee works out to exactly
    // (only the Legendary guarantee can give a Legendary): 1 / sum over k < 160 of 0.992^k = 1.106%.
    // The Epic and Rare guarantees also lift each other a little, so those rates are checked against
    // their own guarantee's value as a floor.
    static void Guarantees()
    {
        var pity = summon.NewPity();
        var rng = new Random(2);
        var since = summon.Guarantees.ToDictionary(g => g.Rarity, g => 0);
        var longest = summon.Guarantees.ToDictionary(g => g.Rarity, g => 0);
        var atLeast = summon.Guarantees.ToDictionary(g => g.Rarity, g => 0);
        for (int i = 0; i < Million; i++)
        {
            var r = summon.Pull(pity, rng, () => true, x => new[] { x });
            foreach (var g in summon.Guarantees)
            {
                since[g.Rarity]++;
                longest[g.Rarity] = Math.Max(longest[g.Rarity], since[g.Rarity]);
                if (summon.Rank(r) <= summon.Rank(g.Rarity)) { since[g.Rarity] = 0; atLeast[g.Rarity]++; }
                if (since[g.Rarity] != pity.Since[g.Rarity]) { Expect(false, $"summon {i + 1}: the {g.Rarity} counter is {pity.Since[g.Rarity]}, should be {since[g.Rarity]}"); return; }
            }
        }
        foreach (var g in summon.Guarantees)
            Expect(longest[g.Rarity] <= g.Every, $"{g.Rarity}{(g.Rarity == "Legendary" ? "" : " or better")}: never more than {g.Every} summons apart (longest {longest[g.Rarity]})");
        Expect(longest["Legendary"] == 160 && longest["Epic"] == 40 && longest["Rare"] == 10, "each guarantee was needed at least once");

        double Due(string rarity, int every) { double q = 1 - Rarities.TakeWhile(x => x != rarity).Append(rarity).Sum(x => summon.Rates[x]); return 1 / Enumerable.Range(0, every).Sum(k => Math.Pow(q, k)); }
        double legendary = Due("Legendary", 160), got = (double)atLeast["Legendary"] / Million;
        Expect(Math.Abs(got - legendary) < 5 * Math.Sqrt(legendary * (1 - legendary) / Million), $"Legendary: {got * 100:0.000}% (worked out {legendary * 100:0.000}%)");
        foreach (var (rarity, every) in new[] { ("Epic", 40), ("Rare", 10) })
        {
            double floor = Due(rarity, every), share = (double)atLeast[rarity] / Million;
            Expect(share >= floor - 5 * Math.Sqrt(floor * (1 - floor) / Million) && share < floor + 0.005, $"{rarity} or better: {share * 100:0.000}% (its own guarantee alone gives {floor * 100:0.000}%)");
        }
    }

    // Summoning 500 times in one go gives the same heroes as 250, saving the counters, and 250 more.
    static void CarryOver()
    {
        var all = Run(summon.NewPity(), new Random(3), 500);
        var rng = new Random(3);
        var first = summon.NewPity();
        var a = Run(first, rng, 250);
        var reopened = new SummonPity { Pulls = first.Pulls, Since = new Dictionary<string, int>(first.Since) };
        var b = Run(reopened, rng, 250);
        Expect(all.SequenceEqual(a.Concat(b)), "500 summons in one go = 250, then 250 more with the saved counters");

        var near = summon.NewPity();
        near.Since["Legendary"] = 159; near.Pulls = 1000;
        Expect(summon.Pull(near, new Random(4), () => true, x => new[] { x }) == "Legendary", "a counter at 159 makes the next summon Legendary");
        Expect(near.Since.Values.All(v => v == 0), "a Legendary sets every counter back to 0");
    }

    static void FirstTen()
    {
        int asked = 0, below = 0;
        for (int seed = 0; seed < 2000; seed++)
        {
            var pity = summon.NewPity();
            var rng = new Random(seed);
            var tenth = Run(pity, rng, 10, () => { asked++; return false; }).Last();
            if (summon.Rank(tenth) > summon.Rank("Epic")) { Expect(false, $"seed {seed}: the 10th summon was {tenth}"); return; }
            if (summon.Rank(Run(summon.NewPity(), new Random(seed), 10, () => true).Last()) > summon.Rank("Epic")) below++;
        }
        Expect(true, "2,000 new players: every 10th summon was Epic or better");
        Expect(asked == 2000, $"whether the player owns an Epic is only asked on the 10th summon ({asked} times for 2,000 players)");
        Expect(below > 0, $"a player who already owns an Epic gets no lift ({below} of 2,000 10th summons were below Epic)");
    }

    static void SameSeed()
    {
        Expect(Run(summon.NewPity(), new Random(5), 10000).SequenceEqual(Run(summon.NewPity(), new Random(5), 10000)), "seed 5 twice: the same 10,000 summons");
        Expect(!Run(summon.NewPity(), new Random(5), 100).SequenceEqual(Run(summon.NewPity(), new Random(6), 100)), "seeds 5 and 6 give different summons");
        var pool = new[] { "a", "b", "c" };
        Expect(Enumerable.Range(0, 100).Select(_ => summon.Pull(summon.NewPity(), new Random(7), () => true, x => pool)).Distinct().Count() == 1, "the hero picked from the pool follows the seed too");
    }

    static void FollowsTheFile()
    {
        Expect(json.Contains("\"cost\": 300") && json.Contains("\"every\": 160"), "progression.json has the values this check changes");
        var changed = Summon.FromJson(json.Replace("\"cost\": 300", "\"cost\": 250").Replace("\"every\": 160", "\"every\": 100"));
        Expect(changed.Cost == 250, "cost 250: a summon costs 250 Godshards");
        Expect(changed.Guarantees.Contains(("Legendary", 100)), "every 100: the Legendary guarantee follows");
    }

    static Dictionary<string, int> Count(Summon s, SummonPity pity, Random rng, int n)
    {
        var counts = Rarities.ToDictionary(r => r, r => 0);
        for (int i = 0; i < n; i++) counts[s.Pull(pity, rng, () => true, x => new[] { x })]++;
        return counts;
    }

    static List<string> Run(SummonPity pity, Random rng, int n, Func<bool> ownsEpic = null) =>
        Enumerable.Range(0, n).Select(_ => summon.Pull(pity, rng, ownsEpic ?? (() => true), x => new[] { x })).ToList();

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
