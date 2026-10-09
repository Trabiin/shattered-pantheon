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
        if (args.ContainsKey("lift")) { foreach (var kv in KitLift(cfg, data).OrderByDescending(x => x.Value)) Console.WriteLine($"{kv.Key,-10} {kv.Value:0.00}"); return 0; }
        if (args.ContainsKey("probe")) { Probe(cfg, data, args["probe"], args.TryGetValue("team", out var tm) && tm != null ? tm.Split(',').ToList() : null); return 0; }

        int days = int.Parse(Arg("days", "365")), seeds = int.Parse(Arg("seeds", "6"));
        var jobs = (from t in cfg.Times from s in cfg.Spends from k in Enumerable.Range(0, seeds) select (t, s, k)).ToList();
        if (args.TryGetValue("only", out var only) && only != null) jobs = jobs.Where(j => only.Split(',').Contains($"{j.t.Name} {j.s.Name}")).ToList();
        var players = new Player[jobs.Count];
        Parallel.For(0, jobs.Count, i =>
        {
            var (t, s, k) = jobs[i];
            var p = new Player(cfg, data, cards, t, s, 1000 + k);
            for (int d = 1; d <= days; d++) p.PlayDay(d);
            players[i] = p;
        });
        if (args.ContainsKey("only"))
        {
            // Quick look while tuning: the day each player finished each difficulty.
            foreach (var p in players)
                Console.Error.WriteLine($"{p.Name} {string.Join(" ", Enumerable.Range(1, cfg.Difficulties.Count).Select(d => p.Days.FirstOrDefault(x => x.Difficulty >= d)?.Day.ToString() ?? "-"))}");
            return 0;
        }
        string report = Report(cfg, players, days, seeds, cards.Count(h => h.ReleaseDay == 0));
        string outPath = Arg("out", Path.Combine(root, "reports", "progression.md"));
        File.WriteAllText(outPath, report);
        Console.WriteLine(report);
        Console.Error.WriteLine($"{players.Sum(p => p.RealFights)} campaign fights simulated, plus {players.Sum(p => p.PracticeFights)} practice fights");
        return 0;
    }

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, DataPath, "progression.json"))) return d.FullName;
        throw new DirectoryNotFoundException("Run from inside the repository");
    }

    // The simulator's roster: the launch heroes, then new ones every month for a year. Each card uses
    // one of the test kits; kits are dealt out in turn so each kit appears at several rarities.
    static List<HeroCard> Cards(Cfg c, GameData data)
    {
        var lift = KitLift(c, data);
        var list = new List<HeroCard>();
        int next = 0;
        void Make(string rarity, int day)
        {
            var h = data.Heroes[next++ % data.Heroes.Count];
            list.Add(new HeroCard
            {
                Id = $"{h.Id}-{rarity.ToLowerInvariant()}-{list.Count}", Kit = h.Id, Rarity = rarity, ReleaseDay = day,
                Name = $"{h.Name} ({rarity})", Faction = h.Faction, Role = h.Role,
                KitPower = lift[h.Id],
            });
        }
        foreach (var r in new[] { c.StarterRarity }.Concat(c.Rarities.Where(x => x != c.StarterRarity)))
            for (int i = 0; i < c.Launch[r]; i++) Make(r, 0);
        for (int day = c.ReleaseEvery; day <= 400; day += c.ReleaseEvery)
            foreach (var r in c.Release) Make(r, day);
        return list;
    }

    // How much each kit lifts a team's win rate across the campaign: random teams at the recommended
    // power on a spread of battles, win rate with the kit divided by the overall win rate. Simulated
    // players pick their teams by this times hero growth, the way a real player learns who's good.
    static Dictionary<string, double> KitLift(Cfg c, GameData data)
    {
        var rng = new Random(23);
        var with = data.Heroes.ToDictionary(h => h.Id, _ => 0.0);
        var seen = data.Heroes.ToDictionary(h => h.Id, _ => 0.0);
        int wins = 0, n = 3000;
        for (int i = 0; i < n; i++)
        {
            int d = rng.Next(c.Difficulties.Count), k = rng.Next(c.BattlesPerDifficulty);
            var ids = data.Heroes.OrderBy(_ => rng.Next()).Take(5).Select(h => h.Id).ToList();
            var team = Formation.AutoPlace(data, ids);
            foreach (var s in team) s.HpScale = s.AtkScale = c.RecScale(d, k);
            bool win = Battle.Run(data, team, Cfg.StageId(d, k), i + 1).Result == "win";
            if (win) wins++;
            foreach (var id in ids) { seen[id]++; if (win) with[id]++; }
        }
        double overall = (double)wins / n;
        return data.Heroes.ToDictionary(h => h.Id, h => with[h.Id] / Math.Max(1, seen[h.Id]) / overall);
    }

    // Win rate of random teams on one battle at 90 to 120% of its recommended power, e.g. --probe c3-167.
    static void Probe(Cfg c, GameData data, string id, List<string> fixedTeam = null)
    {
        var parts = id.Substring(1).Split('-');
        int d = int.Parse(parts[0]), k = int.Parse(parts[1]);
        foreach (double m in new[] { 0.9, 1.0, 1.1, 1.2 })
        {
            var rng = new Random(11);
            int wins = 0, n = 200;
            for (int i = 0; i < n; i++)
            {
                var ids = fixedTeam ?? data.Heroes.OrderBy(_ => rng.Next()).Take(5).Select(h => h.Id).ToList();
                var team = Formation.AutoPlace(data, ids);
                foreach (var s in team) s.HpScale = s.AtkScale = c.RecScale(d, k) * m;
                if (Battle.Run(data, team, id, i + 1).Result == "win") wins++;
            }
            Console.WriteLine($"{id} at {m * 100:0}% of recommended power: {(fixedTeam == null ? "random teams" : string.Join(",", fixedTeam))} win {100 * wins / n}%");
        }
    }

    // Win rate of random Epic teams at exactly the recommended power, a few battles and bosses per difficulty.
    static void Check(Cfg c, GameData data, List<HeroCard> cards)
    {
        var rng = new Random(5);
        for (int d = 0; d < c.Difficulties.Count; d++)
            foreach (int k in new[] { 5, 7, 39, 157, 199, 357, 399 })
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
                Console.WriteLine($"{c.Difficulties[d].Name,-10} {c.BattleName(k),-14}{(c.IsRealmBoss(k) ? " realm boss" : c.IsBoss(k) ? " boss" : ""),-11}: win {100 * wins / n,3}% at recommended power, avg win {Mmss(wins > 0 ? secs / wins : 0)}");
            }
    }

    // ---------- Report ----------

    static string Mmss(double s) => $"{(int)(s / 60)}:{((int)s % 60):00}";
    static string F(double x) => x.ToString("0", CultureInfo.InvariantCulture);
    static double Median(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToArray(); return a.Length == 0 ? double.NaN : a[a.Length / 2]; }
    static string Day(double d) => double.IsNaN(d) || d < 0 ? "not reached" : "day " + F(d);

    static string Position(Cfg c, int diff, int battle) => diff >= c.Difficulties.Count ? "all done" : $"{c.Difficulties[diff].Name} {c.BattleName(battle)}";

    static string Report(Cfg c, Player[] players, int days, int seeds, int rosterSize)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Progression Report");
        sb.AppendLine();
        sb.AppendLine($"*Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by `dotnet run --project tools/ProgressionSim -c Release`. {players.Length / seeds} player types × {seeds} players each, {days} days. Every campaign battle is a real fight on the battle engine ({players.Sum(p => p.RealFights):N0} fights); rewards, upgrades, summons and star challenges follow `progression.json`. Roster: the 20 test kits stand in for {rosterSize} launch heroes ({string.Join(", ", c.Rarities.Select(r => $"{c.Launch[r]} {r}"))}), plus {string.Join(", ", c.Release.GroupBy(x => x).Select(g => $"{g.Count()} {g.Key}"))} every {c.ReleaseEvery} days. Numbers are medians across the players of each type.*");
        sb.AppendLine();

        var groups = players.GroupBy(p => p.Name).ToList();
        var flags = new List<string>();

        // Milestones
        string[] milestones = { "Normal realm 3", "Normal realm 5", "Normal realm 10", "Hard realm 5", "Hard realm 10", "Nightmare realm 10", "Godless realm 10" };
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
        sb.AppendLine("Targets (doc 10 section 9): Normal done (realm 10) by day " + string.Join(", ", c.NormalDays.Select(kv => $"{F(kv.Value)} ({kv.Key})")) + "; Hard done by day " + string.Join(", ", c.HardDays.Select(kv => $"{F(kv.Value)} ({kv.Key})")) + ". A dash means most players of that type hadn't reached it by day " + days + ".");
        sb.AppendLine();

        foreach (var t in c.Times)
        {
            double n = reach[($"{t.Name} Free", "Normal realm 10")], h = reach[($"{t.Name} Free", "Hard realm 10")];
            double tn = c.NormalDays[t.Name], th = c.HardDays[t.Name];
            if (double.IsNaN(n) || n > tn * 1.25) flags.Add($"**{t.Name} Free** finishes Normal on {Day(n)}; the target is about day {F(tn)}.");
            else if (n < tn * 0.75) flags.Add($"**{t.Name} Free** finishes Normal on day {F(n)}, much sooner than the target of about day {F(tn)}.");
            if (days >= th * 1.25 && (double.IsNaN(h) || h > th * 1.25)) flags.Add($"**{t.Name} Free** finishes Hard on {Day(h)}; the target is about day {F(th)}.");
            else if (!double.IsNaN(h) && h < th * 0.75) flags.Add($"**{t.Name} Free** finishes Hard on day {F(h)}, much sooner than the target of about day {F(th)}.");
            double ln = reach.TryGetValue(($"{t.Name} Light", "Normal realm 10"), out var lv) ? lv : double.NaN;
            if (!double.IsNaN(n) && !double.IsNaN(ln))
            {
                double faster = 1 - ln / n;
                if (faster < c.LightFaster[0] - 0.1 || faster > c.LightFaster[1] + 0.1) flags.Add($"**{t.Name}:** a light spender finishes Normal {F(faster * 100)}% sooner than a free player; the target is {F(c.LightFaster[0] * 100)} to {F(c.LightFaster[1] * 100)}%.");
            }
        }

        // Playing longer should pay (Ojon, 2026-10-08): each step up in daily time gets there clearly sooner.
        foreach (var sp in c.Spends.Take(1))
            for (int i = 1; i < c.Times.Count; i++)
                foreach (var m in new[] { "Normal realm 10", "Hard realm 10", "Nightmare realm 10" })
                {
                    double slow = reach[($"{c.Times[i - 1].Name} {sp.Name}", m)], fast = reach[($"{c.Times[i].Name} {sp.Name}", m)];
                    if (double.IsNaN(fast)) continue;
                    double ratio = double.IsNaN(slow) ? days / fast : slow / fast;
                    if (ratio < c.TimePays) flags.Add($"**Time:** {c.Times[i].Name} free players reach {m} only {ratio:0.00}× as fast as {c.Times[i - 1].Name} ones; the target is at least {c.TimePays:0.0}×.");
                }

        // Late spender gap (Ojon, 2026-10-09): spending speeds up the late game, but not by more than this.
        foreach (var t in c.Times)
            foreach (var sp in c.Spends.Skip(1))
                foreach (var m in new[] { "Nightmare realm 10", "Godless realm 10" })
                {
                    double free = reach[($"{t.Name} {c.Spends[0].Name}", m)], paid = reach[($"{t.Name} {sp.Name}", m)];
                    if (double.IsNaN(paid)) continue;
                    double ratio = double.IsNaN(free) ? days / paid : free / paid;
                    if (c.SpendGapLate.TryGetValue(sp.Name, out var lim) && ratio > lim) flags.Add($"**Spending:** {t.Name} {sp.Name} players reach {m} {ratio:0.00}× as fast as free ones; the limit is {lim:0.0}×.");
                }

        // Rhythm
        sb.AppendLine("## Reward rhythm");
        sb.AppendLine();
        sb.AppendLine("| Player | First Legendary | Longest gap between new heroes | Longest gap without a big moment | Longest wall (Normal, Hard) | Walls over " + F(c.WallDays) + " days | Longest wall (Nightmare, Godless) | Godshards per day (first 30 days) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var g in groups)
        {
            double leg = Median(g.Select(p => (double)p.FirstLegendaryDay));
            double heroGap = Median(g.Select(p => (double)LongestGap(p.Days, d => d.NewHero, d => d.Roster >= d.Released)));
            double bigGap = Median(g.Select(p => (double)LongestGap(p.Days, d => d.BigMoment, d => d.Difficulty >= c.Difficulties.Count)));
            // Walls count on Normal and Hard; Nightmare and Godless are meant to be a long climb.
            double wall = Median(g.Select(p => (double)LongestGap(p.Days, d => d.Progressed, d => d.Difficulty >= 2)));
            double walls = Median(g.Select(p => (double)Walls(c, p.Days, c.WallDays, 2).Count));
            double lateWall = Median(g.Select(p => (double)LongestGap(p.Days.Where(d => d.Difficulty >= 2).ToList(), d => d.Progressed, d => d.Difficulty >= c.Difficulties.Count)));
            double gpd = Median(g.Select(p => p.Days.Take(30).Average(d => d.GodshardsEarned)));
            sb.AppendLine($"| {g.Key} | {Day(leg)} | {F(heroGap)} days | {F(bigGap)} days | {F(wall)} days | {F(walls)} | {F(lateWall)} days | {F(gpd)} |");
            if (leg < 0 || leg > c.FirstLegendaryDay) flags.Add($"**{g.Key}:** first Legendary on {Day(leg)}; the target is day {F(c.FirstLegendaryDay)}.");
            if (bigGap > c.MaxDaysWithoutBig) flags.Add($"**{g.Key}:** up to {F(bigGap)} days in a row without a big moment; the target is at most {F(c.MaxDaysWithoutBig)}.");
            if (wall > c.WallDays) flags.Add($"**{g.Key}:** stuck on one battle for up to {F(wall)} days; the limit is {F(c.WallDays)}.");
            if (lateWall > c.LateWallDays) flags.Add($"**{g.Key}:** stuck on one Nightmare or Godless battle for up to {F(lateWall)} days; the limit is {F(c.LateWallDays)}.");
        }
        sb.AppendLine();
        sb.AppendLine("A **big moment** is a new Epic or Legendary hero, a stage chest, a star chest, an ascension or a large Codex reward. A **wall** is a stretch of days with no new campaign battle cleared.");
        sb.AppendLine();

        // Where the walls are
        var wallSpots = players.Where(p => p.Name.EndsWith("Free")).SelectMany(p => Walls(c, p.Days, c.WallDays, 2)).GroupBy(w => w.Item1).OrderByDescending(x => x.Count()).Take(6).ToList();
        if (wallSpots.Count > 0)
        {
            sb.AppendLine("Free players' walls by difficulty and realm: " + string.Join(", ", wallSpots.Select(x => $"{x.Key} ({x.Count()})")) + ".");
            sb.AppendLine();
        }

        // Collection
        int[] cDays = { 7, 30, 90, 180, 365 };
        sb.AppendLine("## Collection");
        sb.AppendLine();
        sb.AppendLine("Share of the released roster owned (it grows every month), then Epics and Legendaries owned of those released. Commons and Uncommons are all owned within days; the chase is the Epics and Legendaries.");
        sb.AppendLine();
        sb.AppendLine("| Player | " + string.Join(" | ", cDays.Where(d => d <= days).Select(d => "Day " + d)) + " | Summons per day, month 1 | Summons per day, later |");
        sb.AppendLine("|---|" + string.Concat(cDays.Where(d => d <= days).Select(_ => "---|")) + "---|---|");
        foreach (var g in groups)
        {
            var cells = cDays.Where(d => d <= days).Select(d =>
            {
                double share = Median(g.Select(p => (double)p.Days[d - 1].Roster / p.Days[d - 1].Released));
                double leg = Median(g.Select(p => (double)p.Days[d - 1].Legendaries)), legR = g.First().Days[d - 1].LegendariesReleased;
                double ep = Median(g.Select(p => (double)p.Days[d - 1].Epics)), epR = g.First().Days[d - 1].EpicsReleased;
                return $"{F(share * 100)}% · E {F(ep)}/{F(epR)} · L {F(leg)}/{F(legR)}";
            });
            double m1 = Median(g.Select(p => p.Days.Take(30).Average(d => d.Pulls)));
            double later = Median(g.Select(p => p.Days.Skip(30).DefaultIfEmpty(new DayLog()).Average(d => d.Pulls)));
            sb.AppendLine($"| {g.Key} | {string.Join(" | ", cells)} | {m1:0.0} | {later:0.0} |");
            if (g.Key.EndsWith("Free"))
            {
                if (m1 < c.PullsMonth1[0] || m1 > c.PullsMonth1[1]) flags.Add($"**{g.Key}:** {m1:0.0} summons a day in month 1; the target is {c.PullsMonth1[0]} to {c.PullsMonth1[1]}.");
                if (days > 60 && (later < c.PullsLater[0] || later > c.PullsLater[1])) flags.Add($"**{g.Key}:** {later:0.0} summons a day after month 1; the target is {c.PullsLater[0]} to {c.PullsLater[1]}.");
            }
        }
        sb.AppendLine();
        var reg = groups.FirstOrDefault(g => g.Key == $"{c.Times[Math.Min(1, c.Times.Count - 1)].Name} Free");
        if (reg != null)
        {
            foreach (var kv in c.CollectionShare.Where(kv => kv.Key <= days))
            {
                double share = Median(reg.Select(p => (double)(p.Days[kv.Key - 1].Epics + p.Days[kv.Key - 1].Legendaries) / (p.Days[kv.Key - 1].EpicsReleased + p.Days[kv.Key - 1].LegendariesReleased)));
                if (share < kv.Value[0] || share > kv.Value[1]) flags.Add($"**Collection:** {reg.Key} players own {F(share * 100)}% of Epics and Legendaries on day {kv.Key}; the target is {F(kv.Value[0] * 100)} to {F(kv.Value[1] * 100)}%.");
            }
            var lastDay = reg.Select(p => p.Days[^1]);
            double legShare = Median(lastDay.Select(d => (double)d.Legendaries / Math.Max(1, d.LegendariesReleased)));
            if (legShare > c.LegendaryShareMax) flags.Add($"**Collection:** {reg.Key} players own {F(legShare * 100)}% of Legendaries on day {days}; the target is at most {F(c.LegendaryShareMax * 100)}%.");
        }

        // Sources, for the free player types
        sb.AppendLine("Where free players' Godshards, hero shards and heroes came from over the whole run (per day, median player):");
        sb.AppendLine();
        var srcs = players.SelectMany(p => p.GodshardsFrom.Keys.Concat(p.HeroShardsFrom.Keys)).Distinct().OrderBy(x => x).ToList();
        sb.AppendLine("| Player | " + string.Join(" | ", srcs) + " | Heroes gained by source (whole run) |");
        sb.AppendLine("|---|" + string.Concat(srcs.Select(_ => "---|")) + "---|");
        foreach (var g in groups.Where(g => g.Key.EndsWith("Free")))
        {
            var cells = srcs.Select(x =>
            {
                double gs = Median(g.Select(p => (p.GodshardsFrom.TryGetValue(x, out var v) ? v : 0) / days));
                double hs = Median(g.Select(p => (p.HeroShardsFrom.TryGetValue(x, out var v) ? v : 0) / days));
                return gs == 0 && hs == 0 ? "" : $"{F(gs)}" + (hs > 0 ? $" + {hs:0.0} shards" : "");
            });
            var how = g.SelectMany(p => p.HeroesFrom.Keys).Distinct().OrderBy(x => x).Select(x => $"{x} {F(Median(g.Select(p => (double)(p.HeroesFrom.TryGetValue(x, out var v) ? v : 0))))}");
            sb.AppendLine($"| {g.Key} | {string.Join(" | ", cells)} | {string.Join(", ", how)} |");
        }
        sb.AppendLine();

        // Spending
        sb.AppendLine("## Spending (doc 08 shop)");
        sb.AppendLine();
        sb.AppendLine("| Spend profile | What they buy | Dollars per month |");
        sb.AppendLine("|---|---|---|");
        foreach (var sp in c.Spends)
        {
            var buys = new List<string>();
            if (sp.PilgrimsPath) buys.Add("Pilgrim's Path");
            if (sp.ShrineBlessing) buys.Add("Shrine Blessing");
            if (sp.PackDollarsPerMonth > 0) buys.Add($"${sp.PackDollarsPerMonth:0} of Godshards");
            if (sp.DevotionRefillsPerDay > 0) buys.Add($"{sp.DevotionRefillsPerDay:0} Devotion refills a day (with Godshards)");
            sb.AppendLine($"| {sp.Name} | {(buys.Count == 0 ? "nothing" : string.Join(", ", buys))} | ${c.DollarsPerMonth(sp):0.00} |");
        }
        sb.AppendLine();

        // Energy
        sb.AppendLine("## Energy (Devotion)");
        sb.AppendLine();
        sb.AppendLine("| Player | Days out of energy with time left (while campaign remains) | First day out | Farm fights per day | Boss Hunts per day | Campaign fights per day (first 30 days) |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var g in groups)
        {
            double share = Median(g.Select(p => p.Days.Count(d => d.OutOfEnergy && d.Difficulty < c.Difficulties.Count) / (double)Math.Max(1, p.Days.Count(d => d.Difficulty < c.Difficulties.Count))));
            double first = Median(g.Select(p => (double)(p.Days.FirstOrDefault(d => d.OutOfEnergy && d.Difficulty < c.Difficulties.Count)?.Day ?? -1)));
            double farm = Median(g.Select(p => p.Days.Average(d => d.FarmFights)));
            double fights = Median(g.Select(p => p.Days.Take(30).Average(d => d.Fights)));
            double hunts = Median(g.Select(p => p.Days.Average(d => d.Hunts)));
            sb.AppendLine($"| {g.Key} | {F(share * 100)}% | {Day(first)} | {farm:0.0} | {hunts:0.0} | {fights:0.0} |");
        }
        sb.AppendLine();
        sb.AppendLine("Campaign fights never cost energy (doc 10 section 4). Energy runs out only when a player has time left after the campaign and the daily round and wants to keep farming.");
        sb.AppendLine();

        // Trajectory
        foreach (var name in new[] { "Regular Free", "Casual Free", "Dedicated Free" })
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

    static List<(string, int)> Walls(Cfg c, List<DayLog> days, double limit, int difficulties)
    {
        var list = new List<(string, int)>();
        int run = 0;
        foreach (var d in days)
        {
            if (d.Difficulty >= difficulties) break;
            if (d.Progressed) run = 0;
            else if (++run == (int)limit + 1) list.Add(($"{c.Difficulties[d.Difficulty].Name} {c.Realms[c.RealmOf(Math.Min(d.Battle, c.BattlesPerDifficulty - 1))]}", run));
        }
        return list;
    }
}
