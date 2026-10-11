// How a summon works (doc 08 section 2), read from the "summon" and "roster" sections of progression.json
// (Unity/Assets/Resources/BattleData): the cost, the rate of each rarity and the guarantees, whose
// counters carry over from one summon to the next. The game, the progression simulator and later the
// server all call this, so the rates players get are the ones we simulated.
// Seeded: the same System.Random seed and counters always give the same summons. A seeded
// System.Random runs the same algorithm in .NET and in Unity.
// In Unity, pass the TextAsset's text to Summon.FromJson.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    public enum SummonPayment { None, Ticket, Godshards }

    // The guarantee counters: summons since at least each guaranteed rarity, and summons in total.
    // They belong to the player and carry over between summons and sessions.
    public class SummonPity
    {
        public Dictionary<string, int> Since = new Dictionary<string, int>();
        public int Pulls;
    }

    public class Summon
    {
        public double Cost;                                                     // Godshards per summon; a ticket pays for one instead
        public List<string> Rarities = new List<string>();                      // rarest first
        public Dictionary<string, double> Rates = new Dictionary<string, double>();
        public List<(string Rarity, int Every)> Guarantees = new List<(string, int)>();   // at least this rarity once every N summons
        public bool FirstTenEpic;                                               // the 10th summon is Epic or better if the player owns none

        public static Summon FromJson(string progression)
        {
            var root = Obj(Json.Parse(progression));
            var su = Obj(root["summon"]);
            var s = new Summon
            {
                Cost = (double)su["cost"],
                Rarities = ((List<object>)Obj(root["roster"])["rarities"]).Cast<string>().ToList(),
                Rates = Obj(su["rates"]).ToDictionary(kv => kv.Key, kv => (double)kv.Value),
                FirstTenEpic = su.TryGetValue("firstTenGuaranteesEpic", out var f) && f is bool b && b,
            };
            foreach (var g in (List<object>)su["guarantees"]) s.Guarantees.Add(((string)Obj(g)["rarity"], (int)(double)Obj(g)["every"]));
            return s;
        }

        static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;

        public int Rank(string rarity) => Rarities.IndexOf(rarity);   // 0 = rarest

        // Counters for a new player: nothing summoned yet.
        public SummonPity NewPity()
        {
            var p = new SummonPity();
            foreach (var g in Guarantees) p.Since[g.Rarity] = 0;
            return p;
        }

        // What pays for the next summon: a ticket first, then Godshards.
        public SummonPayment HowToPay(double tickets, double godshards) =>
            tickets >= 1 ? SummonPayment.Ticket : godshards >= Cost ? SummonPayment.Godshards : SummonPayment.None;

        // One summon, already paid for: rolls a rarity, lifts it to the rarest guarantee that's due, updates
        // the counters, then picks a hero of that rarity from the banner's pool.
        // ownsEpicOrBetter is only asked on the 10th summon, for the first-ten rule.
        public T Pull<T>(SummonPity pity, Random rng, Func<bool> ownsEpicOrBetter, Func<string, IList<T>> pool)
        {
            pity.Pulls++;
            foreach (var g in Guarantees) pity.Since[g.Rarity]++;
            double roll = rng.NextDouble(), acc = 0;
            int rank = Rarities.Count - 1;
            for (int i = 0; i < Rarities.Count; i++) { acc += Rates[Rarities[i]]; if (roll < acc) { rank = i; break; } }
            foreach (var g in Guarantees) if (pity.Since[g.Rarity] >= g.Every) rank = Math.Min(rank, Rank(g.Rarity));
            if (FirstTenEpic && pity.Pulls == 10 && !ownsEpicOrBetter()) rank = Math.Min(rank, Rank("Epic"));
            foreach (var g in Guarantees) if (rank <= Rank(g.Rarity)) pity.Since[g.Rarity] = 0;
            var heroes = pool(Rarities[rank]);
            return heroes[rng.Next(heroes.Count)];
        }
    }
}
