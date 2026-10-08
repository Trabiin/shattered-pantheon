// Battle simulator on the shared C# engine (the same files the Unity game compiles).
// Run from the repository root:
//   dotnet run --project tools/BattleSim                 -> full report, writes reports/latest.md
//   dotnet run --project tools/BattleSim -- --team hilde,solenne,thessaly,maren,pip --stage saint --runs 500
//   dotnet run --project tools/BattleSim -- --log hilde,solenne,thessaly,maren,pip --stage saint --seed 1   -> one fight, event by event
//   dotnet run --project tools/BattleSim -- --fingerprint 300  -> one line per fight; two runs must match (same seed, same fight)
// Options: --data <dir> (default Unity/Assets/Resources/BattleData), --out <file>, --teams N, --runs N.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredPantheon.Battle;

static class Program
{
    static GameData data;

    static int Main(string[] argv)
    {
        var args = new Dictionary<string, string>();
        for (int i = 0; i < argv.Length; i++)
            if (argv[i].StartsWith("--")) args[argv[i].Substring(2)] = i + 1 < argv.Length ? argv[i + 1] : null;
        string Arg(string k, string def) => args.TryGetValue(k, out var v) && v != null ? v : def;

        string root = args.ContainsKey("data") ? null : FindRoot();
        data = GameData.LoadDirectory(Arg("data", root != null ? Path.Combine(root, DataPath) : null));

        if (args.ContainsKey("fingerprint")) { Fingerprint(int.Parse(Arg("fingerprint", "300"))); return 0; }

        if (args.ContainsKey("log"))
        {
            // Play-by-play of one fight: the event stream the game client animates.
            var b = new Battle(data, Place(Arg("log", "").Split(',')), Arg("stage", "saint"), long.Parse(Arg("seed", "1")));
            while (!b.Over) b.Step();
            foreach (var e in b.Events) Console.WriteLine(e);
            return 0;
        }

        if (args.ContainsKey("team"))
        {
            string stage = Arg("stage", "saint");
            var r = RunMany(Arg("team", "").Split(','), stage, int.Parse(Arg("runs", "300")));
            Console.WriteLine($"Team {Arg("team", "")} vs {stage}: win {Pct(r.WinRate)}, avg fight {Mmss(r.AvgSeconds)}, avg win {Mmss(r.AvgWinSeconds)}, timeouts {Pct(r.TimeoutRate)}");
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

    static List<TeamSlot> Place(IList<string> ids) => Formation.AutoPlace(data, ids);

    class Many { public double WinRate, AvgSeconds, TimeoutRate; public double? AvgWinSeconds; }

    static Many RunMany(IList<string> ids, string stageId, int runs, long seedBase = 1)
    {
        var team = Place(ids);
        int wins = 0, timeouts = 0; double secs = 0, winSecs = 0;
        for (int i = 0; i < runs; i++)
        {
            var r = Battle.Run(data, team, stageId, seedBase + i * 7919L);
            secs += r.Seconds;
            if (r.Result == "win") { wins++; winSecs += r.Seconds; }
            if (r.Result == "timeout") timeouts++;
        }
        return new Many { WinRate = (double)wins / runs, AvgSeconds = secs / runs, AvgWinSeconds = wins > 0 ? winSecs / wins : (double?)null, TimeoutRate = (double)timeouts / runs };
    }

    // Number formatting that matches JavaScript's toFixed(0), Math.round and padStart.
    static string Fixed0(double x) => (x < 0 ? "-" : "") + Math.Round(Math.Abs(x), MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
    static string Pct(double x) => Fixed0(x * 100) + "%";
    static string Mmss(double? s)
    {
        if (s == null) return "n/a";
        double r = Battle.JsRound(s.Value);
        return $"{Math.Floor(r / 60)}:{(r % 60).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}";
    }

    // Same team sampler as sim.js (a double-precision LCG, so it must stay in doubles).
    static List<string[]> SampleTeams(int n)
    {
        var ids = data.Heroes.Select(h => h.Id).ToArray();
        double s = 12345;
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

    static readonly (string Name, string[] Team)[] Ref =
    {
        ("Balanced (Hilde, Solenne, Thessaly, Maren, Pip)", new[] { "hilde", "solenne", "thessaly", "maren", "pip" }),
        ("All damage (Solenne, Varkhul, Thessaly, Ysolde, Grub)", new[] { "solenne", "varkhul", "thessaly", "ysolde", "grub" }),
        ("No control (Hilde, Varkhul, Ysolde, Maren, Grub)", new[] { "hilde", "varkhul", "ysolde", "maren", "grub" }),
        ("Double control (Hilde, Solenne, Thessaly, Seraphine, Pip)", new[] { "hilde", "solenne", "thessaly", "seraphine", "pip" }),
    };

    static string FullReport(int teamCount, int runs)
    {
        var R = data.Rules;
        var teams = SampleTeams(teamCount);
        var o = new StringBuilder();
        o.Append($"# Battle Balance Report\n\n*Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by `dotnet run --project tools/BattleSim`. {teamCount} random 5-hero teams × {runs} fights per stage, auto-placed (tanks and melee in front). Fight length assumes {R.SecondsPerActionAt1x.ToString(CultureInfo.InvariantCulture)}s per action at 1x.*\n\n");
        var flags = new List<string>();
        foreach (var st in data.Stages)
        {
            var target = R.DifficultyTargetsSeconds[st.Difficulty];
            var res = teams.Select((t, i) => (T: t, M: RunMany(t, st.Id, runs, 1000 + i * 31))).ToList();
            var wr = res.Select(r => r.M.WinRate).OrderBy(x => x).ToList();
            double median = wr[wr.Count / 2];
            double strong = (double)res.Count(r => r.M.WinRate >= 0.8) / res.Count;
            var winSecs = res.Where(r => r.M.AvgWinSeconds != null).Select(r => r.M.AvgWinSeconds.Value).OrderBy(x => x).ToList();
            double? medWin = winSecs.Count > 0 ? winSecs[winSecs.Count / 2] : (double?)null;
            o.Append($"## {st.Name} ({st.Difficulty})\n\n");
            o.Append("| Measure | Value | Target |\n|---|---|---|\n");
            o.Append($"| Random teams that win at least 80% | {Pct(strong)} | |\n| Median team win rate | {Pct(median)} | |\n");
            o.Append($"| Median winning fight length | {Mmss(medWin)} | {Mmss(target[0])} to {Mmss(target[1])} |\n\n");
            if (medWin != null && (medWin < target[0] || medWin > target[1]))
                flags.Add($"**{st.Name}:** winning fights take {Mmss(medWin)}, outside the {st.Difficulty} target of {Mmss(target[0])} to {Mmss(target[1])}.");
            if (R.ReliableWinShareMax.TryGetValue(st.Difficulty, out var cap) && strong > cap)
                flags.Add($"**{st.Name}:** {Pct(strong)} of random teams win reliably; the {st.Difficulty} limit is {Pct(cap)}, so it may be too easy.");
            if (strong < 0.05)
                flags.Add($"**{st.Name}:** fewer than 5% of random teams reliably win. Fine for hard content only if well-built teams do (see reference teams).");
            double overall = res.Sum(r => r.M.WinRate) / res.Count;
            var lift = data.Heroes.Select(h =>
            {
                var w = res.Where(r => r.T.Contains(h.Id)).ToList();
                double avg = w.Sum(r => r.M.WinRate) / w.Count;
                return (h.Id, h.Name, Avg: avg, Lift: avg - overall);
            }).OrderByDescending(l => l.Lift).ToList();
            o.Append($"Win rate of teams that include each hero (overall average {Pct(overall)}):\n\n| Hero | Win rate with hero | Difference |\n|---|---|---|\n");
            foreach (var l in lift) o.Append($"| {l.Name} | {Pct(l.Avg)} | {(l.Lift >= 0 ? "+" : "")}{Fixed0(l.Lift * 100)} pts |\n");
            o.Append('\n');
            if (lift[0].Lift > 0.25) flags.Add($"**{st.Name}:** {lift[0].Name} looks like a must-have (+{Fixed0(lift[0].Lift * 100)} pts). Check that other answers exist.");
            o.Append("Reference teams (200 fights each):\n\n| Team | Win rate | Avg winning fight |\n|---|---|---|\n");
            foreach (var (name, t) in Ref) { var r = RunMany(t, st.Id, 200, 77); o.Append($"| {name} | {Pct(r.WinRate)} | {Mmss(r.AvgWinSeconds)} |\n"); }
            o.Append('\n');
        }
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
                var r = Battle.Run(data, Place(teams[i]), st.Id, seed);
                var hs = string.Join(";", r.Heroes.Select(h => $"{h.Id}:{N(h.Dmg)}/{N(h.Heal)}/{N(h.Taken)}/{(h.DiedAt?.ToString() ?? "-")}"));
                Console.WriteLine($"{st.Id}|{seed}|{r.Result}|{r.Actions}|{r.Rituals}|{hs}");
            }
    }
}
