// Headless battle engine: the rules of a fight, with no graphics. The Unity client and the
// balance simulator both run this code, so simulated results are what players will get.
// The rules are described in the design doc plan/04-battle-system.md (draft 3).
// Seeded: the same teams and seed always give the same fight (checked in CI by `BattleSim --fingerprint`),
// which replays, server checks and bug reports rely on.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    // xorshift32: small, fast and identical on every platform.
    public sealed class Rng
    {
        uint s;
        public Rng(long seed) { s = unchecked((uint)seed); if (s == 0) s = 1; }
        public double Next()
        {
            s ^= s << 13;
            s ^= unchecked((uint)((int)s >> 17));
            s ^= s << 5;
            return s / 4294967296.0;
        }
    }

    public static class Effects
    {
        public static readonly string[] Buffs = { "challenge", "bulwark", "bloodrite", "tailwind", "grace", "ward" };
        public static readonly string[] Debuffs = { "pyre", "blight", "godstruck", "hush", "sunder", "eclipse" };
        public static readonly string[] Control = { "godstruck", "hush" };
        public static bool IsBuff(string t) => Array.IndexOf(Buffs, t) >= 0;
        public static bool IsDebuff(string t) => Array.IndexOf(Debuffs, t) >= 0;
        // Player-facing names (the 12 effects of doc 04 section 5); internal markers have none.
        public static string Label(string t) => IsBuff(t) || IsDebuff(t) ? char.ToUpperInvariant(t[0]) + t.Substring(1) : t == "channel" ? "Ritual" : t == "mark" ? "Marked" : t == "frozen" ? "Frozen" : null;
    }

    public class Status
    {
        public string Type;
        public int Turns, Stacks = 1;
        public double Value, AtkDown;
        public bool Fresh, BlocksGrace;
        public string Source;
    }

    public class UnitStats
    {
        public double Dmg, Heal, Taken;
        public int Misses, Dodges, Crits, Kills;
        public int? DiedAt;
    }

    public class Unit
    {
        public UnitDef Def;
        public string Id, Key, Side, Row, Slot, Faction, Type, Role;
        public int Index;
        public double X;
        public bool Boss, Unyielding, Alive = true;
        public double MaxHp, Hp, Shield, Mana, T;
        public double[] Cd = new double[0];
        public List<Status> St = new List<Status>();
        public UnitStats Stats = new UnitStats();
        public Mods Base = new Mods();   // stats after doctrines, type bonuses and passives
        public Mods Mods = new Mods();   // rule changes from doctrines, type bonuses and passives
        public int TurnsTaken, SkillsUsed, LastTurnAction, FellAt;
        public bool MoonlessReady, MisdirectReady, InterceptReady;
        public Unit LastAttacker;
        public Dictionary<string, int> HitBy = new Dictionary<string, int>();
        public Dictionary<PassiveDef, double> PassiveCd = new Dictionary<PassiveDef, double>();
        public HashSet<PassiveDef> PassiveUsed = new HashSet<PassiveDef>();

        public bool Has(string t) => St.Any(s => s.Type == t);
        public Status Get(string t) => St.FirstOrDefault(s => s.Type == t);
        public double Stat(string k) => Base.Get(k);
        public double Atk => Stat("atk") * (1 + St.Where(s => s.Type == "bloodrite").Sum(s => s.Value * s.Stacks)) * (1 - St.Where(s => s.Type == "blight").Sum(s => s.AtkDown));
        public double Armour => Stat("armour") * (1 - Sunder);
        public double Spirit => Stat("spirit") * (1 - Sunder);
        double Sunder => Math.Min(0.9, St.Where(s => s.Type == "sunder").Sum(s => s.Value * s.Stacks));
        public double Spd => Math.Max(1, Stat("spd") * (1 + St.Where(s => s.Type == "tailwind").Sum(s => s.Value)));
        public int BuffCount => St.Count(s => Effects.IsBuff(s.Type));
        public int DebuffCount => St.Count(s => Effects.IsDebuff(s.Type));
        public double HpRatio => MaxHp > 0 ? Hp / MaxHp : 0;
    }

    public class TeamSlot
    {
        public string Id, Row, Slot;
        // Growth from levels, stars, gear and skills (1 = the hero's listed stats). Health and attack grow
        // together on both sides, and defence doesn't, so fights keep the same length as both sides grow.
        public double HpScale = 1, AtkScale = 1;
        public TeamSlot(string id, string row, string slot) { Id = id; Row = row; Slot = slot; }
        public TeamSlot(string id, string slot) : this(id, slot.StartsWith("front") ? "front" : "back", slot) { }
    }

    public class BattleOptions
    {
        public string Formation;     // "2-3" or "3-2"; worked out from the slots if left out
        public bool Deterministic;   // puzzle fights: no misses, no crits, average damage, effects land at 50%+
    }

    // What happened, in order, for the client to animate. Actor and Target are unit keys.
    public enum EventKind { Turn, Damage, Miss, Heal, Shield, StatusAdded, Resisted, Blocked, Cleansed, Interrupt, Intercept, RitualStart, RitualComplete, DotTick, Mana, Death, Revive, End }

    public class BattleEvent
    {
        public int Action;
        public EventKind Kind;
        public string Actor, Target, Detail;
        public double Amount;
        public bool Crit;
        public override string ToString() => $"{Action} {Kind} {Actor}->{Target} {Detail} {Amount}{(Crit ? " crit" : "")}";
    }

    public class HeroResult
    {
        public string Id;
        public double Dmg, Heal, Taken;
        public int? DiedAt;
    }

    public class BattleResult
    {
        public string Result;
        public int Actions, Rituals;
        public double Seconds;
        public List<HeroResult> Heroes;
    }

    public class Battle
    {
        public Rules Rules;
        public StageDef Stage;
        public List<Unit> Units = new List<Unit>();
        public int Actions;
        public bool Over;
        public string Result;
        public List<string> Log = new List<string>();
        public List<BattleEvent> Events = new List<BattleEvent>();
        public Unit Current;
        public string HeroFormation, EnemyFormation;
        public readonly bool Deterministic;
        readonly Rng rnd;
        readonly Dictionary<string, int> seaTurns = new Dictionary<string, int> { ["A"] = 0, ["E"] = 0 };
        readonly HashSet<string> roseAgain = new HashSet<string>();
        int fallCounter, passiveDepth;

        public Battle(GameData data, IList<TeamSlot> team, string stageId, long seed, BattleOptions options = null)
        {
            Rules = data.Rules;
            Stage = data.Stages.FirstOrDefault(s => s.Id == stageId) ?? throw new ArgumentException("Unknown stage " + stageId);
            Deterministic = options?.Deterministic ?? false;
            rnd = new Rng(seed);
            HeroFormation = options?.Formation ?? FormationOf(team.Select(t => t.Slot));
            EnemyFormation = Stage.Formation ?? FormationOf(Stage.Enemies.Select(e => e.Slot));
            foreach (var m in team)
                Add(data.Hero(m.Id) ?? throw new ArgumentException("Unknown hero " + m.Id), "A", m.Slot, HeroFormation, m.HpScale, m.AtkScale);
            foreach (var se in Stage.Enemies)
                Add(data.Enemy(se.Id) ?? throw new ArgumentException("Unknown enemy " + se.Id), "E", se.Slot, EnemyFormation, Stage.HpScale, Stage.AtkScale);
            foreach (var side in new[] { "A", "E" }) ApplySynergies(side);
            foreach (var side in new[] { "A", "E" }) Shelter(side);
            foreach (var u in Units)
            {
                double jitter = Deterministic ? 0 : (rnd.Next() * 2 - 1) * Rules.StartJitter;
                u.T = Rules.Gauge / u.Spd * (1 + jitter);
            }
            foreach (var u in Units.ToList()) FirePassives(u, "battleStart", null);
        }

        // ---------- Setup ----------

        public static string FormationOf(IEnumerable<string> slots) => slots.Contains("front2") ? "3-2" : "2-3";

        public static IEnumerable<string> SlotsOf(string formation) => formation == "3-2"
            ? new[] { "front0", "front1", "front2", "back0", "back1" }
            : new[] { "front0", "front1", "back0", "back1", "back2" };

        // Position across the field: a row of three stands at -1, 0, 1; a row of two in the gaps at -0.5, 0.5.
        public static double SlotX(string formation, string slot)
        {
            bool front = slot.StartsWith("front");
            int i = int.Parse(slot.Substring(front ? 5 : 4));
            int count = (formation == "3-2") == front ? 3 : 2;
            if (i >= count) throw new ArgumentException($"Slot {slot} doesn't exist in a {formation} formation");
            return count == 3 ? i - 1 : i - 0.5;
        }

        void Add(UnitDef d, string side, string slot, string formation, double hpScale, double atkScale)
        {
            if (Units.Any(x => x.Side == side && x.Slot == slot)) throw new ArgumentException("Two units in slot " + slot);
            var u = new Unit
            {
                Def = d, Id = d.Id, Key = side + ":" + slot, Side = side, Slot = slot,
                Row = slot.StartsWith("front") ? "front" : "back", X = SlotX(formation, slot),
                Faction = d.Faction, Type = d.Type, Role = d.Role, Boss = d.Boss, Unyielding = d.Unyielding,
                Index = Units.Count, Cd = d.Skills.Select(s => s.StartCd).ToArray(),
            };
            u.Base["crit"] = Rules.CritChance;
            u.Base["critDmg"] = Rules.CritDamage;
            u.Base["dodge"] = Rules.Dodge;
            u.Base["manaRegen"] = Rules.ManaRegen;
            u.Base.Add(d.Stats);
            u.Base["hp"] = u.Base.Get("hp") * hpScale;
            u.Base["atk"] = u.Base.Get("atk") * atkScale;
            foreach (var p in d.Passives) { u.Base.Add(p.Stats); u.Mods.Add(p.Mods); }
            Units.Add(u);
        }

        // Faction doctrines (2/4 heroes) and type bonuses (2/3 heroes), then final stats.
        void ApplySynergies(string side)
        {
            var team = Units.Where(u => u.Side == side).ToList();
            foreach (var u in team)
            {
                AddTiers(u, Rules.Doctrines, u.Faction, team.Count(x => x.Faction == u.Faction));
                AddTiers(u, Rules.TypeBonuses, u.Type, team.Count(x => x.Type == u.Type));
                foreach (var k in new[] { "hp", "atk", "armour", "spirit", "spd" })
                    u.Base[k] = u.Base.Get(k) * (1 + u.Base.Get(k + "Pct"));
                u.MaxHp = u.Hp = Math.Round(u.Base.Get("hp"));
                u.Mana = u.Mods.Get("startMana");
                u.MoonlessReady = u.Mods.Get("moonless") > 0;
                u.InterceptReady = u.Mods.Get("intercept") > 0;
            }
        }

        // Hearth doctrine: at battle start every ally gets a shield and Grace.
        void Shelter(string side)
        {
            var h = Units.FirstOrDefault(u => u.Side == side && u.Mods.Get("shelter") > 0);
            if (h == null) return;
            foreach (var a in Units.Where(u => u.Side == side))
            {
                GiveShield(h, a, a.MaxHp * h.Mods.Get("shelter") / (1 + h.Stat("healPower")));
                AddStatus(h, a, new ActionDef { Status = "grace", Turns = 2, TargetMaxHp = 0.04 }, 1);
            }
        }

        static void AddTiers(Unit u, Dictionary<string, SortedDictionary<int, Mods>> table, string key, int count)
        {
            if (key == null || !table.TryGetValue(key, out var tiers)) return;
            foreach (var t in tiers)
                if (count >= t.Key) { u.Base.Add(t.Value); u.Mods.Add(t.Value); }
        }

        // ---------- Match-ups ----------

        // Type advantage changes damage: strong +30%, resisted -30% (doc 09 section 3.1).
        public static double TypeMultiplier(Rules r, string srcType, string tgtType)
        {
            if (srcType == null || tgtType == null) return 1;
            if (r.TypeStrongAgainst.TryGetValue(srcType, out var s) && s.Contains(tgtType)) return 1 + r.TypeStrong;
            if (r.TypeStrongAgainst.TryGetValue(tgtType, out var t) && t.Contains(srcType)) return 1 - r.TypeResisted;
            return 1;
        }

        // Faction advantage changes reliability: +1 if src's god defeated tgt's, -1 the other way round.
        public static int Dominance(Rules r, string srcFaction, string tgtFaction)
        {
            if (srcFaction == null || tgtFaction == null) return 0;
            if (r.FactionWheel.TryGetValue(srcFaction, out var b) && b == tgtFaction) return 1;
            if (r.FactionWheel.TryGetValue(tgtFaction, out var c) && c == srcFaction) return -1;
            return 0;
        }

        // ---------- Helpers ----------

        void Emit(EventKind k, Unit actor, Unit target, double amount = 0, string detail = null, bool crit = false) =>
            Events.Add(new BattleEvent { Action = Actions, Kind = k, Actor = actor?.Key, Target = target?.Key, Amount = amount, Detail = detail, Crit = crit });

        double Roll() => rnd.Next();
        bool Chance(double p) => Deterministic ? p >= 0.5 : Roll() < p;
        List<Unit> Alive(string side) => Units.Where(u => u.Alive && u.Side == side).ToList();
        List<Unit> Foes(Unit u) => Alive(u.Side == "A" ? "E" : "A");
        List<Unit> Allies(Unit u) => Alive(u.Side);
        double Interval(Unit u) => Rules.Gauge / u.Spd;

        // Back units next to a front unit (half a step across the field or less).
        static bool Covers(Unit front, Unit back) => front.Row == "front" && back.Row == "back" && front.Side == back.Side && Math.Abs(front.X - back.X) <= 0.51;
        List<Unit> CoveredBy(Unit f) => Allies(f).Where(b => Covers(f, b)).ToList();
        List<Unit> CoverersOf(Unit b) => Allies(b).Where(f => Covers(f, b)).ToList();
        public bool Exposed(Unit u) => u.Row == "back" && !Units.Any(f => f.Alive && Covers(f, u));
        List<Unit> Reachable(Unit u) => Foes(u).Where(f => f.Row == "front" || Exposed(f)).ToList();

        static Unit Nearest(IEnumerable<Unit> a, double x) => a.OrderBy(t => Math.Abs(t.X - x)).ThenBy(t => t.HpRatio).ThenBy(t => t.X).FirstOrDefault();
        static Unit Lowest(IEnumerable<Unit> a) => a.OrderBy(t => t.HpRatio).ThenBy(t => t.Index).FirstOrDefault();
        static List<Unit> One(Unit u) => u == null ? new List<Unit>() : new List<Unit> { u };
        Unit Taunt(Unit u) => Foes(u).Where(f => f.Has("challenge")).OrderBy(f => f.Index).FirstOrDefault();

        List<Unit> RowNeighbours(Unit t)
        {
            var row = Units.Where(x => x.Alive && x.Side == t.Side && x.Row == t.Row).OrderBy(x => x.X).ToList();
            int i = row.IndexOf(t);
            return row.Where((x, j) => Math.Abs(j - i) == 1).ToList();
        }

        Unit Behind(Unit t) => t.Row != "front" ? null : Nearest(CoveredBy(t), t.X);

        // ---------- Targeting (doc 04 section 2.3) ----------

        List<Unit> Targets(Unit u, ActionDef a, List<Unit> prev, Unit context)
        {
            var foes = Foes(u);
            var taunt = Taunt(u);
            Unit Single(Func<Unit> pick) => taunt ?? pick();
            // Condition patterns with n > 1 take the top n (and ignore taunt).
            List<Unit> Top(Func<Unit> single, Func<IEnumerable<Unit>, IEnumerable<Unit>> order) => a.N > 1 ? order(foes).Take(a.N).ToList() : One(Single(single));
            Unit Opposite() { var r = Reachable(u); return Nearest(r.Count > 0 ? r : foes, u.X); }
            switch (a.Target ?? "opposite")
            {
                // Enemies by position.
                case "opposite": return One(Single(Opposite));
                case "front": return One(Single(() => Lowest(Reachable(u))));
                case "back": return One(Intercept(Single(() => Nearest(foes.Where(f => f.Row == "back"), u.X) ?? Opposite())));
                case "pierce": { var t = Single(Opposite); return One(t).Concat(One(Behind(t))).ToList(); }
                case "splash": { var t = Single(Opposite); return t == null ? new List<Unit>() : One(t).Concat(RowNeighbours(t)).ToList(); }
                case "cross": { var t = Single(Opposite); return t == null ? new List<Unit>() : One(t).Concat(RowNeighbours(t)).Concat(One(Behind(t))).ToList(); }
                case "frontRow": { var r = foes.Where(f => f.Row == "front").ToList(); return r.Count > 0 ? r : foes; }
                case "backRow": { var r = foes.Where(f => f.Row == "back").ToList(); return r.Count > 0 ? r : foes; }
                case "all": return foes;
                // Enemies by condition.
                case "lowest": return Top(() => Lowest(foes), f => f.OrderBy(x => x.HpRatio).ThenBy(x => x.Index));
                case "highest": return Top(() => foes.OrderByDescending(f => f.Hp).ThenBy(f => f.Index).FirstOrDefault(), f => f.OrderByDescending(x => x.Hp).ThenBy(x => x.Index));
                case "strongest": return Top(() => foes.OrderByDescending(f => f.Atk).ThenBy(f => f.Index).FirstOrDefault(), f => f.OrderByDescending(x => x.Atk).ThenBy(x => x.Index));
                case "fastest": return Top(() => foes.OrderByDescending(f => f.Spd).ThenBy(f => f.Index).FirstOrDefault(), f => f.OrderByDescending(x => x.Spd).ThenBy(x => x.Index));
                case "mostMana": return Top(() => foes.OrderByDescending(f => f.Mana).ThenBy(f => f.Index).FirstOrDefault(), f => f.OrderByDescending(x => x.Mana).ThenBy(x => x.Index));
                case "mostBuffs": return One(Single(() => foes.OrderByDescending(f => f.BuffCount).ThenBy(f => f.HpRatio).FirstOrDefault()));
                case "mostDebuffs": return One(Single(() => foes.OrderByDescending(f => f.DebuffCount).ThenBy(f => f.HpRatio).FirstOrDefault()));
                case "role": return One(Single(() => Lowest(foes.Where(f => f.Role == a.Role)) ?? Opposite()));
                case "revenge": return One(Single(() => u.LastAttacker != null && u.LastAttacker.Alive ? u.LastAttacker : Opposite()));
                case "marked": return One(Single(() => foes.FirstOrDefault(f => f.St.Any(s => s.Type == "mark" && s.Source == u.Side)) ?? Opposite()));
                case "random":
                    {
                        var res = new List<Unit>();
                        var pool = foes.ToList();
                        for (int i = 0; i < Math.Max(1, a.N) && pool.Count > 0; i++)
                        {
                            var t = pool[(int)(Roll() * pool.Count)];
                            res.Add(t);
                            if (a.Distinct) pool.Remove(t);
                        }
                        return res;
                    }
                case "chain":
                    {
                        var first = Single(Opposite);
                        var res = One(first);
                        var pool = foes.Where(f => f != first).ToList();
                        while (res.Count < Math.Max(1, a.N) && pool.Count > 0) { var t = pool[(int)(Roll() * pool.Count)]; res.Add(t); pool.Remove(t); }
                        return res;
                    }
                // Allies.
                case "self": return One(u);
                case "same": return prev.Where(x => x.Alive).ToList();
                case "context": return One(context != null && context.Alive ? context : null);
                case "contextAttacker": return One(context?.LastAttacker != null && context.LastAttacker.Alive && context.LastAttacker.Side != u.Side ? context.LastAttacker : null);
                case "lowestAlly": return One(Lowest(Allies(u)));
                case "allAllies": return Allies(u);
                case "behind": return u.Row == "front" ? CoveredBy(u) : One(u);
                case "inFront": return CoverersOf(u);
                case "neighbours": return RowNeighbours(u);
                case "myRow": return Allies(u).Where(x => x.Row == u.Row).ToList();
                case "debuffedAlly": return One(Allies(u).OrderByDescending(x => x.DebuffCount).ThenBy(x => x.HpRatio).FirstOrDefault());
                case "manaAlly": return One(Allies(u).Where(x => x != u && x.Def.Ult != null && x.Mana < Rules.ManaMax).OrderByDescending(x => x.Mana).ThenBy(x => x.Index).FirstOrDefault());
                case "lowestManaAlly": return One(Allies(u).Where(x => x.Def.Ult != null).OrderBy(x => x.Mana).ThenBy(x => x.Index).FirstOrDefault());
                case "fallenAlly": return One(Units.Where(x => !x.Alive && x.Side == u.Side).OrderByDescending(x => x.FellAt).FirstOrDefault());
                default: throw new ArgumentException("Unknown target " + a.Target);
            }
        }

        // A covering hero with an intercept passive takes a dive aimed at the hero behind them (once per turn).
        Unit Intercept(Unit t)
        {
            if (t == null || t.Row != "back") return t;
            var w = CoverersOf(t).FirstOrDefault(f => f.InterceptReady);
            if (w == null) return t;
            w.InterceptReady = false;
            Emit(EventKind.Intercept, w, t);
            return w;
        }

        // ---------- Effects ----------

        void Hit(Unit src, Unit tgt, ActionDef a, SkillDef skill, double power, bool isSkill)
        {
            var R = Rules;
            if (isSkill && tgt.MoonlessReady && src.Side != tgt.Side)
            {
                tgt.MoonlessReady = false;
                Miss(src, tgt, "moonless");
                return;
            }
            int dom = Dominance(R, src.Faction, tgt.Faction);
            double eclipse = src.St.Where(s => s.Type == "eclipse").Sum(s => s.Value);
            double hit = Math.Max(R.HitMin, Math.Min(R.HitMax, R.HitBase + src.Stat("acc") - tgt.Stat("dodge") + dom * R.FactionAccuracy - eclipse));
            if (!Deterministic && Roll() >= hit) { Miss(src, tgt, null); return; }

            string kind = a.Kind ?? skill?.Kind ?? src.Def.Kind;
            double def = (kind == "magic" ? tgt.Spirit : tgt.Armour) * Math.Max(0, 1 - src.Stat("pen") - a.Pen);
            double d = src.Atk * a.Mult * power * R.DefenceK / (R.DefenceK + def);
            d *= TypeMultiplier(R, src.Type, tgt.Type) * (1 - tgt.Mods.Get("res" + src.Type));
            if (a.BonusVs != null && tgt.Has(a.BonusVs)) d *= 1 + a.Bonus;
            if (a.BonusVsExposed != 0 && Exposed(tgt)) d *= 1 + a.BonusVsExposed;
            foreach (var s in tgt.St) d *= 1 + src.Mods.Get("dmgVs" + s.Type);
            if (src.Mods.Get("packHunt") > 0)
            {
                int hunters = tgt.HitBy.Count(h => h.Key != src.Key && h.Value > src.LastTurnAction && Units.Any(x => x.Key == h.Key && x.Faction == src.Faction));
                d *= 1 + src.Mods.Get("packHunt") * hunters;
            }
            var frozen = tgt.Get("frozen");
            if (frozen != null) { d *= 1 + frozen.Value; tgt.St.Remove(frozen); }

            bool crit = !Deterministic && Roll() < src.Stat("crit") + dom * R.FactionCrit;
            if (crit) d *= src.Stat("critDmg");
            if (!Deterministic)
            {
                double spread = R.DamageRange.TryGetValue(a.Range ?? skill?.Range ?? "normal", out var sp) ? sp : 0.1;
                double r = Roll();
                if (src.Mods.Get("luckyRolls") > 0) r = Math.Max(r, Roll());
                d *= 1 - spread + 2 * spread * r;
            }
            var bulwark = tgt.Get("bulwark");
            if (bulwark != null) d *= 1 - bulwark.Value;
            double coverDr = CoverersOf(tgt).Select(f => f.Mods.Get("coverDR")).DefaultIfEmpty(0).Max();
            d *= 1 - coverDr;
            d = Math.Max(1, Math.Round(d));

            Damage(src, tgt, d, crit, null);
            if (crit) src.Stats.Crits++;
            src.Mana = Math.Min(R.ManaMax, src.Mana + R.ManaOnHit);
            tgt.HitBy[src.Key] = Actions;
            tgt.LastAttacker = src;
            if (a.Lifesteal != 0) Heal(src, src, d * a.Lifesteal, false);
            bool contact = a.Target == null || a.Target == "opposite" || a.Target == "front" || a.Target == "pierce" || a.Target == "splash" || a.Target == "cross";
            if (contact && tgt.Row == "front" && tgt.Mods.Get("thorns") > 0 && src.Alive) Damage(tgt, src, Math.Round(d * tgt.Mods.Get("thorns")), false, "thorns");
            if (crit && src.Mods.Get("moonless") > 0 && tgt.Alive) AddStatus(src, tgt, new ActionDef { Status = "eclipse", Turns = 1, Pct = 0.15, Chance = 1 }, 1);
            if (tgt.Alive && src.Mods.Get("gaugePush") > 0) tgt.T += Interval(tgt) * src.Mods.Get("gaugePush");
            if (tgt.Alive && src.Mods.Get("frozen") > 0 && tgt.Has("godstruck") && !tgt.Has("frozen"))
                tgt.St.Add(new Status { Type = "frozen", Turns = 2, Value = src.Mods.Get("frozen"), Source = src.Side });
            if (tgt.Alive && a.Execute != 0 && !tgt.Boss && tgt.HpRatio < a.Execute) Kill(tgt, src);
            if (!tgt.Alive) return;
            FirePassives(src, "onHit", tgt);
            if (crit) FirePassives(src, "onCrit", tgt);
            FirePassives(tgt, "onHitTaken", src);
        }

        void Miss(Unit src, Unit tgt, string why)
        {
            src.Stats.Misses++; tgt.Stats.Dodges++;
            Emit(EventKind.Miss, src, tgt, 0, why);
            FirePassives(tgt, "onDodge", src);
        }

        // Damage that has already been worked out: shields absorb first, then health.
        void Damage(Unit src, Unit tgt, double d, bool crit, string detail)
        {
            if (!tgt.Alive) return;
            double absorbed = Math.Min(tgt.Shield, d);
            tgt.Shield -= absorbed;
            double lost = Math.Min(tgt.Hp, d - absorbed);
            tgt.Hp -= lost;
            if (src != null) src.Stats.Dmg += d;
            tgt.Stats.Taken += d;
            tgt.Mana = Math.Min(Rules.ManaMax, tgt.Mana + lost / tgt.MaxHp * 100 * Rules.ManaPerPctLost);
            Emit(detail == null ? EventKind.Damage : EventKind.DotTick, src, tgt, d, detail, crit);
            if (tgt.Hp <= 0) Kill(tgt, src);
            else CheckHpTriggers(tgt);
        }

        void CheckHpTriggers(Unit u)
        {
            foreach (var p in u.Def.Passives.Where(p => p.Trigger == "hpBelow" && !u.PassiveUsed.Contains(p) && u.HpRatio < p.HpBelow).ToList())
            {
                u.PassiveUsed.Add(p);
                RunPassive(u, p, u.LastAttacker);
            }
        }

        void Kill(Unit t, Unit killer)
        {
            if (!t.Alive) return;
            t.Hp = 0; t.Shield = 0; t.Alive = false; t.Stats.DiedAt = Actions; t.FellAt = ++fallCounter;
            t.St.Clear();
            Emit(EventKind.Death, killer, t);
            if (killer != null && killer.Side != t.Side)
            {
                killer.Stats.Kills++;
                foreach (var w in Allies(killer).Where(x => x.Mods.Get("bloodriteOnKill") > 0))
                    AddStatus(w, w, new ActionDef { Status = "bloodrite", Turns = 99, Pct = w.Mods.Get("bloodriteOnKill") }, 1);
                FirePassives(killer, "onKill", t);
            }
            foreach (var a in Allies(t))
            {
                if (a.Mods.Get("manaOnAllyDeath") > 0) GiveMana(a, a, a.Mods.Get("manaOnAllyDeath"));
                FirePassives(a, "allyFalls", t);
            }
            if (t.Mods.Get("riseAgain") > 0 && roseAgain.Add(t.Side + t.Faction)) Revive(t, t, t.Mods.Get("riseAgain"));
        }

        void Revive(Unit src, Unit t, double pct)
        {
            if (t.Alive) return;
            t.Alive = true; t.Hp = Math.Max(1, Math.Round(t.MaxHp * pct)); t.Stats.DiedAt = null;
            t.T = Math.Max(t.T, (Current?.T ?? 0)) + Interval(t) * 0.5;
            Emit(EventKind.Revive, src, t, t.Hp);
        }

        void Heal(Unit src, Unit tgt, double amt, bool scaled = true)
        {
            if (!tgt.Alive) return;
            if (scaled) amt *= 1 + src.Stat("healPower");
            if (tgt.Has("blight")) amt *= 0.5;
            double gain = Math.Round(Math.Min(amt, tgt.MaxHp - tgt.Hp));
            tgt.Hp += gain; src.Stats.Heal += gain;
            Emit(EventKind.Heal, src, tgt, gain);
            double over = amt - gain;
            if (over > 0 && src.Mods.Get("overhealShield") > 0)
            {
                double cap = tgt.MaxHp * src.Mods.Get("overhealShield");
                double add = Math.Round(Math.Min(over, Math.Max(0, cap - tgt.Shield)));
                if (add > 0) { tgt.Shield += add; src.Stats.Heal += add; Emit(EventKind.Shield, src, tgt, add); }
            }
            if (gain > 0 && src.Mods.Get("judgement") > 0)
            {
                var foe = Lowest(Foes(src));
                if (foe != null) Damage(src, foe, Math.Round(gain * src.Mods.Get("judgement")), false, "judgement");
            }
        }

        void GiveShield(Unit src, Unit tgt, double amt)
        {
            if (!tgt.Alive) return;
            amt = Math.Round(amt * (1 + src.Stat("healPower")) * (1 + tgt.Mods.Get("shieldBonus")));
            tgt.Shield += amt; src.Stats.Heal += amt;
            Emit(EventKind.Shield, src, tgt, amt);
        }

        void GiveMana(Unit src, Unit tgt, double amt)
        {
            double before = tgt.Mana;
            tgt.Mana = Math.Max(0, Math.Min(Rules.ManaMax, tgt.Mana + amt));
            if (tgt.Mana != before) Emit(EventKind.Mana, src, tgt, tgt.Mana - before);
        }

        void Cleanse(Unit src, Unit tgt, int n)
        {
            // Control first, then damage over time, then the rest.
            var order = tgt.St.Where(s => Effects.IsDebuff(s.Type))
                .OrderBy(s => Array.IndexOf(Effects.Control, s.Type) >= 0 ? 0 : s.Type == "pyre" || s.Type == "blight" ? 1 : 2).Take(Math.Max(1, n)).ToList();
            foreach (var s in order) { tgt.St.Remove(s); Emit(EventKind.Cleansed, src, tgt, 0, s.Type); }
            if (src.Mods.Get("cleanseWard") > 0) AddStatus(src, tgt, new ActionDef { Status = "ward", Turns = 1 }, 1);
        }

        void AddStatus(Unit src, Unit tgt, ActionDef a, double power)
        {
            var R = Rules;
            string type = a.Status;
            if (!tgt.Alive) return;
            if (Effects.IsDebuff(type) && src.Side != tgt.Side)
            {
                if (tgt.Def.Immune.Contains(type) || (type == "eclipse" && tgt.Mods.Get("eclipseImmune") > 0)) { Emit(EventKind.Resisted, src, tgt, 0, type); return; }
                double chance = (a.Chance > 0 ? a.Chance : 1) + (type == "godstruck" ? src.Mods.Get("stunChance") : 0);
                double resist = tgt.Stat("tenacity") + (type == "godstruck" && tgt.Has("stunGuard") ? 0.5 : 0);
                chance *= 1 + src.Stat("effect") + Dominance(R, src.Faction, tgt.Faction) * R.FactionEffect - resist;
                chance = Math.Max(R.EffectMin, Math.Min(R.EffectMax, chance));
                if (!Chance(chance)) { Emit(EventKind.Resisted, src, tgt, 0, type); return; }
                var ward = tgt.Get("ward");
                if (ward != null) { tgt.St.Remove(ward); Emit(EventKind.Blocked, src, tgt, 0, type); return; }
                if (tgt.MisdirectReady && tgt.Mods.Get("misdirection") > 0)
                {
                    tgt.MisdirectReady = false;
                    var other = Foes(tgt);
                    if (other.Count > 0)
                    {
                        var to = other[(int)(Roll() * other.Count)];
                        Emit(EventKind.Blocked, tgt, to, 0, "misdirection");
                        tgt = to;
                    }
                }
            }
            int turns = a.Turns > 0 ? a.Turns : type == "godstruck" || type == "hush" ? 1 : 2;
            if (type == "pyre") turns += (int)src.Mods.Get("pyreTurns");
            if (type == "blight") turns += (int)src.Mods.Get("blightTurns");
            if (type == "hush") turns += (int)src.Mods.Get("hushTurns");
            if (Array.IndexOf(Effects.Control, type) >= 0 && tgt.Unyielding) turns = Math.Min(turns, 1);
            if (Array.IndexOf(Effects.Control, type) >= 0)
            {
                var ch = tgt.Get("channel");
                if (ch != null)
                {
                    tgt.St.Remove(ch);
                    Log.Add($"{src.Id} interrupts {tgt.Id}");
                    Emit(EventKind.Interrupt, src, tgt);
                }
            }
            double value;
            switch (type)
            {
                case "pyre":
                case "blight":
                    value = Math.Round(a.Mult > 0 ? src.Atk * a.Mult * power : a.Value);
                    break;
                case "grace":
                    value = Math.Round((a.TargetMaxHp > 0 ? tgt.MaxHp * a.TargetMaxHp : src.Atk * a.Mult * power) * (1 + src.Stat("healPower")) * (1 + src.Mods.Get("gracePct")));
                    break;
                default:
                    value = a.Pct != 0 ? a.Pct : a.Value;
                    break;
            }
            int maxStacks = type == "pyre" ? 3 + (int)src.Mods.Get("pyreStacks") : type == "bloodrite" ? 3 : type == "sunder" ? 1 + (int)src.Mods.Get("sunderStacks") : 1;
            var old = tgt.Get(type);
            if (old != null && maxStacks > 1)
            {
                old.Stacks = Math.Min(maxStacks, old.Stacks + Math.Max(1, a.Stacks));
                old.Turns = Math.Max(old.Turns, turns);
                old.Value = Math.Max(old.Value, value);
                old.Fresh = tgt == Current;
            }
            else
            {
                if (old != null) tgt.St.Remove(old);
                tgt.St.Add(new Status
                {
                    Type = type, Turns = turns, Value = value, Stacks = Math.Min(maxStacks, Math.Max(1, a.Stacks)),
                    Fresh = tgt == Current, Source = src.Side,
                    AtkDown = type == "blight" ? src.Mods.Get("blightAtkDown") : 0,
                    BlocksGrace = type == "blight" && src.Mods.Get("blightBlocksGrace") > 0,
                });
            }
            Emit(EventKind.StatusAdded, src, tgt, tgt.Get(type).Stacks, type);
        }

        // ---------- Skills ----------

        // Is there any point using this skill now? Healing nobody hurt, cleansing nothing or reviving nobody isn't.
        bool Useful(Unit u, SkillDef s)
        {
            foreach (var a in s.Actions)
            {
                switch (a.Effect)
                {
                    case "heal": if (Targets(u, a, new List<Unit>(), null).Any(t => t.Hp < t.MaxHp)) return true; break;
                    case "cleanse": if (Targets(u, a, new List<Unit>(), null).Any(t => t.DebuffCount > 0)) return true; break;
                    case "revive": if (Units.Any(x => !x.Alive && x.Side == u.Side)) return true; break;
                    default: return true;
                }
            }
            return false;
        }

        void RunActions(Unit u, SkillDef skill, IList<ActionDef> actions, double power, bool isSkill, Unit context)
        {
            var prev = One(context);
            foreach (var a in actions)
            {
                var ts = Targets(u, a, prev, context);
                for (int i = 0; i < ts.Count; i++)
                {
                    var t = ts[i];
                    if (!u.Alive && a.Effect != "revive") return;
                    if (!t.Alive && a.Effect != "revive") continue;
                    double p = power * (a.Target == "chain" ? Math.Pow(1 - a.Falloff, i) : 1);
                    switch (a.Effect)
                    {
                        case "damage":
                            Hit(u, t, a, skill, p, isSkill);
                            for (int r = 0; r < a.RepeatOnKill && !t.Alive && u.Alive; r++)
                            {
                                var next = Targets(u, a, prev, context).FirstOrDefault(x => x.Alive);
                                if (next == null) break;
                                t = next;
                                Hit(u, t, a, skill, p, isSkill);
                            }
                            break;
                        case "heal":
                            Heal(u, t, a.TargetMaxHp > 0 ? t.MaxHp * a.TargetMaxHp * p : u.Atk * a.Mult * p);
                            break;
                        case "shield":
                            GiveShield(u, t, a.CasterMaxHp > 0 ? u.MaxHp * a.CasterMaxHp * p : a.TargetMaxHp > 0 ? t.MaxHp * a.TargetMaxHp * p : a.Mult > 0 ? u.Atk * a.Mult * p : a.Value * p);
                            break;
                        case "status": AddStatus(u, t, a, p); break;
                        case "cleanse": Cleanse(u, t, a.N); break;
                        case "strip":
                        case "steal":
                            foreach (var s in t.St.Where(s => Effects.IsBuff(s.Type)).Take(Math.Max(1, a.N)).ToList())
                            {
                                t.St.Remove(s);
                                Emit(EventKind.Cleansed, u, t, 0, s.Type);
                                if (a.Effect == "steal")
                                {
                                    var to = Lowest(Allies(u)) ?? u;
                                    to.St.RemoveAll(x => x.Type == s.Type);
                                    to.St.Add(new Status { Type = s.Type, Turns = Math.Max(1, s.Turns), Value = s.Value, Stacks = s.Stacks, Source = u.Side, Fresh = to == Current });
                                    Emit(EventKind.StatusAdded, u, to, s.Stacks, s.Type);
                                }
                            }
                            break;
                        case "mana": GiveMana(u, t, a.Value * p); break;
                        case "gauge": t.T += Interval(t) * a.Value; break;
                        case "revive": Revive(u, t, a.Pct > 0 ? a.Pct : 0.25); break;
                        case "mark":
                            foreach (var f in Foes(u)) f.St.RemoveAll(s => s.Type == "mark" && s.Source == u.Side);
                            t.St.Add(new Status { Type = "mark", Turns = a.Turns > 0 ? a.Turns : 2, Source = u.Side, Fresh = t == Current });
                            Emit(EventKind.StatusAdded, u, t, 1, "mark");
                            break;
                        case "channel":
                            t.St.Add(new Status { Type = "channel", Turns = 99, Value = a.Pct, Fresh = true, Source = u.Side });
                            Log.Add($"{u.Id} starts a ritual");
                            Emit(EventKind.RitualStart, u, t);
                            break;
                        case "resetCd":
                            if (t.Cd.Length > 0) t.Cd[Math.Min(t.Cd.Length, Math.Max(1, a.N)) - 1] = 0;
                            break;
                        default: throw new ArgumentException("Unknown effect " + a.Effect);
                    }
                }
                prev = ts;
            }
        }

        void FirePassives(Unit u, string trigger, Unit context)
        {
            if (!u.Alive || passiveDepth > 2) return;
            foreach (var p in u.Def.Passives)
            {
                if (p.Trigger != trigger || p.Actions.Count == 0) continue;
                if (p.Once && u.PassiveUsed.Contains(p)) continue;
                if (u.PassiveCd.TryGetValue(p, out var cd) && cd > 0) continue;
                if (trigger == "turnStart" && p.Every > 0 && u.TurnsTaken % (int)p.Every != 0) continue;
                if (p.Chance > 0 && !Chance(p.Chance)) continue;
                u.PassiveUsed.Add(p);
                if (p.Cooldown > 0) u.PassiveCd[p] = p.Cooldown;
                RunPassive(u, p, context);
            }
        }

        void RunPassive(Unit u, PassiveDef p, Unit context)
        {
            passiveDepth++;
            Emit(EventKind.Turn, u, null, 0, "passive:" + p.Name);
            RunActions(u, null, p.Actions, 1, false, context);
            passiveDepth--;
        }

        // ---------- Turns ----------

        Unit Next()
        {
            Unit best = null;
            foreach (var x in Units)
            {
                if (!x.Alive) continue;
                if (best == null || x.T < best.T - 1e-9) { best = x; continue; }
                if (Math.Abs(x.T - best.T) > 1e-9) continue;
                if (x.Spd > best.Spd || (x.Spd == best.Spd && string.CompareOrdinal(x.Side, best.Side) < 0)) best = x;
            }
            return best;
        }

        // Advances the fight by one unit's turn.
        public void Step()
        {
            if (Over) return;
            var u = Next();
            Current = u;
            Actions++;
            u.TurnsTaken++;
            u.MisdirectReady = true;
            u.InterceptReady = u.Mods.Get("intercept") > 0;
            u.T += Interval(u);

            // Start of turn: damage and healing over time, mana.
            foreach (var s in u.St.Where(s => s.Type == "pyre" || s.Type == "blight").ToList())
            {
                double d = s.Value * s.Stacks;
                var bulwark = u.Get("bulwark");
                if (bulwark != null && u.Mods.Get("bulwarkDot") > 0) d *= 1 - bulwark.Value;
                Damage(null, u, Math.Round(d), false, s.Type);
                if (!u.Alive) break;
            }
            var grace = u.Get("grace");
            if (u.Alive && grace != null && !u.St.Any(s => s.Type == "blight" && s.BlocksGrace)) Heal(u, u, grace.Value, false);
            if (u.Alive)
            {
                u.Mana = Math.Min(Rules.ManaMax, u.Mana + u.Stat("manaRegen"));
                FirePassives(u, "turnStart", null);
                if (u.Faction != null && u.Mods.Get("tideEvery") > 0 && ++seaTurns[u.Side] % (int)u.Mods.Get("tideEvery") == 0) Tide(u);
            }

            if (u.Alive)
            {
                var channel = u.Get("channel");
                if (u.Has("godstruck")) Emit(EventKind.Turn, u, null, 0, "stunned");
                else if (channel != null)
                {
                    u.St.Remove(channel);
                    Heal(u, u, u.MaxHp * channel.Value, false);
                    Log.Add($"{u.Id} completes a ritual");
                    Emit(EventKind.RitualComplete, u, u);
                }
                else Act(u);
            }

            // End of turn: cooldowns and effect durations tick down.
            if (u.Alive)
            {
                double tick = 1 + u.Stat("haste");
                for (int i = 0; i < u.Cd.Length; i++) u.Cd[i] -= tick;
                foreach (var p in u.PassiveCd.Keys.ToList()) u.PassiveCd[p]--;
                bool wasStunned = u.Has("godstruck");
                foreach (var s in u.St) { if (s.Fresh) s.Fresh = false; else s.Turns--; }
                u.St.RemoveAll(s => s.Turns <= 0);
                if (wasStunned && !u.Has("godstruck")) u.St.Add(new Status { Type = "stunGuard", Turns = 1, Source = u.Side });
                u.LastTurnAction = Actions;
            }
            Current = null;
            if (Alive("E").Count == 0) { Over = true; Result = "win"; }
            else if (Alive("A").Count == 0) { Over = true; Result = "lose"; }
            else if (Actions >= Rules.MaxActions) { Over = true; Result = "timeout"; }
            if (Over) Emit(EventKind.End, null, null, 0, Result);
        }

        // Ultimate if mana is full, else the first ready skill (1, 2, 3) that has a use, else the basic attack.
        void Act(Unit u)
        {
            bool hushed = u.Has("hush");
            if (!hushed && u.Def.Ult != null && u.Mana >= Rules.ManaMax)
            {
                u.Mana = 0;
                Emit(EventKind.Turn, u, null, 0, "ult:" + u.Def.Ult.Name);
                RunActions(u, u.Def.Ult, u.Def.Ult.Actions, 1, true, null);
                FirePassives(u, "afterUlt", null);
                return;
            }
            if (!hushed)
            {
                for (int i = 0; i < u.Def.Skills.Count; i++)
                {
                    var s = u.Def.Skills[i];
                    if (u.Cd[i] > 1e-9 || !Useful(u, s)) continue;
                    Emit(EventKind.Turn, u, null, 0, "skill:" + s.Name);
                    RunActions(u, s, s.Actions, 1, true, null);
                    u.Cd[i] = s.Cd;
                    u.SkillsUsed++;
                    double every = u.Mods.Get("recitationEvery");
                    if (every > 0 && u.SkillsUsed % (int)every == 0 && u.Alive)
                    {
                        Emit(EventKind.Turn, u, null, 0, "recitation:" + s.Name);
                        RunActions(u, s, s.Actions, u.Mods.Get("recitationPower"), true, null);
                    }
                    return;
                }
            }
            Emit(EventKind.Turn, u, null, 0, "basic");
            RunActions(u, u.Def.Basic, u.Def.Basic.Actions, 1, false, null);
        }

        // Sea doctrine: the tide cleanses one effect from each ally and gives them Ward.
        void Tide(Unit u)
        {
            Emit(EventKind.Turn, u, null, 0, "passive:Tide");
            foreach (var a in Allies(u))
            {
                if (a.DebuffCount > 0) Cleanse(u, a, 1);
                AddStatus(u, a, new ActionDef { Status = "ward", Turns = 2 }, 1);
            }
        }

        public static BattleResult Run(GameData data, IList<TeamSlot> team, string stageId, long seed, BattleOptions options = null)
        {
            var B = new Battle(data, team, stageId, seed, options);
            while (!B.Over) B.Step();
            return new BattleResult
            {
                Result = B.Result,
                Actions = B.Actions,
                Seconds = B.Actions * data.Rules.SecondsPerActionAt1x,
                Rituals = B.Log.Count(l => l.Contains("completes")),
                Heroes = B.Units.Where(u => u.Side == "A").Select(u => new HeroResult
                {
                    Id = u.Id, Dmg = u.Stats.Dmg, Heal = u.Stats.Heal, Taken = u.Stats.Taken, DiedAt = u.Stats.DiedAt,
                }).ToList(),
            };
        }
    }
}
