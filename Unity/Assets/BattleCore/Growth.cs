// How heroes grow and what growing costs (doc 07 section 1), read from the "heroGrowth" and "costs"
// sections of progression.json (Unity/Assets/Resources/BattleData). The game and the progression
// simulator both call these, so a balance change in progression.json changes both.
// In Unity, pass the TextAsset's text to Growth.FromJson.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    public class Growth
    {
        // Stat growth: rarity's multiplier, then a gain per level, per star above 3, per gear tier and per skill level.
        public double LevelGain, StarGain, GearGain, SkillGain;
        public Dictionary<string, double> RarityBase = new Dictionary<string, double>();
        public Dictionary<string, int> RarityStars = new Dictionary<string, int>();   // the star rank a hero of each rarity starts at
        public Dictionary<int, int> LevelCapByStars = new Dictionary<int, int>();
        public int MaxGear, MaxSkill;
        // Costs
        public double XpA, XpExp, GoldPerXp, GearMatsPerTier, GearGoldLevels, TomesPerLevel;
        public Dictionary<int, int> AscendFodder = new Dictionary<int, int>();         // star rank -> fodder to reach the next

        public static Growth FromJson(string progression)
        {
            var root = Obj(Json.Parse(progression));
            var g = Obj(root["heroGrowth"]);
            var co = Obj(root["costs"]);
            return new Growth
            {
                LevelGain = Num(g, "levelGain"), StarGain = Num(g, "starGain"), GearGain = Num(g, "gearGain"), SkillGain = Num(g, "skillGain"),
                RarityBase = Obj(g["rarityBase"]).ToDictionary(kv => kv.Key, kv => (double)kv.Value),
                RarityStars = Obj(g["rarityStars"]).ToDictionary(kv => kv.Key, kv => (int)(double)kv.Value),
                LevelCapByStars = Ranks(g["levelCapByStars"]),
                MaxGear = (int)Num(g, "maxGear"), MaxSkill = (int)Num(g, "maxSkill"),
                XpA = Num(Obj(co["xpToNext"]), "a"), XpExp = Num(Obj(co["xpToNext"]), "exp"),
                GoldPerXp = Num(co, "goldPerXp"), GearMatsPerTier = Num(Obj(co["gearMats"]), "perTier"),
                GearGoldLevels = Num(co, "gearGoldLevels"), TomesPerLevel = Num(Obj(co["skillTomes"]), "perLevel"),
                AscendFodder = Ranks(co["ascendFodder"]),
            };
        }

        static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        static double Num(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is double x ? x : 0;
        static Dictionary<int, int> Ranks(object o) => Obj(o).ToDictionary(kv => int.Parse(kv.Key), kv => (int)(double)kv.Value);

        // What a hero's listed health and attack are multiplied by. Defence doesn't grow.
        // Level, stars and skill are fractional for the campaign's recommended power between whole steps.
        public double StatScale(string rarity, double level, double stars, double gear, double skill) =>
            RarityBase[rarity] * (1 + LevelGain * (level - 1)) * (1 + StarGain * (stars - 3)) * (1 + GearGain * gear) * (1 + SkillGain * (skill - 1));

        public int StartStars(string rarity) => RarityStars[rarity];
        public int MaxStars => LevelCapByStars.Keys.Max();

        // The highest level a hero of this star rank can reach.
        public int LevelCap(int stars) => LevelCapByStars.TryGetValue(stars, out var c) ? c : LevelCapByStars.Values.Max();

        // XP and gold to go from this level to the next.
        public double XpToNext(int level) => Math.Round(XpA * Math.Pow(level, XpExp));
        public double GoldToNext(int level) => XpToNext(level) * GoldPerXp;

        // Heroes of the same star rank (or Vessels) used up to ascend from this star rank to the next.
        public int FodderToAscend(int stars) => AscendFodder[stars];

        // Skill tomes to go from this skill level to the next.
        public double TomesToNext(int skill) => TomesPerLevel * skill;

        // Gear materials and gold to go from this gear tier to the next; the gold follows the hero's level cost.
        public double GearMatsToNext(int gear) => GearMatsPerTier * (gear + 1);
        public double GearGoldToNext(int level) => GearGoldLevels * XpToNext(Math.Max(1, level)) * GoldPerXp;
    }
}
