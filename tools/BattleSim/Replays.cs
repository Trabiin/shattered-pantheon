// Sample fights recorded turn by turn for the browser fight viewer (tools/FightViewer).
// For each stage: a strong team winning, a typical team, and on two stages the same strong team
// in its best and worst arrangement, to show how much placement matters.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShatteredPantheon.Battle;

static partial class Program
{
    static void WriteReplays(string path)
    {
        var fights = new List<object>();
        var teams = SampleTeams(60, 777);
        foreach (var stage in data.Stages)
        {
            int runs = stage.Difficulty == "nightmare" ? 4 : 8;
            var scored = teams.Select(t => (ids: t, team: Formation.AutoPlace(data, t), r: RunMany(Formation.AutoPlace(data, t), stage.Id, runs)))
                .OrderByDescending(x => x.r.WinRate).ThenBy(x => x.r.AvgWinSeconds ?? 9999).ToList();
            var best = scored[0];
            var typical = scored[scored.Count / 2];
            fights.Add(Record(stage, best.team, FirstSeed(best.team, stage.Id, "win"), "Strong team", $"Wins {Pct(best.r.WinRate)} of fights"));
            fights.Add(Record(stage, typical.team, 1, "Typical team", $"Wins {Pct(typical.r.WinRate)} of fights"));

            if (stage.Id == "chapel" || stage.Id == "foundry")
            {
                var placed = Arrangements(best.ids).Select(a => (team: a, rate: RunMany(a, stage.Id, 6).WinRate)).ToList();
                var top = placed.OrderByDescending(p => p.rate).First();
                var worst = placed.OrderBy(p => p.rate).First();
                fights.Add(Record(stage, top.team, FirstSeed(top.team, stage.Id, "win"), "Same heroes, best placement", $"Wins {Pct(top.rate)} of fights in this arrangement"));
                fights.Add(Record(stage, worst.team, FirstSeed(worst.team, stage.Id, "lose"), "Same heroes, worst placement", $"Wins {Pct(worst.rate)} of fights in this arrangement"));
            }
        }
        var json = JsonSerializer.Serialize(new { generated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"), fights });
        File.WriteAllText(path, json);
        Console.WriteLine($"Wrote {fights.Count} fights to {path} ({json.Length / 1024} KB)");
    }

    // The first seed whose fight ends with the wanted result, so the replay shows the typical outcome.
    static long FirstSeed(List<TeamSlot> team, string stageId, string result)
    {
        for (long s = 1; s < 200; s++)
            if (Battle.Run(data, team, stageId, s, options).Result == result) return s;
        return 1;
    }

    static object Record(StageDef stage, List<TeamSlot> team, long seed, string title, string note)
    {
        var b = new Battle(data, team, stage.Id, seed, options);
        var units = b.Units.Select(u => new
        {
            key = u.Key, id = u.Id, name = u.Def.Name, side = u.Side, slot = u.Slot, row = u.Row, x = u.X,
            faction = u.Faction, type = u.Type, role = u.Role, boss = u.Boss, maxHp = Math.Round(u.MaxHp),
        }).ToList();
        var frames = new List<object> { Frame(b, 0) };
        while (!b.Over)
        {
            int from = b.Events.Count;
            b.Step();
            frames.Add(Frame(b, from));
        }
        return new
        {
            stage = stage.Id, stageName = stage.Name, difficulty = stage.Difficulty, title, note, seed,
            heroFormation = b.HeroFormation, enemyFormation = b.EnemyFormation, result = b.Result, actions = b.Actions, units, frames,
        };
    }

    // One turn: what happened (events) and where every unit stands afterwards
    // (health, shield, mana, alive, statuses as "Label:stacks:turns").
    static object Frame(Battle b, int from)
    {
        var events = b.Events.Skip(from).Select(e => new object[] { e.Kind.ToString(), e.Actor ?? "", e.Target ?? "", e.Detail ?? "", Math.Round(e.Amount), e.Crit ? 1 : 0 }).ToList();
        var state = b.Units.Select(u => new object[]
        {
            Math.Round(Math.Max(0, u.Hp)), Math.Round(u.Shield), Math.Round(u.Mana), u.Alive ? 1 : 0,
            u.St.Select(s => (Effects.Label(s.Type) ?? s.Type) + ":" + s.Stacks + ":" + s.Turns).ToArray(),
        }).ToList();
        return new { e = events, s = state };
    }
}
