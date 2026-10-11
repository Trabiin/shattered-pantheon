// What the campaign expects of the player's heroes at each battle (doc 07 section 2) and the battle XP
// a first clear gives, read from the "campaign" and "rewards" sections of progression.json
// (Unity/Assets/Resources/BattleData). The progression simulator builds and plays the campaign with
// these, and the game shares out battle XP with them, so both follow the same numbers.
// In Unity, pass the TextAsset's text to CampaignCurve.FromJson.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    public class CampaignCurve
    {
        public class Difficulty
        {
            public string Name;
            // From the first battle to the last: an Epic hero's recommended level, star rank, gear tier and skill level.
            public double[] Levels, Stars, Gear, Skill;
            // How fast the level climbs (below 1: faster early on), and how much XP, gold and items rewards give here.
            public double Curve = 1, RewardScale = 1;
        }

        public Growth Growth;
        public List<Difficulty> Difficulties = new List<Difficulty>();
        public int Stages, BattlesPerStage, StagesPerRealm;
        // Share of a difficulty's battles over which rewards ease in from the previous difficulty's scale.
        public double RewardEase = 1;
        // A first clear's XP, in level-ups at the battle's recommended level.
        public double FirstClearXpLevels;

        public int BattlesPerDifficulty => Stages * BattlesPerStage;

        public static CampaignCurve FromJson(string progression)
        {
            var root = Obj(Json.Parse(progression));
            var ca = Obj(root["campaign"]);
            return new CampaignCurve
            {
                Growth = Growth.FromJson(progression),
                Difficulties = ((List<object>)ca["difficulties"]).Select(o => Obj(o)).Select(d => new Difficulty
                {
                    Name = (string)d["name"], Levels = Arr(d, "levels"), Stars = Arr(d, "stars"), Gear = Arr(d, "gear"), Skill = Arr(d, "skill"),
                    Curve = Num(d, "curve", 1), RewardScale = Num(d, "rewardScale", 1),
                }).ToList(),
                Stages = (int)Num(ca, "stages"), BattlesPerStage = (int)Num(ca, "battlesPerStage"), StagesPerRealm = (int)Num(ca, "stagesPerRealm"),
                RewardEase = Num(ca, "rewardEase", 1),
                FirstClearXpLevels = Num(Obj(Obj(root["rewards"])["firstClear"]), "xpLevels"),
            };
        }

        static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        static double Num(Dictionary<string, object> d, string k, double def = 0) => d.TryGetValue(k, out var v) && v is double x ? x : def;
        static double[] Arr(Dictionary<string, object> d, string k) => ((List<object>)d[k]).Select(x => (double)x).ToArray();
        static double Lerp(double[] a, double t) => a[0] + (a[1] - a[0]) * t;

        // What an Epic hero should have reached to take on battle k (0 = the first) of difficulty diff (0 = Normal).
        public (double level, double stars, double gear, double skill) Recommended(int diff, int k)
        {
            var d = Difficulties[diff];
            double t = (double)k / (BattlesPerDifficulty - 1);
            return (Lerp(d.Levels, Math.Pow(t, d.Curve)), Lerp(d.Stars, t), Lerp(d.Gear, t), Lerp(d.Skill, t));
        }

        // The recommended strength, as a multiplier of an Epic hero's listed health and attack.
        public double RecScale(int diff, int k) { var r = Recommended(diff, k); return Growth.StatScale("Epic", r.level, r.stars, r.gear, r.skill); }
        public int RecLevel(int diff, int k) => (int)Math.Round(Recommended(diff, k).level);

        // How much XP, gold and items rewards give at this battle. It eases from the previous difficulty's
        // scale to this one's over the difficulty's first battles, so there's no sudden drop.
        public double RewardScale(int diff, int k)
        {
            double from = diff == 0 ? Difficulties[0].RewardScale : Difficulties[diff - 1].RewardScale;
            double t = Math.Min(1, k / (RewardEase * BattlesPerDifficulty));
            return from + (Difficulties[diff].RewardScale - from) * t;
        }

        // What one level-up is worth in XP at this battle: rewards counted in level-ups are multiplied by it.
        public double LevelCost(int diff, int k) => Growth.XpToNext(Math.Max(1, RecLevel(diff, k))) * RewardScale(diff, k);

        // Where a campaign battle sits: its difficulty (0 = Normal) and its place in that difficulty (0 = realm 1, stage 1, battle 1).
        public (int diff, int k) Position(StageDef battle)
        {
            int diff = Difficulties.FindIndex(d => string.Equals(d.Name, battle.Difficulty, StringComparison.OrdinalIgnoreCase));
            if (diff < 0 || battle.RealmNumber < 1) throw new ArgumentException(battle.Id + " isn't a campaign battle");
            return (diff, ((battle.RealmNumber - 1) * StagesPerRealm + battle.StageNumber - 1) * BattlesPerStage + battle.BattleNumber - 1);
        }

        // The battle XP the heroes who fought share when they clear this battle for the first time.
        public long FirstClearXp(StageDef battle)
        {
            var (diff, k) = Position(battle);
            return (long)Math.Round(FirstClearXpLevels * LevelCost(diff, k));
        }
    }
}
