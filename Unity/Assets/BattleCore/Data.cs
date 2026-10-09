// Game data: rules, heroes, enemies and stages, read from the JSON files in
// Unity/Assets/Resources/BattleData. In Unity, pass the TextAsset texts to GameData.FromJson.
// The format is described in tools/BattleData/README.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    // Named numbers ("atkPct": 0.12). Missing keys read as 0.
    public class Mods : Dictionary<string, double>
    {
        public double Get(string k) => TryGetValue(k, out var v) ? v : 0;
        public void Add(Mods other) { if (other != null) foreach (var kv in other) this[kv.Key] = Get(kv.Key) + kv.Value; }
    }

    public class Rules
    {
        public double SecondsPerActionAt1x = 1;
        public int MaxActions = 900;
        public double Gauge = 1000, StartJitter = 0.15;
        public double ManaMax = 100, ManaRegen = 20, ManaOnHit = 5, ManaPerPctLost = 0.5;
        public double HitBase = 0.95, HitMin = 0.6, HitMax = 1;
        public double CritChance = 0.1, CritDamage = 1.5, Dodge = 0.05;
        public Dictionary<string, double> DamageRange = new Dictionary<string, double> { ["normal"] = 0.1, ["steady"] = 0.03, ["wild"] = 0.25 };
        public double DefenceK = 100;
        public double EffectMin = 0.15, EffectMax = 0.95;
        public double TypeStrong = 0.3, TypeResisted = 0.3;
        public double FactionAccuracy = 0.1, FactionCrit = 0.15, FactionEffect = 0.15;
        public Dictionary<string, string> FactionWheel = new Dictionary<string, string>();
        public Dictionary<string, List<string>> TypeStrongAgainst = new Dictionary<string, List<string>>();
        // Faction doctrines and type bonuses: name -> tier (2, 3, 4) -> mods.
        public Dictionary<string, SortedDictionary<int, Mods>> Doctrines = new Dictionary<string, SortedDictionary<int, Mods>>();
        public Dictionary<string, SortedDictionary<int, Mods>> TypeBonuses = new Dictionary<string, SortedDictionary<int, Mods>>();
        public Dictionary<string, double[]> DifficultyTargetsSeconds = new Dictionary<string, double[]>();
        public Dictionary<string, double> ReliableWinShareMax = new Dictionary<string, double>();
    }

    // One step of a skill: pick targets, then apply an effect. Numbers left out are 0.
    public class ActionDef
    {
        public string Target, Effect, Status, Kind, Role, Range;
        public int N, Turns, Stacks;
        public bool Distinct;
        public double Mult, Value, Pct, Chance, Pen, Lifesteal, Execute, BonusVsExposed, Falloff, RepeatOnKill;
        public double CasterMaxHp, TargetMaxHp;
        public string BonusVs; public double Bonus;
    }

    public class SkillDef
    {
        public string Name, Kind, Range;
        public double Cd, StartCd;
        public List<ActionDef> Actions = new List<ActionDef>();
    }

    public class PassiveDef
    {
        public string Name, Trigger;
        public double Every, Cooldown, HpBelow, Chance;
        public bool Once;
        public Mods Stats = new Mods();   // added to the unit's stats at battle start
        public Mods Mods = new Mods();    // always-on rule changes (see README)
        public List<ActionDef> Actions = new List<ActionDef>();
    }

    public class UnitDef
    {
        public string Id, Name, Faction, Type, Role, Rarity, Kind;
        public Mods Stats = new Mods();   // hp, atk, armour, spirit, spd and any secondary stat
        public bool Boss, Unyielding;
        public List<string> Immune = new List<string>();
        public SkillDef Basic, Ult;
        public List<SkillDef> Skills = new List<SkillDef>();
        public List<PassiveDef> Passives = new List<PassiveDef>();
        public string Description;
    }

    public class StageEnemy
    {
        public string Id, Slot;
    }

    public class StageDef
    {
        public string Id, Name, Difficulty, Formation = "2-3", Notes;
        // Campaign position (doc 06 section 1.1); empty for test stages. Boss is "stage", "realm" or null.
        public string Realm, Boss;
        public int RealmNumber, StageNumber, BattleNumber;
        public double HpScale = 1, AtkScale = 1;
        // How strong the campaign expects the player's heroes to be here (times their listed stats);
        // the battle is sim-checked against heroes at this strength. 1 for test stages.
        public double HeroScale = 1;
        public List<StageEnemy> Enemies = new List<StageEnemy>();
    }

    public class GameData
    {
        public Rules Rules;
        public List<UnitDef> Heroes, Enemies;
        public List<StageDef> Stages;

        public UnitDef Hero(string id) => Heroes.FirstOrDefault(h => h.Id == id);
        public UnitDef Enemy(string id) => Enemies.FirstOrDefault(h => h.Id == id);

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
        static List<object> Arr(object o) => o == null ? new List<object>() : (List<object>)o;
        static double Num(Dictionary<string, object> d, string k, double def = 0) => d.TryGetValue(k, out var v) && v is double x ? x : def;
        static string Str(Dictionary<string, object> d, string k, string def = null) => d.TryGetValue(k, out var v) && v is string s ? s : def;
        static bool Bool(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is bool b && b;
        static List<string> Strs(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? Arr(v).Cast<string>().ToList() : new List<string>();

        static Mods ReadMods(Dictionary<string, object> d, string k)
        {
            var m = new Mods();
            if (d.TryGetValue(k, out var v) && v is Dictionary<string, object> o)
                foreach (var kv in o) m[kv.Key] = (double)kv.Value;
            return m;
        }

        static Dictionary<string, SortedDictionary<int, Mods>> ReadTiers(Dictionary<string, object> d, string k)
        {
            var res = new Dictionary<string, SortedDictionary<int, Mods>>();
            if (!d.TryGetValue(k, out var v)) return res;
            foreach (var kv in Obj(v))
            {
                var tiers = new SortedDictionary<int, Mods>();
                foreach (var t in Obj(kv.Value)) tiers[int.Parse(t.Key)] = ReadMods(Obj(kv.Value), t.Key);
                res[kv.Key] = tiers;
            }
            return res;
        }

        static Rules ReadRules(Dictionary<string, object> d)
        {
            var r = new Rules
            {
                SecondsPerActionAt1x = Num(d, "secondsPerActionAt1x", 1),
                MaxActions = (int)Num(d, "maxActions", 900),
                Doctrines = ReadTiers(d, "doctrines"),
                TypeBonuses = ReadTiers(d, "typeBonuses"),
            };
            void Group(string k, Action<Dictionary<string, object>> read) { if (d.TryGetValue(k, out var v)) read(Obj(v)); }
            Group("turn", o => { r.Gauge = Num(o, "gauge", r.Gauge); r.StartJitter = Num(o, "startJitter", r.StartJitter); });
            Group("mana", o => { r.ManaMax = Num(o, "max", r.ManaMax); r.ManaRegen = Num(o, "regen", r.ManaRegen); r.ManaOnHit = Num(o, "onHit", r.ManaOnHit); r.ManaPerPctLost = Num(o, "perPctHealthLost", r.ManaPerPctLost); });
            Group("hit", o => { r.HitBase = Num(o, "base", r.HitBase); r.HitMin = Num(o, "min", r.HitMin); r.HitMax = Num(o, "max", r.HitMax); });
            Group("crit", o => { r.CritChance = Num(o, "chance", r.CritChance); r.CritDamage = Num(o, "damage", r.CritDamage); });
            Group("damageRange", o => { foreach (var kv in o) r.DamageRange[kv.Key] = (double)kv.Value; });
            Group("effectChance", o => { r.EffectMin = Num(o, "min", r.EffectMin); r.EffectMax = Num(o, "max", r.EffectMax); });
            Group("type", o => { r.TypeStrong = Num(o, "strong", r.TypeStrong); r.TypeResisted = Num(o, "resisted", r.TypeResisted); });
            Group("factionAdvantage", o => { r.FactionAccuracy = Num(o, "accuracy"); r.FactionCrit = Num(o, "crit"); r.FactionEffect = Num(o, "effect"); });
            r.Dodge = Num(d, "dodge", r.Dodge);
            r.DefenceK = Num(d, "defenceK", r.DefenceK);
            Group("factionWheel", o => { foreach (var kv in o) r.FactionWheel[kv.Key] = (string)kv.Value; });
            Group("typeChart", o => { foreach (var kv in o) r.TypeStrongAgainst[kv.Key] = Strs(Obj(kv.Value), "strong"); });
            Group("difficultyTargetsSeconds", o => { foreach (var kv in o) r.DifficultyTargetsSeconds[kv.Key] = Arr(kv.Value).Select(x => (double)x).ToArray(); });
            Group("reliableWinShareMax", o => { foreach (var kv in o) r.ReliableWinShareMax[kv.Key] = (double)kv.Value; });
            return r;
        }

        static SkillDef ReadSkill(object v)
        {
            if (v == null) return null;
            var s = Obj(v);
            return new SkillDef
            {
                Name = Str(s, "name"),
                Kind = Str(s, "kind"),
                Range = Str(s, "range"),
                Cd = Num(s, "cd"),
                StartCd = Num(s, "startCd"),
                Actions = Arr(s.TryGetValue("actions", out var a) ? a : null).Select(x => ReadAction(Obj(x))).ToList(),
            };
        }

        static ActionDef ReadAction(Dictionary<string, object> a) => new ActionDef
        {
            Target = Str(a, "target"), Effect = Str(a, "effect"), Status = Str(a, "status"),
            Kind = Str(a, "kind"), Role = Str(a, "role"), Range = Str(a, "range"),
            N = (int)Num(a, "n"), Turns = (int)Num(a, "turns"), Stacks = (int)Num(a, "stacks"),
            Distinct = Bool(a, "distinct"),
            Mult = Num(a, "mult"), Value = Num(a, "value"), Pct = Num(a, "pct"), Chance = Num(a, "chance"),
            Pen = Num(a, "pen"), Lifesteal = Num(a, "lifesteal"), Execute = Num(a, "execute"),
            BonusVsExposed = Num(a, "bonusVsExposed"), Falloff = Num(a, "falloff", 0.2), RepeatOnKill = Num(a, "repeatOnKill"),
            CasterMaxHp = Num(a, "casterMaxHp"), TargetMaxHp = Num(a, "targetMaxHp"),
            BonusVs = Str(a, "bonusVs"), Bonus = Num(a, "bonus"),
        };

        static PassiveDef ReadPassive(Dictionary<string, object> p) => new PassiveDef
        {
            Name = Str(p, "name"),
            Trigger = Str(p, "trigger", "always"),
            Every = Num(p, "every"), Cooldown = Num(p, "cooldown"), HpBelow = Num(p, "hpBelow"), Chance = Num(p, "chance"),
            Once = Bool(p, "once"),
            Stats = ReadMods(p, "stats"),
            Mods = ReadMods(p, "mods"),
            Actions = Arr(p.TryGetValue("actions", out var a) ? a : null).Select(x => ReadAction(Obj(x))).ToList(),
        };

        static readonly string[] StatKeys = { "hp", "atk", "armour", "spirit", "spd", "crit", "critDmg", "acc", "dodge", "effect", "tenacity", "manaRegen", "haste", "healPower", "pen" };

        static UnitDef ReadUnit(Dictionary<string, object> d)
        {
            var u = new UnitDef
            {
                Id = Str(d, "id"), Name = Str(d, "name"), Faction = Str(d, "faction"), Type = Str(d, "type"),
                Role = Str(d, "role"), Rarity = Str(d, "rarity"), Kind = Str(d, "kind", "phys"),
                Description = Str(d, "description"),
                Boss = Bool(d, "boss"), Unyielding = Bool(d, "unyielding") || Bool(d, "boss"),
                Immune = Strs(d, "immune"),
                Basic = ReadSkill(d.TryGetValue("basic", out var b) ? b : null),
                Ult = ReadSkill(d.TryGetValue("ult", out var ul) ? ul : null),
                Skills = Arr(d.TryGetValue("skills", out var sk) ? sk : null).Select(ReadSkill).ToList(),
                Passives = Arr(d.TryGetValue("passives", out var ps) ? ps : null).Select(x => ReadPassive(Obj(x))).ToList(),
            };
            foreach (var k in StatKeys) if (d.TryGetValue(k, out var v) && v is double x) u.Stats[k] = x;
            if (u.Basic == null)
            {
                bool ranged = u.Role == "Ranger" || u.Role == "Caster" || u.Role == "Support";
                u.Basic = new SkillDef { Name = "Attack", Actions = { new ActionDef { Target = ranged ? "front" : "opposite", Effect = "damage", Mult = 1, Falloff = 0.2 } } };
            }
            return u;
        }

        // Enemies are [id, slot] pairs. A slot of just "front" or "back" takes the next free spot in that row.
        static StageDef ReadStage(Dictionary<string, object> d)
        {
            double S(string k) { double v = Num(d, k); return v == 0 ? 1 : v; }
            var st = new StageDef
            {
                Id = Str(d, "id"), Name = Str(d, "name"), Difficulty = Str(d, "difficulty"),
                Formation = Str(d, "formation", "2-3"), Notes = Str(d, "notes"),
                Realm = Str(d, "realm"), Boss = Str(d, "boss"),
                RealmNumber = (int)Num(d, "realmNumber"), StageNumber = (int)Num(d, "stage"), BattleNumber = (int)Num(d, "battle"),
                HpScale = S("hpScale"), AtkScale = S("atkScale"), HeroScale = S("heroScale"),
            };
            var next = new Dictionary<string, int> { ["front"] = 0, ["back"] = 0 };
            foreach (var e in Arr(d["enemies"]))
            {
                var p = Arr(e);
                string slot = (string)p[1];
                if (slot == "front" || slot == "back") slot += next[slot]++;
                st.Enemies.Add(new StageEnemy { Id = (string)p[0], Slot = slot });
            }
            return st;
        }
    }
}
