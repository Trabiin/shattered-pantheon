// What the campaign gives (doc 06 section 1.4, doc 08 section 4, doc 10 section 4), read from the
// "rewards" section of progression.json (Unity/Assets/Resources/BattleData): a battle's first win (with
// the stage clear and its Vessel chance on a stage boss), replays, challenge stars, the stage chests at
// 6, 12 and 20 stars, and the star track's chest every 100 stars on a difficulty. The game hands these out and the
// progression simulator plays a year with them, so both give the same rewards.
// Rewards counted in level-ups ("xpLevels", "goldLevels") are worth that many level-ups at the
// recommended level of the player's current campaign battle: callers pass its place (diff, k) as
// CampaignCurve.Position gives it. A first win is always on the current battle, since battles open in order.
// In Unity, pass the TextAsset's text to CampaignRewards.FromJson.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    // One reward as progression.json lists it: XP and gold in level-ups, the rest as counts. Vessels map a
    // star rank to an expected count (0.3 = a 30% chance of one).
    public class Reward
    {
        public double XpLevels, GoldLevels, Godshards, Devotion, GearMats, SkillTomes, SummonTickets, HeroShards;
        public Dictionary<int, double> Vessels = new Dictionary<int, double>();

        // Takes a reward object from Json.Parse.
        public static Reward FromJson(object o)
        {
            var d = (Dictionary<string, object>)o;
            double N(string k) => d.TryGetValue(k, out var v) && v is double x ? x : 0;
            return new Reward
            {
                XpLevels = N("xpLevels"), GoldLevels = N("goldLevels"), Godshards = N("godshards"), Devotion = N("devotion"),
                GearMats = N("gearMats"), SkillTomes = N("skillTomes"), SummonTickets = N("summonTickets"), HeroShards = N("heroShards"),
                Vessels = d.TryGetValue("vessels", out var v) ? Ranks(v) : new Dictionary<int, double>(),
            };
        }

        internal static Dictionary<int, double> Ranks(object o) => ((Dictionary<string, object>)o).ToDictionary(kv => int.Parse(kv.Key), kv => (double)kv.Value);
    }

    // What rewards are worth at one place in the campaign: XP and gold as amounts, the rest as counts.
    // Amounts are expected values and can be fractions until Whole rolls them.
    public class Loot
    {
        public double Xp, Gold, Godshards, Devotion, GearMats, SkillTomes, SummonTickets, HeroShards;
        public Dictionary<int, double> Vessels = new Dictionary<int, double>();   // star rank -> count

        public Loot Add(Loot o)
        {
            Xp += o.Xp; Gold += o.Gold; Godshards += o.Godshards; Devotion += o.Devotion; GearMats += o.GearMats;
            SkillTomes += o.SkillTomes; SummonTickets += o.SummonTickets; HeroShards += o.HeroShards;
            foreach (var v in o.Vessels) Vessels[v.Key] = (Vessels.TryGetValue(v.Key, out var n) ? n : 0) + v.Value;
            return this;
        }

        public Loot Times(double n) => new Loot
        {
            Xp = Xp * n, Gold = Gold * n, Godshards = Godshards * n, Devotion = Devotion * n, GearMats = GearMats * n,
            SkillTomes = SkillTomes * n, SummonTickets = SummonTickets * n, HeroShards = HeroShards * n,
            Vessels = Vessels.ToDictionary(v => v.Key, v => v.Value * n),
        };

        // Whole amounts to hand out: XP and gold rounded, and for everything else the fraction of a count
        // is the chance of one more (2.4 gear materials: 2, and a 40% chance of a third). Seeded, so the
        // same Random gives the same reward.
        public Loot Whole(Random rng)
        {
            double Roll(double x) { double whole = Math.Floor(x); return whole + (rng.NextDouble() < x - whole ? 1 : 0); }
            return new Loot
            {
                Xp = Math.Round(Xp), Gold = Math.Round(Gold), Godshards = Roll(Godshards), Devotion = Roll(Devotion), GearMats = Roll(GearMats),
                SkillTomes = Roll(SkillTomes), SummonTickets = Roll(SummonTickets), HeroShards = Roll(HeroShards),
                Vessels = Vessels.OrderBy(v => v.Key).ToDictionary(v => v.Key, v => Roll(v.Value)),
            };
        }
    }

    public class CampaignRewards
    {
        public CampaignCurve Curve;
        public Reward FirstClear, StageClear, Farm, Star, StarChest;
        public List<Dictionary<int, double>> StageClearVessels;   // by difficulty (0 = Normal)
        public List<Reward> FarmByDifficulty;                     // a replay's extra drops, by the replayed battle's difficulty
        public int[] StageChestStars;                             // a stage's stars that open each of its chests
        public List<Reward> StageChests;
        public int StarChestEvery;
        // What a replay costs. Not charged yet: Devotion arrives with E10-F3-S1.
        public double ReplayDevotion;

        public static CampaignRewards FromJson(string progression)
        {
            var re = Obj(Obj(Json.Parse(progression))["rewards"]);
            var chests = Obj(re["stageChests"]);
            var r = new CampaignRewards
            {
                Curve = CampaignCurve.FromJson(progression),
                FirstClear = Reward.FromJson(re["firstClear"]), StageClear = Reward.FromJson(re["stageClear"]),
                Farm = Reward.FromJson(re["farm"]), Star = Reward.FromJson(re["star"]), StarChest = Reward.FromJson(re["starChest"]),
                StageClearVessels = ((List<object>)re["stageClearVessels"]).Select(Reward.Ranks).ToList(),
                FarmByDifficulty = ((List<object>)re["farmByDifficulty"]).Select(Reward.FromJson).ToList(),
                StageChestStars = ((List<object>)chests["stars"]).Select(s => (int)(double)s).ToArray(),
                StageChests = ((List<object>)chests["rewards"]).Select(Reward.FromJson).ToList(),
                StarChestEvery = (int)(double)re["starChestEvery"],
            };
            // The farm entry's Devotion is what a replay costs, not something it gives.
            r.ReplayDevotion = r.Farm.Devotion;
            r.Farm.Devotion = 0;
            return r;
        }

        static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;

        // A reward's worth while the player's current campaign battle is battle k of difficulty diff.
        // Scaled rewards follow the difficulty's reward scale (XP and gold through the level-up's cost, gear
        // materials and skill tomes directly); unscaled ones are tables that already list an amount per difficulty.
        public Loot Value(Reward r, int diff, int k, bool scaled = true)
        {
            double levelUp = scaled ? Curve.LevelCost(diff, k) : Curve.Growth.XpToNext(Math.Max(1, Curve.RecLevel(diff, k)));
            double scale = scaled ? Curve.RewardScale(diff, k) : 1;
            return new Loot
            {
                Xp = r.XpLevels * levelUp, Gold = r.GoldLevels * levelUp * Curve.Growth.GoldPerXp,
                Godshards = r.Godshards, Devotion = r.Devotion, GearMats = r.GearMats * scale, SkillTomes = r.SkillTomes * scale,
                SummonTickets = r.SummonTickets, HeroShards = r.HeroShards, Vessels = new Dictionary<int, double>(r.Vessels),
            };
        }

        public bool IsStageBoss(int k) => k % Curve.BattlesPerStage == Curve.BattlesPerStage - 1;

        // The first win on battle k of difficulty diff. A stage boss (battle 4) adds the stage clear and its
        // chance of a Vessel (doc 08 section 4).
        public Loot FirstWin(int diff, int k)
        {
            var loot = Value(FirstClear, diff, k);
            if (IsStageBoss(k)) loot.Add(Value(StageClear, diff, k)).Add(new Loot { Vessels = new Dictionary<int, double>(StageClearVessels[diff]) });
            return loot;
        }

        // The battle XP the heroes who fought share on a first win.
        public long FirstClearXp(StageDef battle)
        {
            var (diff, k) = Curve.Position(battle);
            return (long)Math.Round(FirstWin(diff, k).Xp);
        }

        // A won replay of a cleared battle on difficulty replayDiff, while the player's current battle is
        // (diff, k): the farm reward, plus the replayed difficulty's own drops (doc 08 section 4).
        public Loot Replay(int replayDiff, int diff, int k) => Value(Farm, diff, k).Add(Value(FarmByDifficulty[replayDiff], diff, k, false));

        // Challenge stars (2 to 5) newly earned; the win's own star gives nothing extra.
        public Loot Stars(int count, int diff, int k) => Value(Star, diff, k).Times(count);

        // How many of a stage's chests its stars have opened.
        public int StageChestsAt(int stageStars) => StageChestStars.Count(s => stageStars >= s);
        public Loot StageChest(int chest, int diff, int k) => Value(StageChests[chest], diff, k);

        // How many star track chests the player's stars on one difficulty have opened.
        public int StarChestsAt(int difficultyStars) => difficultyStars / StarChestEvery;
        public Loot StarTrackChest(int diff, int k) => Value(StarChest, diff, k);
    }
}
