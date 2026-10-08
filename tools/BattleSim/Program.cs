// Battle simulator on the shared C# engine (the same files the Unity game compiles).
// Run from the repository root:
//   dotnet run --project tools/BattleSim -c Release              -> full report, writes reports/latest.md
//   dotnet run --project tools/BattleSim -- --team hilde,solenne,thessaly,maren,pip --stage saint --runs 500
//   dotnet run --project tools/BattleSim -- --team hilde@front0,solenne@front1,thessaly@back0,maren@back1,pip@back2 --stage saint
//   dotnet run --project tools/BattleSim -- --log hilde,solenne,thessaly,maren,pip --stage saint --seed 1   -> one fight, event by event
//   dotnet run --project tools/BattleSim -- --fingerprint 300  -> one line per fight; two runs must match (same seed, same fight)
// Options: --data <dir> (default Unity/Assets/Resources/BattleData), --out <file>, --teams N, --runs N, --puzzle (no randomness).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShatteredPantheon.Battle;

static class Program
{
    static GameData data;
    static BattleOptions options = new BattleOptions();

    static int Main(string[] argv)
    {
        var args = new Dictionary<string, string>();
        for (int i = 0; i < argv.Length; i++)
            if (argv[i].StartsWith("--")) args[argv[i].Substring(2)] = i + 1 < argv.Length && !argv[i + 1].StartsWith("--") ? argv[i + 1] : null;
        string Arg(string k, string def) => args.TryGetValue(k, out var v) && v != null ? v : def;

        string root = args.ContainsKey("data") ? null : FindRoot();
        data = GameData.LoadDirectory(Arg("data", root != null ? Path.Combine(root, DataPath) : null));
        options.Deterministic = args.ContainsKey("puzzle");

        if (args.ContainsKey("fingerprint")) { Fingerprint(int.Parse(Arg("fingerprint", "300"))); return 0; }

        if (args.ContainsKey("log"))
        {
            // Play-by-play of one fight: the event stream the game client animates.
            var team = Place(Arg("log", "").Split(','));
            var b = new Battle(data, team, Arg("stage", "saint"), long.Parse(Arg("seed", "1")), options);
            Console.WriteLine("Heroes: " + string.Join(", ", team.Select(t => t.Id + "@" + t.Slot)) + $" ({b.HeroFormation})");
            while (!b.Over) b.Step();
            foreach (var e in b.Events) Console.WriteLine(e);
            return 0;
        }

        if (args.ContainsKey("team"))
        {
            string stage = Arg("stage", "saint");
            var team = Place(Arg("team", "").Split(','));
            var r = RunMany(team, stage, int.Parse(Arg("runs", "300")));
            Console.WriteLine($"{string.Join(", ", team.Select(t => t.Id + "@" + t.Slot))} vs {stage}: win {Pct(r.WinRate)}, avg fight {Mmss(r.AvgSeconds)}, avg win {Mmss(r.AvgWinSeconds)}, timeouts {Pct(r.TimeoutRate)}");
            return 0;
        }

        string report = FullReport(int.Parse(Arg("teams", "400")), int.Parse(Arg("runs", "12")));
        string outPath = Arg("out", Path.Combine(root ?? ".", "reports", "latest.md"));
        File.WriteAllText(outPath, report);
        Console.WriteLine(report);
        return 0;
    }

