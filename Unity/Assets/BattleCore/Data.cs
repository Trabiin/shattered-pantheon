// Game data: rules, heroes, enemies and stages, read from the same JSON files the simulator uses
// (Unity/Assets/Resources/BattleData/*.json). In Unity, pass the TextAsset texts to GameData.FromJson.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    public class TypeMatch
    {
        public List<string> Strong = new List<string>();
        public List<string> ResistedBy = new List<string>();
    }

    public class Rules
    {
        public double SecondsPerActionAt1x;
        public int MaxActions;
        public double CritChance, CritMultiplier, DamageSpread, EnergyPerAction, EnergyWhenHit;
        public double FactionAdvantage, TypeStrong, TypeResisted;
        public double MatchupMin, MatchupMax;
        public Dictionary<string, string> FactionWheel = new Dictionary<string, string>();
        public Dictionary<string, TypeMatch> TypeChart = new Dictionary<string, TypeMatch>();
        public Dictionary<string, double[]> DifficultyTargetsSeconds = new Dictionary<string, double[]>();
        public Dictionary<string, double> ReliableWinShareMax = new Dictionary<string, double>();
    }

    // One step of a skill: pick targets, then apply an effect. Zero means "not set".
    public class ActionDef
    {
        public string Target, Effect, Status;
        public double Mult, Pierce, Lifesteal, Execute, Value, CasterMaxHp, AtkScale, HealMaxHp;
        public int Turns;
        public bool Cleanse;
    }

    public class SkillDef
    {
        public string Name;
        public int Cd;
        public List<ActionDef> Actions = new List<ActionDef>();
    }

    public class UnitDef
    {
        public string Id, Name, Faction, Type, Role;
        public double Hp, Atk, Def, Spd;
        public bool Backline, Boss;
        public SkillDef Skill, Ult;
    }

    public class StageEnemy
    {
        public string Id, Row;
    }

    public class StageDef
    {
        public string Id, Name, Difficulty;
        public double StatScale = 1, HpScale = 1, AtkScale = 1;
        public List<StageEnemy> Enemies = new List<StageEnemy>();
    }

    public class GameData
    {
        public Rules Rules;
        public List<UnitDef> Heroes, Enemies;
        public List<StageDef> Stages;

        public static GameData LoadDirectory(string dir)
        {
            string R(string f) => File.ReadAllText(Path.Combine(dir, f));
            return FromJson(R("rules.json"), R("heroes.json"), R("enemies.json"), R("stages.json"));
        }

        public static GameData FromJson(string rules, string heroes, string enemies, string stages)
        {
            return new GameData
            {
                Rules = ReadRules(Obj(Json.Parse(rules))),
                Heroes = Arr(Json.Parse(heroes)).Select(x => ReadUnit(Obj(x))).ToList(),
                Enemies = Arr(Json.Parse(enemies)).Select(x => ReadUnit(Obj(x))).ToList(),
                Stages = Arr(Json.Parse(stages)).Select(x => ReadStage(Obj(x))).ToList(),
            };
        }

        static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        static List<object> Arr(object o) => (List<object>)o;
        static double Num(Dictionary<string, object> d, string k, double def = 0) => d.TryGetValue(k, out var v) && v != null ? (double)v : def;
        static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? (string)v : null;
        static bool Bool(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is bool b && b;
        static List<string> Strs(object o) => Arr(o).Cast<string>().ToList();

        static Rules ReadRules(Dictionary<string, object> d)
        {
            var clamp = Arr(d["matchupClamp"]);
            var r = new Rules
            {
                SecondsPerActionAt1x = Num(d, "secondsPerActionAt1x"),
                MaxActions = (int)Num(d, "maxActions"),
                CritChance = Num(d, "critChance"),
                CritMultiplier = Num(d, "critMultiplier"),
                DamageSpread = Num(d, "damageSpread"),
                EnergyPerAction = Num(d, "energyPerAction"),
                EnergyWhenHit = Num(d, "energyWhenHit"),
                FactionAdvantage = Num(d, "factionAdvantage"),
                TypeStrong = Num(d, "typeStrong"),
                TypeResisted = Num(d, "typeResisted"),
                MatchupMin = (double)clamp[0],
                MatchupMax = (double)clamp[1],
            };
            foreach (var kv in Obj(d["factionWheel"])) r.FactionWheel[kv.Key] = (string)kv.Value;
            foreach (var kv in Obj(d["typeChart"]))
            {
                var t = Obj(kv.Value);
                r.TypeChart[kv.Key] = new TypeMatch { Strong = Strs(t["strong"]), ResistedBy = Strs(t["resistedBy"]) };
            }
            if (d.TryGetValue("difficultyTargetsSeconds", out var dt))
                foreach (var kv in Obj(dt)) r.DifficultyTargetsSeconds[kv.Key] = Arr(kv.Value).Select(x => (double)x).ToArray();
            if (d.TryGetValue("reliableWinShareMax", out var rw))
                foreach (var kv in Obj(rw)) r.ReliableWinShareMax[kv.Key] = (double)kv.Value;
            return r;
        }

        static SkillDef ReadSkill(Dictionary<string, object> d, string k)
        {
            if (!d.TryGetValue(k, out var v) || v == null) return null;
            var s = Obj(v);
            return new SkillDef
            {
                Name = Str(s, "name"),
                Cd = (int)Num(s, "cd"),
                Actions = Arr(s["actions"]).Select(x => ReadAction(Obj(x))).ToList(),
            };
        }

        static ActionDef ReadAction(Dictionary<string, object> a) => new ActionDef
        {
            Target = Str(a, "target"),
            Effect = Str(a, "effect"),
            Status = Str(a, "status"),
            Mult = Num(a, "mult"),
            Pierce = Num(a, "pierce"),
            Lifesteal = Num(a, "lifesteal"),
            Execute = Num(a, "execute"),
            Value = Num(a, "value"),
            CasterMaxHp = Num(a, "casterMaxHp"),
            AtkScale = Num(a, "atkScale"),
            HealMaxHp = Num(a, "healMaxHp"),
            Turns = (int)Num(a, "turns"),
            Cleanse = Bool(a, "cleanse"),
        };

        static UnitDef ReadUnit(Dictionary<string, object> d) => new UnitDef
        {
            Id = Str(d, "id"),
            Name = Str(d, "name"),
            Faction = Str(d, "faction"),
            Type = Str(d, "type"),
            Role = Str(d, "role"),
            Hp = Num(d, "hp"),
            Atk = Num(d, "atk"),
            Def = Num(d, "def"),
            Spd = Num(d, "spd"),
            Backline = Bool(d, "backline"),
            Boss = Bool(d, "boss"),
            Skill = ReadSkill(d, "skill"),
            Ult = ReadSkill(d, "ult"),
        };

        // Missing or zero scales count as 1, as in the JavaScript engine.
        static StageDef ReadStage(Dictionary<string, object> d)
        {
            double S(string k) { double v = Num(d, k); return v == 0 ? 1 : v; }
            return new StageDef
            {
                Id = Str(d, "id"),
                Name = Str(d, "name"),
                Difficulty = Str(d, "difficulty"),
                StatScale = S("statScale"),
                HpScale = S("hpScale"),
                AtkScale = S("atkScale"),
                Enemies = Arr(d["enemies"]).Select(e => { var p = Arr(e); return new StageEnemy { Id = (string)p[0], Row = (string)p[1] }; }).ToList(),
            };
        }
    }
}
