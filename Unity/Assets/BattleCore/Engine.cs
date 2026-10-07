// Headless battle engine: the rules of a fight, with no graphics. The Unity client and the
// balance simulator both run this code, so simulated results are what players will get.
// Ported line for line from sim-js/engine.js; seeded fights give identical results in both
// (checked by `BattleSim --parity` against sim-js/parity.js). Keep the two in step until the JavaScript one is retired.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    // xorshift32, matching rngFrom() in engine.js bit for bit (including its signed >> 17).
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

    public class Status
    {
        public string Type;
        public int Turns;
        public double Value;
        public bool Fresh;
    }

    public class UnitStats
    {
        public double Dmg, Heal, Taken;
        public int? DiedAt;
    }

    public class Unit
    {
        public UnitDef Def;
        public string Id, Key, Side, Row, Slot, Faction, Type;
        public bool Boss, Backline;
        public double MaxHp, Hp, Atk, Defence, Spd, Energy, Shield, T;
        public int Cd;
        public bool Alive = true;
        public List<Status> St = new List<Status>();
        public UnitStats Stats = new UnitStats();
        public bool Has(string t) => St.Any(s => s.Type == t);
    }

    public class TeamSlot
    {
        public string Id, Row, Slot;
        public TeamSlot(string id, string row, string slot) { Id = id; Row = row; Slot = slot; }
    }

    // What happened, in order, for the client to animate. Actor and Target are unit keys.
    public enum EventKind { Turn, Damage, Heal, Shield, StatusAdded, Interrupt, RitualStart, RitualComplete, BurnTick, Death, End }

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
        readonly Rng rnd;

        public Battle(GameData data, IList<TeamSlot> team, string stageId, long seed)
        {
            Rules = data.Rules;
            Stage = data.Stages.FirstOrDefault(s => s.Id == stageId) ?? throw new ArgumentException("Unknown stage " + stageId);
            rnd = new Rng(seed);
            foreach (var m in team)
            {
                var h = data.Heroes.FirstOrDefault(x => x.Id == m.Id) ?? throw new ArgumentException("Unknown hero " + m.Id);
                Units.Add(MakeUnit(h, "A", m.Row, m.Slot, 1, 1));
            }
            var cnt = new Dictionary<string, int> { ["front"] = 0, ["back"] = 0 };
            foreach (var se in Stage.Enemies)
            {
                var e = data.Enemies.FirstOrDefault(x => x.Id == se.Id) ?? throw new ArgumentException("Unknown enemy " + se.Id);
                if (!cnt.ContainsKey(se.Row)) cnt[se.Row] = 0;
                Units.Add(MakeUnit(e, "E", se.Row, se.Row + (cnt[se.Row]++), Stage.HpScale * Stage.StatScale, Stage.AtkScale * Stage.StatScale));
            }
            foreach (var u in Units) u.T = 1000 / u.Spd * (0.85 + rnd.Next() * 0.3);
        }

        static Unit MakeUnit(UnitDef d, string side, string row, string slot, double hpScale, double atkScale)
        {
            double hp = JsRound(d.Hp * hpScale);
            return new Unit
            {
                Def = d, Id = d.Id, Key = side + ":" + slot, Side = side, Row = row, Slot = slot,
                Faction = d.Faction, Type = d.Type, Boss = d.Boss, Backline = d.Backline,
                MaxHp = hp, Hp = hp, Atk = d.Atk * atkScale, Defence = d.Def, Spd = d.Spd,
                Energy = side == "A" ? 20 : 0,
                Cd = d.Skill != null ? (int)Math.Ceiling(d.Skill.Cd / 2.0) : 0,
            };
        }

        // JavaScript's Math.round: halves round towards +infinity.
        public static double JsRound(double x)
        {
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        public static double Matchup(Rules r, Unit src, Unit tgt)
        {
            double m = 1;
            if (r.FactionWheel.TryGetValue(src.Faction ?? "", out var beats) && beats == tgt.Faction) m *= 1 + r.FactionAdvantage;
            else if (r.FactionWheel.TryGetValue(tgt.Faction ?? "", out var beaten) && beaten == src.Faction) m *= 1 - r.FactionAdvantage;
            if (r.TypeChart.TryGetValue(src.Type ?? "", out var tc))
            {
                if (tc.Strong.Contains(tgt.Type)) m *= 1 + r.TypeStrong;
                if (tc.ResistedBy.Contains(tgt.Type)) m *= 1 - r.TypeResisted;
            }
            return Math.Min(r.MatchupMax, Math.Max(r.MatchupMin, m));
        }

        void Emit(EventKind k, Unit actor, Unit target, double amount = 0, string detail = null, bool crit = false) =>
            Events.Add(new BattleEvent { Action = Actions, Kind = k, Actor = actor?.Key, Target = target?.Key, Amount = amount, Detail = detail, Crit = crit });

        List<Unit> AliveSide(string side) => Units.Where(u => u.Alive && u.Side == side).ToList();
        List<Unit> FoesOf(Unit u) => AliveSide(u.Side == "A" ? "E" : "A");
        List<Unit> AlliesOf(Unit u) => AliveSide(u.Side);
        Unit Pick(List<Unit> a) => a.Count > 0 ? a[(int)Math.Floor(rnd.Next() * a.Count)] : null;
        List<Unit> FrontRow(Unit u) { var f = FoesOf(u); var r = f.Where(x => x.Row == "front").ToList(); return r.Count > 0 ? r : f; }
        List<Unit> BackRow(Unit u) { var f = FoesOf(u); var r = f.Where(x => x.Row == "back").ToList(); return r.Count > 0 ? r : f; }
        Unit FrontTarget(Unit u) => FoesOf(u).FirstOrDefault(x => x.Has("taunt")) ?? Pick(FrontRow(u));
        static List<Unit> One(Unit u) => u == null ? new List<Unit>() : new List<Unit> { u };
        // First unit with the lowest health ratio (stable, like Array.sort()[0]).
        static List<Unit> LowestRatio(List<Unit> a) => a.OrderBy(x => x.Hp / x.MaxHp).Take(1).ToList();

        List<Unit> ResolveTargets(Unit u, string kind, List<Unit> prev)
        {
            switch (kind)
            {
                case "self": return One(u);
                case "same": return prev.Where(x => x.Alive).ToList();
                case "front": return One(FrontTarget(u));
                case "back": return One(Pick(BackRow(u)));
                case "frontRow": return FrontRow(u);
                case "backRow": return BackRow(u);
                case "allEnemies": return FoesOf(u);
                case "weakest": return LowestRatio(FoesOf(u));
                case "allAllies": return AlliesOf(u);
                case "lowestAlly": return LowestRatio(AlliesOf(u));
                case "strongestAlly": return One(AlliesOf(u).Where(x => x != u).OrderByDescending(x => x.Atk).FirstOrDefault() ?? u);
                case "control":
                    {
                        var f = FoesOf(u);
                        return One(f.FirstOrDefault(x => x.Has("channel")) ?? f.FirstOrDefault(x => x.Boss) ?? FrontTarget(u));
                    }
                default: throw new ArgumentException("Unknown target " + kind);
            }
        }

        static double AtkOf(Unit u)
        {
            double m = 1;
            foreach (var s in u.St) if (s.Type == "atkup") m += s.Value;
            return u.Atk * m;
        }

        void Kill(Unit t)
        {
            if (!t.Alive) return;
            t.Hp = 0; t.Alive = false; t.Stats.DiedAt = Actions;
            Emit(EventKind.Death, null, t);
        }

        void Hit(Unit src, Unit tgt, ActionDef a)
        {
            var R = Rules;
            double d = AtkOf(src) * a.Mult * 100 / (100 + tgt.Defence * (1 - a.Pierce));
            d *= Matchup(R, src, tgt);
            bool crit = rnd.Next() < R.CritChance;
            if (crit) d *= R.CritMultiplier;
            d *= 1 - R.DamageSpread + rnd.Next() * 2 * R.DamageSpread;
            var g = tgt.St.FirstOrDefault(s => s.Type == "guard");
            if (g != null) d *= 1 - g.Value;
            d = JsRound(d);
            double absorbed = Math.Min(tgt.Shield, d);
            tgt.Shield -= absorbed;
            tgt.Hp = Math.Max(0, tgt.Hp - (d - absorbed));
            src.Stats.Dmg += d; tgt.Stats.Taken += d;
            tgt.Energy = Math.Min(100, tgt.Energy + R.EnergyWhenHit);
            Emit(EventKind.Damage, src, tgt, d, null, crit);
            if (a.Lifesteal != 0) Heal(src, src, d * a.Lifesteal);
            if (tgt.Hp <= 0) Kill(tgt);
            else if (a.Execute != 0 && !tgt.Boss && tgt.Hp < tgt.MaxHp * a.Execute) Kill(tgt);
        }

        void Heal(Unit src, Unit tgt, double amt)
        {
            if (!tgt.Alive) return;
            double a = JsRound(Math.Min(amt, tgt.MaxHp - tgt.Hp));
            tgt.Hp += a; src.Stats.Heal += a;
            Emit(EventKind.Heal, src, tgt, a);
        }

        void AddStatus(Unit src, Unit tgt, ActionDef a)
        {
            if ((a.Status == "stun" || a.Status == "silence") && tgt.Has("channel"))
            {
                tgt.St = tgt.St.Where(s => s.Type != "channel").ToList();
                Log.Add($"{src.Id} interrupts {tgt.Id}");
                Emit(EventKind.Interrupt, src, tgt);
            }
            if (a.Status != "atkup") tgt.St = tgt.St.Where(s => s.Type != a.Status).ToList();
            double value = a.AtkScale != 0 ? JsRound(AtkOf(src) * a.AtkScale) : a.Value;
            tgt.St.Add(new Status { Type = a.Status, Turns = a.Turns, Value = value, Fresh = tgt == Current });
            Emit(EventKind.StatusAdded, src, tgt, value, a.Status);
        }

        void RunActions(Unit u, List<ActionDef> actions)
        {
            var prev = new List<Unit>();
            foreach (var a in actions)
            {
                var ts = ResolveTargets(u, a.Target, prev);
                foreach (var t in ts)
                {
                    if (!t.Alive) continue;
                    switch (a.Effect)
                    {
                        case "damage": Hit(u, t, a); break;
                        case "heal":
                            Heal(u, t, AtkOf(u) * a.Mult);
                            if (a.Cleanse) t.St = t.St.Where(s => s.Type != "burn" && s.Type != "stun" && s.Type != "silence").ToList();
                            break;
                        case "shield":
                            {
                                double v = a.CasterMaxHp != 0 ? JsRound(u.MaxHp * a.CasterMaxHp) : a.Value;
                                t.Shield += v; u.Stats.Heal += v;
                                Emit(EventKind.Shield, u, t, v);
                                break;
                            }
                        case "status": AddStatus(u, t, a); break;
                        case "channel":
                            t.St.Add(new Status { Type = "channel", Turns = 99, Value = a.HealMaxHp, Fresh = true });
                            Log.Add($"{u.Id} starts a ritual");
                            Emit(EventKind.RitualStart, u, t);
                            break;
                        default: throw new ArgumentException("Unknown effect " + a.Effect);
                    }
                }
                prev = ts;
            }
        }

        static double EffSpd(Unit u)
        {
            var s = u.St.FirstOrDefault(x => x.Type == "slow");
            return u.Spd * (s != null ? 1 - s.Value : 1);
        }

        // Advances the fight by one unit's turn.
        public void Step()
        {
            if (Over) return;
            Unit u = null;
            foreach (var x in Units) if (x.Alive && (u == null || x.T < u.T)) u = x;
            u.T += 1000 / EffSpd(u); Actions++; Current = u;
            foreach (var s in u.St.Where(s => s.Type == "burn").ToList())
            {
                u.Hp = Math.Max(0, u.Hp - s.Value); u.Stats.Taken += s.Value;
                Emit(EventKind.BurnTick, null, u, s.Value);
                if (u.Hp <= 0) Kill(u);
            }
            if (u.Alive)
            {
                if (u.Cd > 0) u.Cd--;
                var ch = u.St.FirstOrDefault(s => s.Type == "channel");
                if (u.Has("stun")) Emit(EventKind.Turn, u, null, 0, "stunned");
                else if (ch != null)
                {
                    u.St.Remove(ch);
                    Heal(u, u, u.MaxHp * ch.Value);
                    Log.Add($"{u.Id} completes a ritual");
                    Emit(EventKind.RitualComplete, u, u);
                }
                else
                {
                    bool silenced = u.Has("silence");
                    if (u.Def.Ult != null && u.Energy >= 100 && !silenced)
                    {
                        u.Energy = 0;
                        Emit(EventKind.Turn, u, null, 0, "ult:" + u.Def.Ult.Name);
                        RunActions(u, u.Def.Ult.Actions);
                    }
                    else if (u.Def.Skill != null && u.Cd == 0 && !silenced)
                    {
                        Emit(EventKind.Turn, u, null, 0, "skill:" + u.Def.Skill.Name);
                        RunActions(u, u.Def.Skill.Actions);
                        u.Cd = u.Def.Skill.Cd;
                        u.Energy = Math.Min(100, u.Energy + Rules.EnergyPerAction);
                    }
                    else
                    {
                        Emit(EventKind.Turn, u, null, 0, "basic");
                        var t = u.Backline ? Pick(BackRow(u)) : FrontTarget(u);
                        if (t != null) Hit(u, t, new ActionDef { Mult = 1 });
                        u.Energy = Math.Min(100, u.Energy + Rules.EnergyPerAction);
                    }
                }
                foreach (var s in u.St) { if (s.Fresh) s.Fresh = false; else s.Turns--; }
                u.St = u.St.Where(s => s.Turns > 0).ToList();
            }
            Current = null;
            if (AliveSide("E").Count == 0) { Over = true; Result = "win"; }
            else if (AliveSide("A").Count == 0) { Over = true; Result = "lose"; }
            else if (Actions >= Rules.MaxActions) { Over = true; Result = "timeout"; }
            if (Over) Emit(EventKind.End, null, null, 0, Result);
        }

        public static BattleResult Run(GameData data, IList<TeamSlot> team, string stageId, long seed)
        {
            var B = new Battle(data, team, stageId, seed);
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
