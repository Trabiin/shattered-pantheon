// Development-only fight log: in the Unity editor and development builds, each battle writes what happened
// and when to the Console (turns, skills, damage, effects, rituals, interrupts, deaths), so things that flash
// past on screen can be checked. Release builds skip it. It only reads the engine's event stream, so the
// battle engine does no extra work for it.
namespace ShatteredPantheon.Game
{
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;

    public static class FightLog
    {
        public static bool Enabled => Debug.isDebugBuild;

        public static string Start(Battle b, StageDef stage, int seed) =>
            $"Fight log: {stage.Name} ({stage.Id}), seed {seed}. Heroes ({b.HeroFormation}): {Names(b, "A")}. Enemies: {Names(b, "E")}.";

        // One Console line per action: when it happened (fight time at 1x) and everything in it.
        public static string Action(Battle b, IEnumerable<BattleEvent> events)
        {
            var parts = events.Select(e => Describe(b, e)).Where(d => d != null).ToList();
            if (parts.Count == 0) return null;
            int action = events.First().Action;
            int seconds = (int)(action * b.Rules.SecondsPerActionAt1x);
            return $"[{seconds / 60}:{seconds % 60:00}, action {action}] {string.Join(". ", parts)}.";
        }

        // What one event means, in words; null for events the log leaves out (mana changes).
        public static string Describe(Battle b, BattleEvent e)
        {
            string a = Name(b, e.Actor), t = Name(b, e.Target), detail = e.Detail ?? "";
            string effect = Effects.Label(detail) ?? Ui.Capitalise(detail);
            switch (e.Kind)
            {
                case EventKind.Turn:
                    if (detail == "stunned") return $"{a} is Godstruck and loses the turn";
                    if (detail.StartsWith("ult:")) return $"{a} unleashes the ultimate {detail.Substring(4)}";
                    if (detail.StartsWith("skill:")) return $"{a} uses {detail.Substring(6)}";
                    if (detail.StartsWith("recitation:")) return $"{a} recites {detail.Substring(11)} again";
                    if (detail.StartsWith("passive:")) return $"{a}'s passive {detail.Substring(8)} fires";
                    return $"{a} attacks";
                case EventKind.Damage: return $"{t} takes {e.Amount:0}{(e.Crit ? " (critical)" : "")}{(a != null ? " from " + a : "")}";
                case EventKind.Miss: return $"{a} misses {t}{(detail != "" ? " (" + detail + ")" : "")}";
                case EventKind.DotTick: return $"{t} takes {e.Amount:0} from {effect}";
                case EventKind.Heal: return e.Amount > 0 ? $"{t} heals {e.Amount:0}" : null;
                case EventKind.Shield: return $"{t} gains a {e.Amount:0} shield";
                case EventKind.StatusAdded: return Effects.Label(detail) != null ? $"{t} gets {effect}{(e.Amount > 1 ? $" x{e.Amount:0}" : "")}" : null;
                case EventKind.Resisted: return $"{t} resists {effect}";
                case EventKind.Blocked: return detail == "misdirection" ? $"{a} misdirects the effect onto {t}" : $"{t}'s Ward blocks {effect}";
                case EventKind.Cleansed: return $"{t} is cleansed of {effect}";
                case EventKind.Interrupt: return $"{a} interrupts {t}'s ritual";
                case EventKind.Intercept: return $"{a} intercepts the attack on {t}";
                case EventKind.RitualStart: return $"{t} begins a healing ritual";
                case EventKind.RitualComplete: return $"{t} completes the ritual";
                case EventKind.Death: return $"{t} falls";
                case EventKind.Revive: return $"{t} rises again";
                case EventKind.End: return $"Fight over: {(detail == "win" ? "Victory" : detail == "lose" ? "Defeat" : "Time's up")}";
                default: return null;
            }
        }

        // Units that share a name (three Hollow Acolytes) also say where they stand.
        static string Name(Battle b, string key) => key == null ? null : Name(b, b.Units.First(u => u.Key == key));
        static string Name(Battle b, Unit u) => b.Units.Count(x => x.Def.Name == u.Def.Name) > 1 ? $"{u.Def.Name} ({u.Slot ?? u.Key})" : u.Def.Name;
        static string Names(Battle b, string side) => string.Join(", ", b.Units.Where(u => u.Side == side).Select(u => u.Side == "A" ? u.Def.Name + "@" + u.Slot : Name(b, u)));
    }
}
