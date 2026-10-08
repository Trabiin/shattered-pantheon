// Progression and economy simulator (plan docs 07, 08, 10). Simulates players day by day through the
// generated campaign, fighting every campaign battle on the real battle engine.
// From the repository root:
//   dotnet run --project tools/ProgressionSim -c Release                 -> report to reports/progression.md
//   dotnet run --project tools/ProgressionSim -c Release -- --days 120 --seeds 4 --out p.md   (defaults: 365 days, 6 players per type)
//   dotnet run --project tools/ProgressionSim -c Release -- --build-campaign  -> rebuild and sim-check campaign.json
//   dotnet run --project tools/ProgressionSim -c Release -- --check      -> win rate at exactly the recommended power
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
    const string DataPath = "Unity/Assets/Resources/BattleData";

    static int Main(string[] argv)
    {
        var args = new Dictionary<string, string>();
        for (int i = 0; i < argv.Length; i++)
            if (argv[i].StartsWith("--")) args[argv[i].Substring(2)] = i + 1 < argv.Length && !argv[i + 1].StartsWith("--") ? argv[i + 1] : null;
        string Arg(string k, string def) => args.TryGetValue(k, out var v) && v != null ? v : def;

        string root = FindRoot();
        var baseData = GameData.LoadDirectory(Path.Combine(root, DataPath));
        var cfg = Cfg.Load(Path.Combine(root, DataPath, "progression.json"));
        // The campaign: built by recipe and sim-checked once (--build-campaign), then read from campaign.json.
        string campaignPath = Path.Combine(root, DataPath, "campaign.json");
        var data = new GameData { Rules = baseData.Rules, Heroes = baseData.Heroes, Enemies = baseData.Enemies };
        if (args.ContainsKey("build-campaign") || !File.Exists(campaignPath))
        {
            data.Stages = cfg.BuildCampaign();
            cfg.Calibrate(data, data.Stages);
            File.WriteAllText(campaignPath, Cfg.ToJson(data.Stages));
            Console.Error.WriteLine($"Wrote {data.Stages.Count} sim-checked campaign battles to {campaignPath}");
            if (args.ContainsKey("build-campaign")) return 0;
        }
        else
        {
            var dir = Path.Combine(root, DataPath);
            data.Stages = GameData.FromJson(File.ReadAllText(Path.Combine(dir, "rules.json")), "[]", "[]", File.ReadAllText(campaignPath)).Stages;
        }
        var cards = Cards(cfg, data);

        if (args.ContainsKey("check")) { Check(cfg, data, cards); return 0; }

        int days = int.Parse(Arg("days", "365")), seeds = int.Parse(Arg("seeds", "6"));
        var jobs = (from t in cfg.Times from s in cfg.Spends from k in Enumerable.Range(0, seeds) select (t, s, k)).ToList();
        var players = new Player[jobs.Count];
        Parallel.For(0, jobs.Count, i =>
        {
            var (t, s, k) = jobs[i];
            var p = new Player(cfg, data, cards, t, s, 1000 + k);
            for (int d = 1; d <= days; d++) p.PlayDay(d);
            players[i] = p;
        });
        string report = Report(cfg, players, days, seeds, cards.Count);
        string outPath = Arg("out", Path.Combine(root, "reports", "progression.md"));
        File.WriteAllText(outPath, report);
        Console.WriteLine(report);
        Console.Error.WriteLine($"{players.Sum(p => p.RealFights)} campaign fights simulated");
        return 0;
    }

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, DataPath, "progression.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Run from inside the repository");
    }

    // The simulator's roster: every test kit under one name per rarity listed in progression.json.
    static List<HeroCard> Cards(Cfg c, GameData data)
    {
        var list = new List<HeroCard>();
        for (int i = 0; i < data.Heroes.Count && i < c.Copies.Count; i++)
        {
            var h = data.Heroes[i];
            for (int j = 0; j < c.Copies[i].Length; j++)
                list.Add(new HeroCard
                {
                    Id = j == 0 ? h.Id : $"{h.Id}-{c.Copies[i][j].ToLowerInvariant()}", Kit = h.Id, Rarity = c.Copies[i][j],
                    Name = h.Name + (j == 0 ? "" : $" ({c.Copies[i][j]})"), Faction = h.Faction, Role = h.Role,
                    KitPower = Math.Sqrt(h.Stats.Get("hp") * h.Stats.Get("atk")),
                });
        }
        return list;
    }

    // Win rate of random Epic teams at exactly the recommended power, per difficulty and stage band.
    static void Check(Cfg c, GameData data, List<HeroCard> cards)
    {
        var rng = new Random(5);
        for (int d = 0; d < c.Difficulties.Count; d++)
            foreach (int k in new[] { 5, 9, 55, 99, 155, 199, 255, 299, 355, 399 })
            {
                int wins = 0, n = 60; double secs = 0;
                double scale = c.RecScale(d, k);
                for (int i = 0; i < n; i++)
                {
                    var ids = data.Heroes.OrderBy(_ => rng.Next()).Take(5).Select(h => h.Id).ToList();
                    var team = Formation.AutoPlace(data, ids);
                    foreach (var s in team) s.HpScale = s.AtkScale = scale;
                    var r = Battle.Run(data, team, Cfg.StageId(d, k), i + 1);
                    if (r.Result == "win") { wins++; secs += r.Seconds; }
                }
                Console.WriteLine($"{c.Difficulties[d].Name,-10} battle {k + 1,3}: win {100 * wins / n,3}% at recommended power, avg win {Mmss(wins > 0 ? secs / wins : 0)}");
            }
    }

    // ---------- Report ----------

    static string Mmss(double s) => $"{(int)(s / 60)}:{((int)s % 60):00}";
    static string F(double x) => x.ToString("0", CultureInfo.InvariantCulture);
    static double Median(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToArray(); return a.Length == 0 ? double.NaN : a[a.Length / 2]; }
    static string Day(double d) => double.IsNaN(d) || d < 0 ? "not reached" : "day " + F(d);

    static string Position(Cfg c, int diff, int battle) => diff >= c.Difficulties.Count ? "all done" : $"{c.Difficulties[diff].Name} {battle / c.BattlesPerStage + 1}-{battle % c.BattlesPerStage + 1}";

    static string Report(Cfg c, Player[] players, int days, int seeds, int rosterSize)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Progression Report");
        sb.AppendLine();
        sb.AppendLine($"*Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by `dotnet run --project tools/ProgressionSim -c Release`. {players.Length / seeds} player types × {seeds} players each, {days} days. Every campaign battle is a real fight on the battle engine ({players.Sum(p => p.RealFights):N0} fights); rewards, upgrades, summons and star challenges follow `progression.json`. Roster: the 20 test kits as {rosterSize} heroes (Rare, Epic and Legendary versions). Numbers are medians across the players of each type.*");
        sb.AppendLine();

        var groups = players.GroupBy(p => p.Name).ToList();
        var flags = new List<string>();

        // Milestones
        string[] milestones = { "Normal 10", "Normal 20", "Normal 40", "Hard 20", "Hard 40", "Nightmare 40", "Godless 40" };
        sb.AppendLine("## When each player type gets there");
        sb.AppendLine();
        sb.AppendLine("| Player | " + string.Join(" | ", milestones) + " | Where on day " + days + " |");
        sb.AppendLine("|---|" + string.Concat(milestones.Select(_ => "---|")) + "---|");
        var reach = new Dictionary<(string, string), double>();
        foreach (var g in groups)
        {
            var cells = new List<string>();
            foreach (var m in milestones)
            {
                var ds = g.Select(p => p.DoneDay.TryGetValue(m, out var d) ? d : double.NaN).ToList();
                int got = ds.Count(x => !double.IsNaN(x));
                double med = got * 2 > ds.Count ? Median(ds.Where(x => !double.IsNaN(x)).Concat(Enumerable.Repeat(double.MaxValue, ds.Count - got))) : double.NaN;
                if (med == double.MaxValue) med = double.NaN;
                reach[(g.Key, m)] = med;
                cells.Add(double.IsNaN(med) ? "–" : F(med));
            }
            var last = g.OrderBy(p => p.Diff * 1000 + p.Battle).ElementAt(g.Count() / 2);
            sb.AppendLine($"| {g.Key} | {string.Join(" | ", cells)} | {Position(c, last.Diff, last.Battle)} |");
        }
        sb.AppendLine();
        sb.AppendLine("Targets (doc 10 section 9): Normal 40 by day " + string.Join(", ", c.NormalDays.Select(kv => $"{F(kv.Value)} ({kv.Key})")) + "; Hard 40 by day " + string.Join(", ", c.HardDays.Select(kv => $"{F(kv.Value)} ({kv.Key})")) + ". A dash means most players of that type hadn't reached it by day " + days + ".");
        sb.AppendLine();

        foreach (var t in c.Times)
        {
            double n = reach[($"{t.Name} Free", "Normal 40")], h = reach[($"{t.Name} Free", "Hard 40")];
            double tn = c.NormalDays[t.Name], th = c.HardDays[t.Name];
            if (double.IsNaN(n) || n > tn * 1.25) flags.Add($"**{t.Name} Free** finishes Normal on {Day(n)}; the target is about day {F(tn)}.");
            else if (n < tn * 0.75) flags.Add($"**{t.Name} Free** finishes Normal on day {F(n)}, much sooner than the target of about day {F(tn)}.");
            if (days >= th * 1.25 && (double.IsNaN(h) || h > th * 1.25)) flags.Add($"**{t.Name} Free** finishes Hard on {Day(h)}; the target is about day {F(th)}.");
            else if (!double.IsNaN(h) && h < th * 0.75) flags.Add($"**{t.Name} Free** finishes Hard on day {F(h)}, much sooner than the target of about day {F(th)}.");
            double ln = reach[($"{t.Name} Light", "Normal 40")];
            if (!double.IsNaN(n) && !double.IsNaN(ln))
            {
                double faster = 1 - ln / n;
                if (faster < c.LightFaster[0] - 0.1 || faster > c.LightFaster[1] + 0.1) flags.Add($"**{t.Name}:** a light spender finishes Normal {F(faster * 100)}% sooner than a free player; the target is {F(c.LightFaster[0] * 100)} to {F(c.LightFaster[1] * 100)}%.");
            }
        }

        // Rhythm
        sb.AppendLine("## Reward rhythm");
        sb.AppendLine();
        sb.AppendLine("| Player | First Legendary | Longest gap between new heroes | Longest gap without a big moment | Longest wall (Normal, Hard) | Walls over " + F(c.WallDays) + " days | Longest wall (Nightmare, Godless) | Godshards per day (first 30 days) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var g in groups)
        {
            double leg = Median(g.Select(p => (double)p.FirstLegendaryDay));
            double heroGap = Median(g.Select(p => (double)LongestGap(p.Days, d => d.NewHero, d => d.Roster >= rosterSize)));
            double bigGap = Median(g.Select(p => (double)LongestGap(p.Days, d => d.BigMoment, d => d.Difficulty >= c.Difficulties.Count)));
            // Walls count on Normal and Hard; Nightmare and Godless are meant to be a long climb.
            double wall = Median(g.Select(p => (double)LongestGap(p.Days, d => d.Progressed, d => d.Difficulty >= 2)));
            double walls = Median(g.Select(p => (double)Walls(p.Days, c.WallDays, 2).Count));
            double lateWall = Median(g.Select(p => (double)LongestGap(p.Days.Where(d => d.Difficulty >= 2).ToList(), d => d.Progressed, d => d.Difficulty >= c.Difficulties.Count)));
            double gpd = Median(g.Select(p => p.Days.Take(30).Average(d => d.GodshardsEarned)));
            sb.AppendLine($"| {g.Key} | {Day(leg)} | {F(heroGap)} days | {F(bigGap)} days | {F(wall)} days | {F(walls)} | {F(lateWall)} days | {F(gpd)} |");
            if (leg < 0 || leg > c.FirstLegendaryDay) flags.Add($"**{g.Key}:** first Legendary on {Day(leg)}; the target is day {F(c.FirstLegendaryDay)}.");
            if (bigGap > c.MaxDaysWithoutBig) flags.Add($"**{g.Key}:** up to {F(bigGap)} days in a row without a big moment; the target is at most {F(c.MaxDaysWithoutBig)}.");
            if (wall > c.WallDays) flags.Add($"**{g.Key}:** stuck on one battle for up to {F(wall)} days; the limit is {F(c.WallDays)}.");
        }
        sb.AppendLine();
        sb.AppendLine("A **big moment** is a new Epic or Legendary hero, a stage chest, a star chest, an ascension or a large Codex reward. A **wall** is a stretch of days with no new campaign battle cleared.");
        sb.AppendLine();

        // Where the walls are
        var wallSpots = players.Where(p => p.Name.EndsWith("Free")).SelectMany(p => Walls(p.Days, c.WallDays, 2)).GroupBy(w => w.Item1).OrderByDescending(x => x.Count()).Take(6).ToList();
        if (wallSpots.Count > 0)
        {
            sb.AppendLine("Free players' walls by difficulty and stage band: " + string.Join(", ", wallSpots.Select(x => $"{x.Key} ({x.Count()})")) + ".");
            sb.AppendLine();
        }

        // Energy
        sb.AppendLine("## Energy (Devotion)");
        sb.AppendLine();
        sb.AppendLine("| Player | Days out of energy with time left (while campaign remains) | First day out | Farm fights per day | Campaign fights per day (first 30 days) |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var g in groups)
        {
            double share = Median(g.Select(p => p.Days.Count(d => d.OutOfEnergy && d.Difficulty < c.Difficulties.Count) / (double)Math.Max(1, p.Days.Count(d => d.Difficulty < c.Difficulties.Count))));
            double first = Median(g.Select(p => (double)(p.Days.FirstOrDefault(d => d.OutOfEnergy && d.Difficulty < c.Difficulties.Count)?.Day ?? -1)));
            double farm = Median(g.Select(p => p.Days.Average(d => d.FarmFights)));
            double fights = Median(g.Select(p => p.Days.Take(30).Average(d => d.Fights)));
            sb.AppendLine($"| {g.Key} | {F(share * 100)}% | {Day(first)} | {farm:0.0} | {fights:0.0} |");
        }
        sb.AppendLine();
        sb.AppendLine("Campaign fights never cost energy (doc 10 section 4). Energy runs out only when a player has time left after the campaign and the daily round and wants to keep farming.");
        sb.AppendLine();

        // Trajectory
        foreach (var name in new[] { "Regular Free", "Casual Free" })
        {
            var p = groups.First(g => g.Key == name).OrderBy(x => x.Diff * 1000 + x.Battle).ElementAt(seeds / 2);
            sb.AppendLine($"## One {name.ToLowerInvariant()} player, day by day");
            sb.AppendLine();
            sb.AppendLine("| Day | Next battle | Team level | Star rank | Gear tier | Skill level | Team power vs recommended | Heroes | Stars earned | Godshards earned that day |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (int d in new[] { 1, 2, 3, 5, 7, 10, 14, 21, 30, 45, 60, 90, 120, 150, 180, 210, 240, 300, 365 })
            {
                if (d > p.Days.Count) break;
                var x = p.Days[d - 1];
                sb.AppendLine($"| {d} | {Position(c, x.Difficulty, x.Battle)} | {x.TeamLevel} | {x.TeamStars:0.0} | {x.TeamGear:0.0} | {x.TeamSkill:0.0} | {F(x.TeamScale / x.RecScale * 100)}% | {x.Roster} | {x.Stars} | {F(x.GodshardsEarned)} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Flags");
        sb.AppendLine();
        if (flags.Count == 0) sb.AppendLine("- None.");
        foreach (var f in flags) sb.AppendLine("- " + f);
        return sb.ToString();
    }

    // Longest run of days where `hit` is false, ignoring days where `done` is true.
    static int LongestGap(List<DayLog> days, Func<DayLog, bool> hit, Func<DayLog, bool> done)
    {
        int best = 0, run = 0;
        foreach (var d in days)
        {
            if (done(d)) { run = 0; continue; }
            if (hit(d)) run = 0; else best = Math.Max(best, ++run);
        }
        return best;
    }

    static List<(string, int)> Walls(List<DayLog> days, double limit, int difficulties)
    {
        var list = new List<(string, int)>();
        int run = 0;
        foreach (var d in days)
        {
            if (d.Difficulty >= difficulties) break;
            if (d.Progressed) run = 0;
            else if (++run == (int)limit + 1) list.Add(($"{new[] { "Normal", "Hard", "Nightmare", "Godless" }[d.Difficulty]} {d.Battle / 100 * 10 + 1}-{d.Battle / 100 * 10 + 10}", run));
        }
        return list;
    }
}