    // The repository root: the folder holding Unity/Assets/Resources/BattleData.
    static string FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, DataPath, "rules.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Could not find " + DataPath + "; pass --data <dir>");
    }

    const string DataPath = "Unity/Assets/Resources/BattleData";

    // "id" entries are auto-placed; "id@slot" entries are placed where given.
    static List<TeamSlot> Place(IList<string> ids) => ids.All(i => i.Contains('@'))
        ? ids.Select(i => new TeamSlot(i.Split('@')[0], i.Split('@')[1])).ToList()
        : Formation.AutoPlace(data, ids);

    class Many { public double WinRate, AvgSeconds, TimeoutRate; public double? AvgWinSeconds; }

    static Many RunMany(List<TeamSlot> team, string stageId, int runs, long seedBase = 1)
    {
        int wins = 0, timeouts = 0; double secs = 0, winSecs = 0;
        for (int i = 0; i < runs; i++)
        {
            var r = Battle.Run(data, team, stageId, seedBase + i * 7919L, options);
            secs += r.Seconds;
            if (r.Result == "win") { wins++; winSecs += r.Seconds; }
            if (r.Result == "timeout") timeouts++;
        }
        return new Many { WinRate = (double)wins / runs, AvgSeconds = secs / runs, AvgWinSeconds = wins > 0 ? winSecs / wins : (double?)null, TimeoutRate = (double)timeouts / runs };
    }

    static string Pct(double x) => Math.Round(x * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) + "%";
    static string Pts(double x) => (x >= 0 ? "+" : "") + Math.Round(x * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) + " pts";
    static string Mmss(double? s)
    {
        if (s == null) return "n/a";
        double r = Math.Round(s.Value, MidpointRounding.AwayFromZero);
        return $"{Math.Floor(r / 60)}:{(r % 60).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}";
    }
    static string Short(string id) => char.ToUpper(id[0]) + id.Substring(1);
    static string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(Short));
    static string Slots(List<TeamSlot> team) => string.Join(", ", team.Select(t => Short(t.Id) + " " + t.Slot));

    // Reproducible team sampler (a double-precision LCG).
    static List<string[]> SampleTeams(int n, double seed = 12345)
    {
        var ids = data.Heroes.Select(h => h.Id).ToArray();
        double s = seed;
        double Rnd() { s = (s * 1103515245 + 12345) % 2147483648; return s / 2147483648; }
        var teams = new List<string[]>();
        for (int k = 0; k < n; k++)
        {
            var a = (string[])ids.Clone();
            for (int i = a.Length - 1; i > 0; i--) { int j = (int)Math.Floor(Rnd() * (i + 1)); (a[i], a[j]) = (a[j], a[i]); }
            teams.Add(a.Take(5).ToArray());
        }
        return teams;
    }

    // Every way to place 5 heroes: both formations, every order (240 arrangements).
    static IEnumerable<List<TeamSlot>> Arrangements(string[] ids)
    {
        foreach (var f in new[] { "2-3", "3-2" })
        {
            var slots = Battle.SlotsOf(f).ToArray();
            foreach (var p in Permutations(ids.ToList()))
                yield return p.Select((id, i) => new TeamSlot(id, slots[i])).ToList();
        }
    }

    static IEnumerable<List<string>> Permutations(List<string> a)
    {
        if (a.Count <= 1) { yield return new List<string>(a); yield break; }
        for (int i = 0; i < a.Count; i++)
        {
            var rest = a.Where((_, j) => j != i).ToList();
            foreach (var p in Permutations(rest)) { p.Insert(0, a[i]); yield return p; }
        }
    }

    static string FullReport(int teamCount, int runs)
    {
        var R = data.Rules;
        var teams = SampleTeams(teamCount);
        var o = new StringBuilder();
        o.Append("# Battle Balance Report\n\n");
        o.Append($"*Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by `dotnet run --project tools/BattleSim`. Rules: battle system draft 3 (doc 04) with the 10 factions and 10 types of doc 09. ");
        o.Append($"{teamCount} random 5-hero teams × {runs} fights per stage, auto-placed (tanks and warriors in front). Fight length assumes {R.SecondsPerActionAt1x.ToString(CultureInfo.InvariantCulture)}s per action at 1x.*\n\n");
        var flags = new List<string>();
        var overallLift = data.Heroes.ToDictionary(h => h.Id, h => 0.0);

        foreach (var st in data.Stages)
        {
            var target = R.DifficultyTargetsSeconds[st.Difficulty];
            var res = teams.AsParallel().AsOrdered().Select((t, i) => (T: t, M: RunMany(Place(t), st.Id, runs, 1000 + i * 31))).ToList();
            var wr = res.Select(r => r.M.WinRate).OrderBy(x => x).ToList();
            double median = wr[wr.Count / 2];
            double strong = (double)res.Count(r => r.M.WinRate >= 0.8) / res.Count;
            var winSecs = res.Where(r => r.M.AvgWinSeconds != null).Select(r => r.M.AvgWinSeconds.Value).OrderBy(x => x).ToList();
            double? medWin = winSecs.Count > 0 ? winSecs[winSecs.Count / 2] : (double?)null;
            double timeouts = res.Average(r => r.M.TimeoutRate);
            o.Append($"## {st.Name} ({st.Difficulty})\n\n");
            o.Append("| Measure | Value | Target |\n|---|---|---|\n");
            o.Append($"| Random teams that win at least 80% | {Pct(strong)} | at most {Pct(R.ReliableWinShareMax.TryGetValue(st.Difficulty, out var cap0) ? cap0 : 1)} |\n");
            o.Append($"| Median team win rate | {Pct(median)} | |\n");
            o.Append($"| Median winning fight length | {Mmss(medWin)} | {Mmss(target[0])} to {Mmss(target[1])} |\n");
            o.Append($"| Fights that hit the time limit | {Pct(timeouts)} | |\n");

            // Luck: the best sampled teams, re-run many times. A good team should win at least 85%.
            var best = res.OrderByDescending(r => r.M.WinRate).ThenBy(r => r.M.AvgSeconds).Take(3)
                .Select(r => (r.T, M: RunMany(Place(r.T), st.Id, 300, 555))).OrderByDescending(r => r.M.WinRate).ToList();
            o.Append($"| Best sampled team, re-run 300 times | {Pct(best[0].M.WinRate)} | at least 85% |\n\n");
            if (best[0].M.WinRate < 0.85) flags.Add($"**{st.Name}:** the best sampled team wins only {Pct(best[0].M.WinRate)} of 300 fights. Either the stage is too hard for this roster or luck decides too much.");

            // Placement: best vs worst arrangement of the same five heroes.
            var placementTeams = SampleTeams(8, 777 + st.Id.Length);
            var gaps = placementTeams.AsParallel().AsOrdered().Select(t =>
            {
                var rates = Arrangements(t).Select(a => (A: a, W: RunMany(a, st.Id, 4, 4242).WinRate)).ToList();
                var top = rates.OrderByDescending(x => x.W).First();
                var bottom = rates.OrderBy(x => x.W).First();
                var middle = rates.OrderBy(x => x.W).ElementAt(rates.Count / 2).W;
                var auto = RunMany(Place(t), st.Id, 4, 4242).WinRate;
                return (T: t, Best: top, Worst: bottom, Median: middle, Auto: auto);
            }).ToList();
            double avgGap = gaps.Average(g => g.Best.W - g.Worst.W);
            o.Append($"Placement (8 teams, all 240 arrangements each, both formations): best vs worst arrangement differ by {Pts(avgGap)} on average.\n\n");
            o.Append("| Team | Best arrangement | Best | Median | Worst | Auto-placed |\n|---|---|---|---|---|---|\n");
            foreach (var g in gaps) o.Append($"| {Names(g.T)} | {Slots(g.Best.A)} | {Pct(g.Best.W)} | {Pct(g.Median)} | {Pct(g.Worst.W)} | {Pct(g.Auto)} |\n");
            o.Append('\n');
            if (avgGap < 0.2 && median < 0.95) flags.Add($"**{st.Name}:** placement changes win rates by only {Pts(avgGap)} on average. Position should matter more.");

            o.Append("Best sampled teams (300 fights each):\n\n| Team | Win rate | Avg winning fight |\n|---|---|---|\n");
            foreach (var b in best) o.Append($"| {Names(b.T)} | {Pct(b.M.WinRate)} | {Mmss(b.M.AvgWinSeconds)} |\n");
            o.Append('\n');

            if (medWin != null && (medWin < target[0] || medWin > target[1]))
                flags.Add($"**{st.Name}:** winning fights take {Mmss(medWin)}, outside the {st.Difficulty} target of {Mmss(target[0])} to {Mmss(target[1])}.");
            if (R.ReliableWinShareMax.TryGetValue(st.Difficulty, out var cap) && strong > cap)
                flags.Add($"**{st.Name}:** {Pct(strong)} of random teams win reliably; the {st.Difficulty} limit is {Pct(cap)}, so it may be too easy.");

            double overall = res.Average(r => r.M.WinRate);
            var lift = data.Heroes.Select(h =>
            {
                var w = res.Where(r => r.T.Contains(h.Id)).ToList();
                double avg = w.Count > 0 ? w.Average(r => r.M.WinRate) : 0;
                return (h.Id, h.Name, Avg: avg, Lift: avg - overall);
            }).OrderByDescending(l => l.Lift).ToList();
            foreach (var l in lift) overallLift[l.Id] += l.Lift / data.Stages.Count;
            o.Append($"<details><summary>Win rate of teams that include each hero (average {Pct(overall)})</summary>\n\n| Hero | Win rate with hero | Difference |\n|---|---|---|\n");
            foreach (var l in lift) o.Append($"| {l.Name} | {Pct(l.Avg)} | {Pts(l.Lift)} |\n");
            o.Append("\n</details>\n\n");
            if (lift[0].Lift > 0.25) flags.Add($"**{st.Name}:** {lift[0].Name} looks like a must-have ({Pts(lift[0].Lift)}). Check that other answers exist.");
        }

        o.Append("## Heroes across all stages\n\nAverage difference in win rate when a hero is in the team. Big positive numbers suggest a hero is too strong, big negative ones too weak.\n\n| Hero | Faction | Type | Role | Difference |\n|---|---|---|---|---|\n");
        foreach (var kv in overallLift.OrderByDescending(k => k.Value))
        {
            var h = data.Hero(kv.Key);
            o.Append($"| {h.Name} | {h.Faction} | {h.Type} | {h.Role} | {Pts(kv.Value)} |\n");
            if (Math.Abs(kv.Value) > 0.12) flags.Add($"**{h.Name}:** {Pts(kv.Value)} on average across stages.");
        }
        o.Append('\n');
        o.Append("## Flags\n\n" + (flags.Count > 0 ? string.Join("\n", flags.Select(f => "- " + f)) : "- None.") + "\n");
        return o.ToString();
    }

    // One line per fight with every number the engine produces, to check that a seed always replays the same fight.
    static void Fingerprint(int teamCount)
    {
        var teams = SampleTeams(teamCount);
        string N(double x) => x.ToString("R", CultureInfo.InvariantCulture);
        foreach (var st in data.Stages)
            for (int i = 0; i < teams.Count; i++)
            {
                long seed = 1000 + i * 31;
                var r = Battle.Run(data, Place(teams[i]), st.Id, seed, options);
                var hs = string.Join(";", r.Heroes.Select(h => $"{h.Id}:{N(h.Dmg)}/{N(h.Heal)}/{N(h.Taken)}/{(h.DiedAt?.ToString() ?? "-")}"));
                Console.WriteLine($"{st.Id}|{seed}|{r.Result}|{r.Actions}|{r.Rituals}|{hs}");
            }
    }
}
