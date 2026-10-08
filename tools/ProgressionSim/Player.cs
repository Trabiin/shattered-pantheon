// One simulated player, played day by day. Campaign battles are real fights on the battle engine;
// everything else (rewards, upgrades, summons, star challenges, farming, Boss Hunts, Endless and the
// shop) follows progression.json.
using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredPantheon.Battle;

class HeroCard
{
    public string Id, Kit, Name, Rarity, Faction, Role;
    public double KitPower;   // how much the kit lifts a team's win rate (1 = average), measured once by simulation
    public int ReleaseDay;    // 0 = in the launch roster
}

class Owned
{
    public HeroCard Card;
    public int Level = 1, Stars, Gear, Skill = 1;
}

class DayLog
{
    public int Day, Difficulty, Battle, Roster, TeamLevel, Stars, Fights, FarmFights, Hunts, Pulls, Released, Legendaries, LegendariesReleased, Epics, EpicsReleased;
    public double TeamStars, TeamGear, TeamSkill;
    public double TeamScale, RecScale, Godshards, GodshardsEarned, MinutesUnused;
    public bool OutOfEnergy, BigMoment, NewHero, Progressed;
}

class Player
{
    readonly Cfg C;
    readonly GameData data;
    readonly List<HeroCard> cards;
    readonly TimeProfile time;
    readonly SpendProfile spend;
    readonly Random rng;
    readonly long seed;

    public readonly Dictionary<string, Owned> Roster = new Dictionary<string, Owned>();
    // Fodder by star rank: duplicates and Vessels, which count as any hero of that rank when ascending.
    readonly Dictionary<int, int> fodder = Enumerable.Range(1, 6).ToDictionary(r => r, r => 0);
    double xp, gold, shards, devotion, gearMats, tomes, tickets, shardsEarned, heroShards;
    readonly Dictionary<string, int> sinceRarity = new Dictionary<string, int>();   // pulls since at least this rarity
    int pullsTotal;
    public double Dollars;
    public int Diff, Battle;   // next campaign battle: difficulty and index within it
    int[,] stars;              // stars earned per difficulty and battle (0 = not cleared, 1 = won, 2-5 challenges)
    int starsTotal, starChestsGiven, battlesCleared, attemptsToday;
    bool legendaryPicked, epicPicked;
    readonly HashSet<string> codexDone = new HashSet<string>();
    public readonly List<DayLog> Days = new List<DayLog>();
    DayLog today;
    public int RealFights, PracticeFights;
    public Dictionary<string, int> DoneDay = new Dictionary<string, int>();   // milestone -> day
    public int FirstLegendaryDay = -1;

    public Player(Cfg c, GameData data, List<HeroCard> cards, TimeProfile time, SpendProfile spend, long seed)
    {
        C = c; this.data = data; this.cards = cards; this.time = time; this.spend = spend; this.seed = seed;
        rng = new Random((int)(seed * 7919 % int.MaxValue));
        stars = new int[c.Difficulties.Count, c.BattlesPerDifficulty];
        foreach (var kit in c.Starters) Gain(cards.First(h => h.Kit == kit && h.Rarity == c.StarterRarity), false, "Starters");
        foreach (var g in c.Guarantees) sinceRarity[g.rarity] = 0;
        shards = c.StartingGodshards;   // the first 10-pull, a few minutes in
    }

    int Day => today?.Day ?? 0;
    IEnumerable<HeroCard> Released => cards.Where(h => h.ReleaseDay <= Day);
    int Rank(string rarity) => C.Rarities.IndexOf(rarity);   // 0 = rarest

    public string Name => $"{time.Name} {spend.Name}";

    // ---------- Day loop ----------

    public void PlayDay(int day)
    {
        today = new DayLog { Day = day };
        double before = shardsEarned;
        int clearedBefore = battlesCleared;
        int rosterBefore = Roster.Count;
        double minutesPerSession = time.Minutes / time.Sessions;
        attemptsToday = 0;

        for (int s = 0; s < time.Sessions; s++)
        {
            double minutes = minutesPerSession;
            // Shrine and energy fill while away.
            double away = 24.0 / time.Sessions;
            src = "Shrine"; Add(C.ShrinePerHour, Math.Min(away, C.ShrineCapHours));
            devotion += Math.Min(C.DevotionRegenCap, C.DevotionRegenPerDay / time.Sessions);
            if (s == 0)
            {
                minutes -= C.DailyMinutes;
                src = "Dailies"; Add(C.Daily, 1);
                var team = BestTeam(null);
                src = "Dailies"; if (TeamScale(team) >= C.RecScale(Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1)) * C.DailyHardPower) Earn(C.DailyHardGodshards * C.DailyHardCount);
                src = "Weekly"; if (day % 7 == 0) Add(C.Weekly, 1);
                if (day % C.LoginEvery == 0) { tickets += C.LoginTickets; today.BigMoment = true; }
                Shop(day);
                if (day == C.EpicPickDay && !epicPicked) { epicPicked = true; Pick("Epic"); }
                // Endless: one run a week for its milestone rewards.
                src = "Endless"; if (day % 7 == 3 && battlesCleared >= C.Endless.UnlockBattles) { Add(C.Endless.ByDifficulty[HuntTier()], 1, false); minutes -= C.Endless.Minutes; }
            }
            // The day's free Boss Hunts are part of the daily round; extra hunts with Devotion come later.
            if (s == 0) minutes = Hunts(minutes, C.BossHunts.FreePerDay, false);
            Upgrade();
            Summon();
            minutes = PushCampaign(minutes);
            Upgrade();
            minutes = Challenges(minutes);
            minutes = Hunts(minutes, 0, true);
            minutes = FarmFights(minutes);
            today.MinutesUnused += Math.Max(0, minutes);
            Upgrade();
            Summon();
        }

        var t = BestTeam(null);
        today.Difficulty = Diff; today.Battle = Battle; today.Roster = Roster.Count;
        today.TeamLevel = t.Count == 0 ? 0 : (int)Math.Round(t.Average(o => o.Level));
        if (t.Count > 0) { today.TeamStars = t.Average(o => o.Stars); today.TeamGear = t.Average(o => o.Gear); today.TeamSkill = t.Average(o => o.Skill); }
        today.Released = Released.Count();
        today.Epics = Roster.Values.Count(o => o.Card.Rarity == C.Rarities[1]); today.EpicsReleased = Released.Count(h => h.Rarity == C.Rarities[1]); today.Legendaries = Roster.Values.Count(o => o.Card.Rarity == C.Rarities[0]);
        today.LegendariesReleased = Released.Count(h => h.Rarity == C.Rarities[0]);
        today.TeamScale = TeamScale(t); today.RecScale = C.RecScale(Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1));
        today.Stars = starsTotal; today.Godshards = shards; today.GodshardsEarned = shardsEarned - before;
        today.Progressed = battlesCleared > clearedBefore;
        if (Debug && !today.Progressed && Diff < C.Difficulties.Count && Days.Count >= 20 && Days.Skip(Days.Count - 20).All(x => !x.Progressed) && Days[^20].Progressed == false && !debugged.Contains((Diff, Battle))) DebugWall();
        today.NewHero |= Roster.Count > rosterBefore;
        Days.Add(today);
    }

    // ---------- Campaign ----------

    double FightSeconds(int actions) => actions / C.ReplaySpeed + C.FightOverhead;

    double PushCampaign(double minutes)
    {
        int losses = 0;
        bool upgradedAfterLoss = false;
        while (minutes > 0 && Diff < C.Difficulties.Count)
        {
            var stage = Cfg.StageId(Diff, Battle);
            var enemies = data.Stages.First(s => s.Id == stage).Enemies.Select(e => data.Enemy(e.Id)).ToList();
            // After a loss, alternate between the best line-up and a different one, as a player would.
            lossesHere.TryGetValue((Diff, Battle), out int tries);
            var team = tries < 2 ? BestTeam(enemies) : Counter(stage, enemies, tries);
            var slots = Place(team);
            var r = ShatteredPantheon.Battle.Battle.Run(data, slots, stage, seed * 1000003 + Diff * 10007 + Battle * 31 + attemptsToday, null);
            RealFights++; attemptsToday++; today.Fights++;
            minutes -= FightSeconds(r.Actions) / 60;
            if (r.Result == "win")
            {
                Clear();
                losses = 0; upgradedAfterLoss = false;
                continue;
            }
            losses++;
            lossesHere[(Diff, Battle)] = tries + 1;
            if (losses >= C.AttemptsBeforeUpgrading)
            {
                if (upgradedAfterLoss) break;   // stuck for this session
                Upgrade(); Summon();
                upgradedAfterLoss = true; losses = 0;
            }
        }
        return minutes;
    }

    void Clear()
    {
        int d = Diff, k = Battle;
        stars[d, k] = Math.Max(stars[d, k], 1);
        starsTotal++; battlesCleared++;
        src = "Campaign"; Add(C.FirstClear, 1);
        src = "Campaign"; if (k % C.BattlesPerStage == C.BattlesPerStage - 1) { Add(C.StageClear, 1); AddVessels(C.StageClearVessels[d], 1); today.BigMoment = true; }
        CheckStarChest();
        if (battlesCleared == C.LegendaryPickBattles && !legendaryPicked) { legendaryPicked = true; Pick("Legendary"); }
        Battle++;
        int stage = k / C.BattlesPerStage + 1;
        if (k % C.BattlesPerStage == C.BattlesPerStage - 1 && stage % 10 == 0) DoneDay.TryAdd($"{C.Difficulties[d].Name} {stage}", today.Day);
        if (Battle >= C.BattlesPerDifficulty) { Diff++; Battle = 0; }
    }

    // Stars 2 to 5 are challenges. They're met on a replay once the team is far enough above the
    // battle's recommended power (a stand-in for "win without a healer", "win in 60 seconds"...).
    double Challenges(double minutes)
    {
        double team = TeamScale(BestTeam(null));
        for (int d = 0; d <= Math.Min(Diff, C.Difficulties.Count - 1) && minutes > 0; d++)
            for (int k = 0; k < C.BattlesPerDifficulty && minutes > 0; k++)
            {
                if (stars[d, k] == 0) break;
                double need = C.RecScale(d, k);
                while (stars[d, k] < 5 && team >= need * C.StarSteps[stars[d, k] - 1] && minutes > 0)
                {
                    stars[d, k]++; starsTotal++;
                    src = "Stars"; Earn(C.StarGodshards);
                    minutes -= FightSeconds(60) / 60;
                    CheckStarChest();
                }
            }
        return minutes;
    }

    void CheckStarChest()
    {
        while (starsTotal >= (starChestsGiven + 1) * C.StarChestEvery)
        {
            starChestsGiven++;
            src = "Stars"; Add(C.StarChest, 1);
            today.BigMoment = true;
        }
    }

    double FarmFights(double minutes)
    {
        double each = FightSeconds(40) / 60;
        while (minutes >= each)
        {
            if (devotion < C.FarmCost) { today.OutOfEnergy = true; break; }
            devotion -= C.FarmCost;
            src = "Farming"; Add(C.Farm, 1);
            src = "Farming"; Add(C.FarmByDifficulty[HuntTier()], 1, false);
            minutes -= each;
            today.FarmFights++;
        }
        return minutes;
    }

    // Boss Hunts (doc 06): free attempts each day, then a few more with Devotion, on the highest
    // difficulty where the player has beaten a boss. A stand-in for real fights: the player picks a
    // tier they've already beaten.
    int huntsToday;
    double Hunts(double minutes, int free, bool paid)
    {
        if (battlesCleared < C.BossHunts.UnlockBattles) return minutes;
        if (free > 0) huntsToday = 0;
        while (minutes >= C.BossHunts.Minutes)
        {
            if (free > 0) free--;
            else if (paid && huntsToday < C.BossHunts.FreePerDay + 4 && devotion >= C.BossHunts.DevotionCost) devotion -= C.BossHunts.DevotionCost;
            else break;
            huntsToday++; today.Hunts++;
            src = "Boss Hunts"; Add(C.BossHunts.ByDifficulty[HuntTier()], 1, false);
            minutes -= C.BossHunts.Minutes;
        }
        return minutes;
    }

    int HuntTier()
    {
        if (Diff >= C.Difficulties.Count) return C.Difficulties.Count - 1;
        return Battle >= C.BattlesPerStage || Diff == 0 ? Diff : Diff - 1;
    }

    // ---------- Shop (doc 08) ----------

    void Shop(int day)
    {
        int cycle = (int)C.PassDays;
        bool newMonth = (day - 1) % cycle == 0;
        src = "Shop"; if (day == 1 && spend.StarterOffer) { Add(C.StarterOffer, 1); Dollars += C.StarterPrice; }
        if (spend.PilgrimsPath)
        {
            // The pass pays out along its track; spread evenly over the month here.
            src = "Shop"; Earn(C.PassReward.Godshards / cycle);
            tickets += C.PassReward.SummonTickets / cycle;
            if (newMonth) { AddVessels(C.PassReward.Vessels, 1); Dollars += C.PassPrice; }
        }
        if (spend.ShrineBlessing)
        {
            src = "Shop"; if (newMonth) { Earn(C.BlessingNow); Dollars += C.BlessingPrice; }
            src = "Shop"; Earn(C.BlessingPerDay); devotion += C.BlessingDevotion;
        }
        if (newMonth && spend.PackDollarsPerMonth > 0)
        {
            src = "Shop"; Earn(C.PackGodshards(spend.PackDollarsPerMonth) * (day == 1 ? 2 : 1));   // first purchases count double
            Dollars += C.DollarsPerMonth(new SpendProfile { PackDollarsPerMonth = spend.PackDollarsPerMonth });
        }
        for (int i = 0; i < spend.DevotionRefillsPerDay && i < C.RefillCosts.Length; i++)
            if (shards >= C.RefillCosts[i]) { shards -= C.RefillCosts[i]; devotion += C.RefillDevotion; }
    }

    static readonly bool Debug = Environment.GetEnvironmentVariable("SIM_DEBUG") == "1";
    readonly HashSet<(int, int)> debugged = new HashSet<(int, int)>();
    void DebugWall()
    {
        debugged.Add((Diff, Battle));
        var stage = Cfg.StageId(Diff, Battle);
        var enemies = data.Stages.First(x => x.Id == stage).Enemies.Select(e => data.Enemy(e.Id)).ToList();
        var team = BestTeam(enemies);
        int wins = 0, n = 40, winsVar = 0;
        for (int i = 0; i < n; i++)
        {
            if (ShatteredPantheon.Battle.Battle.Run(data, Place(team), stage, 99000 + i, null).Result == "win") wins++;
            if (ShatteredPantheon.Battle.Battle.Run(data, Place(VariantTeam(enemies, i)), stage, 98000 + i, null).Result == "win") winsVar++;
        }
        Console.Error.WriteLine($"[{Name} {seed}] day {today.Day} stuck 20+ days on {stage}: team {TeamScale(team) / C.RecScale(Diff, Battle):P0} of recommended, wins {wins}/{n}, variants {winsVar}/{n}. " +
            string.Join(", ", team.Select(o => $"{o.Card.Kit}/{o.Card.Rarity[0]} L{o.Level} {o.Stars}* g{o.Gear} s{o.Skill}")) + $" | fodder {string.Join(" ", fodder.Select(kv => kv.Key + ":" + kv.Value))} xp {xp:0} gold {gold:0} mats {gearMats:0} tomes {tomes:0}");
    }

    // ---------- Team ----------

    double HeroScale(Owned o) => C.Scale(o.Card.Rarity, o.Level, o.Stars, o.Gear, o.Skill);
    // Kit strength counts for a lot: an average kit needs about 15% more growth to match a strong one.
    double Power(Owned o) => HeroScale(o) * Math.Pow(o.Card.KitPower, 2);
    double TeamScale(List<Owned> team) => team.Count == 0 ? 0 : team.Average(HeroScale) * Math.Min(1, team.Count / 5.0);

    // The strongest 5 heroes with different kits, nudged toward heroes whose type does well against
    // the enemies, and with at least one front-liner when one is available.
    List<Owned> BestTeam(List<UnitDef> enemies)
    {
        double Score(Owned o)
        {
            double p = Power(o);
            if (enemies == null || enemies.Count == 0) return p;
            double deals = enemies.Average(e => ShatteredPantheon.Battle.Battle.TypeMultiplier(data.Rules, o.Card.Kit == null ? "" : data.Hero(o.Card.Kit).Type, e.Type));
            double takes = enemies.Average(e => ShatteredPantheon.Battle.Battle.TypeMultiplier(data.Rules, e.Type, data.Hero(o.Card.Kit).Type));
            return p * deals / Math.Sqrt(takes);
        }
        // A tank and a healer first when there's a decent one: without them most line-ups lose
        // badly at equal power (a team of five Warriors wins about 1 fight in 10 that a balanced team wins).
        var ranked = Roster.Values.OrderByDescending(Score).ToList();
        var team = new List<Owned>();
        double top = ranked.Count > 0 ? Score(ranked[0]) : 0;
        foreach (var role in new[] { "Tank", "Support" })
        {
            var o = ranked.FirstOrDefault(h => h.Card.Role == role && team.All(t => t.Card.Kit != h.Card.Kit));
            if (o != null && Score(o) >= top * 0.7) team.Add(o);
        }
        foreach (var o in ranked)
        {
            if (team.Count == 5) break;
            if (team.All(t => t.Card.Kit != o.Card.Kit)) team.Add(o);
        }
        return team;
    }

    readonly Dictionary<(int, int), int> lossesHere = new Dictionary<(int, int), int>();

    // A different line-up after a loss: any 5 kits whose best hero is close to the strongest ones,
    // keeping a tank and a healer when the pool has them. Players rearrange and try counters before grinding (doc 10).
    List<Owned> VariantTeam(List<UnitDef> enemies, int variant, double barFactor = 0.85)
    {
        var best = BestTeam(enemies);
        var byKit = Roster.Values.OrderByDescending(HeroScale).GroupBy(o => o.Card.Kit).Select(g => g.First()).ToList();
        double bar = byKit.Count >= 5 ? HeroScale(byKit[4]) * barFactor : 0;
        var pool = byKit.Where(o => HeroScale(o) >= bar).ToList();
        if (pool.Count <= 5) return best;
        var r = new Random(variant * 7919 + Diff * 31 + Battle);
        for (int tries = 0; tries < 20; tries++)
        {
            var team = pool.OrderBy(_ => r.Next()).Take(5).ToList();
            bool Has(string role) => team.Any(t => t.Card.Role == role) || pool.All(t => t.Card.Role != role);
            if (Has("Tank") && Has("Support")) return team;
        }
        return best;
    }

    // After a couple of losses the player studies the fight and tries counters (doc 10: rearrange
    // before grinding). Modelled as picking, from the best line-up and a spread of other line-ups,
    // the one that does best in a few practice fights. Re-thought now and then as the roster grows.
    readonly Dictionary<(int, int), (int at, List<Owned> team)> plans = new Dictionary<(int, int), (int, List<Owned>)>();
    List<Owned> Counter(string stage, List<UnitDef> enemies, int tries)
    {
        if (plans.TryGetValue((Diff, Battle), out var plan) && tries < plan.at * 2 + 4) return plan.team;   // re-think less and less often
        var main = BestTeam(enemies);
        var candidates = new List<List<Owned>> { main };
        for (int v = 0; v < 9; v++) candidates.Add(VariantTeam(enemies, tries * 100 + v));
        List<Owned> best = null; int bestWins = -1;
        foreach (var team in candidates)
        {
            int wins = 0;
            for (int i = 0; i < 3; i++)
            {
                PracticeFights++;
                if (ShatteredPantheon.Battle.Battle.Run(data, Place(team), stage, seed * 7 + tries * 1009 + i * 13 + 5, null).Result == "win") wins++;
            }
            if (wins > bestWins || (wins == bestWins && team == main)) { best = team; bestWins = wins; }
        }
        plans[(Diff, Battle)] = (tries, best);
        return best;
    }

    List<TeamSlot> Place(List<Owned> team, double scale = 0)
    {
        var placed = Formation.AutoPlace(data, team.Select(o => o.Card.Kit).ToList());
        foreach (var s in placed)
        {
            var o = team.First(t => t.Card.Kit == s.Id);
            s.HpScale = s.AtkScale = scale > 0 ? scale : HeroScale(o);
        }
        return placed;
    }

    // ---------- Upgrades ----------

    void Upgrade()
    {
        var team = BestTeam(null);
        // A bench of 10 is levelled together, so there are real line-ups to switch to when a battle needs a counter.
        var core = Roster.Values.OrderByDescending(Power).GroupBy(o => o.Card.Kit).Select(g => g.First()).Take(10).ToList();
        // Gear first, up to what the current battle recommends, so gold isn't all spent on levels.
        var rec = C.Recommended(Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1));
        UpgradeGear(team, (int)Math.Ceiling(rec.gear));
        // Levels: the team up to the recommended level first, then the whole bench, lowest first.
        foreach (var (group, upTo) in new[] { (team, (int)Math.Ceiling(rec.level)), (core, int.MaxValue) })
            while (true)
            {
                var o = group.Where(h => h.Level < Math.Min(upTo, C.Cap(h.Stars))).OrderBy(h => h.Level).FirstOrDefault();
                if (o == null) break;
                double cost = C.XpToNext(o.Level);
                if (xp < cost || gold < cost * C.GoldPerXp) break;
                xp -= cost; gold -= cost * C.GoldPerXp; o.Level++;
            }
        // Then gear beyond the recommendation, and skills.
        UpgradeGear(team, C.MaxGear);
        while (true)
        {
            var o = team.Where(h => h.Skill < C.MaxSkill).OrderBy(h => h.Skill).FirstOrDefault();
            if (o == null) break;
            double need = C.TomesPerLevel * o.Skill;
            if (tomes < need) break;
            tomes -= need; o.Skill++;
        }
        // Ascension: a capped team hero takes fodder of its own star rank.
        foreach (var o in team.Where(h => h.Level >= C.Cap(h.Stars) && h.Stars < 6).OrderByDescending(Power))
            if (TakeFodder(o.Stars, C.AscendFodder[o.Stars]))
            {
                o.Stars++;
                today.BigMoment = true;
                if (C.CodexStars.TryGetValue(o.Stars, out var r) && codexDone.Add(o.Card.Id + "*" + o.Stars)) EarnFrom("Codex", r);
            }
    }

    void UpgradeGear(List<Owned> team, int upTo)
    {
        while (true)
        {
            var o = team.Where(h => h.Gear < Math.Min(upTo, C.MaxGear)).OrderBy(h => h.Gear).FirstOrDefault();
            if (o == null) break;
            double mats = C.GearMatsPerTier * (o.Gear + 1), g = C.GearGoldLevels * C.XpToNext(Math.Max(1, o.Level)) * C.GoldPerXp;
            if (gearMats < mats || gold < g) break;
            gearMats -= mats; gold -= g; o.Gear++;
        }
    }

    // Fodder of a star rank, made from lower-rank fodder when needed (a fodder hero is ascended
    // with the same cost as any other hero). Crafted fodder that isn't used stays in the pool.
    bool TakeFodder(int rank, int count)
    {
        if (!Ensure(rank, count)) return false;
        fodder[rank] -= count;
        return true;
    }

    bool Ensure(int rank, int count)
    {
        while (fodder[rank] < count)
        {
            if (rank <= 1) return false;
            int need = 1 + C.AscendFodder[rank - 1];
            if (!Ensure(rank - 1, need)) return false;
            fodder[rank - 1] -= need;
            fodder[rank]++;
        }
        return true;
    }

    // ---------- Heroes and summons ----------

    void Gain(HeroCard card, bool fromPull, string how = "Summons")
    {
        HeroesFrom[how + (Roster.ContainsKey(card.Id) ? " (duplicate)" : "")] = (HeroesFrom.TryGetValue(how + (Roster.ContainsKey(card.Id) ? " (duplicate)" : ""), out var n) ? n : 0) + 1;
        if (Roster.ContainsKey(card.Id))
        {
            fodder[(int)C.RarityStars[card.Rarity]]++;
            return;
        }
        Roster[card.Id] = new Owned { Card = card, Stars = (int)C.RarityStars[card.Rarity] };
        if (today != null) { today.NewHero = true; if (Rank(card.Rarity) <= 1) today.BigMoment = true; }
        if (card.Rarity == C.Rarities[0] && FirstLegendaryDay < 0) FirstLegendaryDay = today?.Day ?? 0;
        if (fromPull || today != null)
        {
            EarnFrom("Codex", C.CodexFirst[card.Rarity]);
            var faction = Released.Where(h => h.Faction == card.Faction).ToList();
            double share = (double)faction.Count(h => Roster.ContainsKey(h.Id)) / faction.Count;
            foreach (var kv in C.CodexFaction)
                if (share >= kv.Key - 1e-9 && codexDone.Add(card.Faction + "%" + kv.Key)) { EarnFrom("Codex", kv.Value); if (kv.Value >= 300 && today != null) today.BigMoment = true; }
        }
    }

    void Summon()
    {
        while (tickets >= 1 || shards >= C.SummonCost)
        {
            if (tickets >= 1) tickets--; else shards -= C.SummonCost;
            pullsTotal++; if (today != null) today.Pulls++;
            foreach (var g in C.Guarantees) sinceRarity[g.rarity]++;
            // Roll a rarity, then lift it to the rarest guarantee that's due.
            double roll = rng.NextDouble(), acc = 0;
            int rank = C.Rarities.Count - 1;
            for (int i = 0; i < C.Rarities.Count; i++) { acc += C.Rates[C.Rarities[i]]; if (roll < acc) { rank = i; break; } }
            foreach (var g in C.Guarantees) if (sinceRarity[g.rarity] >= g.every) rank = Math.Min(rank, Rank(g.rarity));
            if (C.FirstTenEpic && pullsTotal == 10 && !Roster.Values.Any(o => Rank(o.Card.Rarity) <= Rank("Epic"))) rank = Math.Min(rank, Rank("Epic"));
            foreach (var g in C.Guarantees) if (rank <= Rank(g.rarity)) sinceRarity[g.rarity] = 0;
            var pool = Released.Where(h => h.Rarity == C.Rarities[rank]).ToList();
            Gain(pool[rng.Next(pool.Count)], true);
        }
    }

    // Hero shards go to the top of the wish list: the rarest, strongest released hero not yet owned.
    void SpendHeroShards()
    {
        while (true)
        {
            var wish = Released.Where(h => !Roster.ContainsKey(h.Id)).OrderBy(h => Rank(h.Rarity)).ThenByDescending(h => h.KitPower).FirstOrDefault();
            if (wish == null || heroShards < C.ShardUnlock[wish.Rarity]) return;
            heroShards -= C.ShardUnlock[wish.Rarity];
            Gain(wish, true, "Hero shards");
            if (today != null) today.BigMoment = true;
        }
    }

    void AddVessels(Dictionary<int, double> v, double times)
    {
        foreach (var kv in v)
        {
            double n = kv.Value * times;
            int whole = (int)n;
            if (rng.NextDouble() < n - whole) whole++;
            fodder[kv.Key] += whole;
        }
    }

    // A free pick: the strongest hero of that rarity not yet owned.
    void Pick(string rarity)
    {
        var c = Released.Where(h => h.Rarity == rarity && !Roster.ContainsKey(h.Id)).OrderByDescending(h => h.KitPower).FirstOrDefault();
        if (c != null) { Gain(c, true, "Free picks"); today.BigMoment = true; }
    }

    // ---------- Rewards ----------

    // XP and gold rewards are counted in level-ups at the current battle's recommended level.
    double LevelCost()
    {
        int d = Math.Min(Diff, C.Difficulties.Count - 1), k = Math.Min(Battle, C.BattlesPerDifficulty - 1);
        return C.XpToNext(Math.Max(1, C.RecLevel(d, k))) * RewardScale();
    }

    double RewardScale()
    {
        int d = Math.Min(Diff, C.Difficulties.Count - 1), k = Math.Min(Battle, C.BattlesPerDifficulty - 1);
        // Eases from the previous difficulty's value to this one's over its first battles, so there's no sudden drop.
        double from = d == 0 ? C.Difficulties[0].RewardScale : C.Difficulties[d - 1].RewardScale;
        double t = Math.Min(1, k / (C.RewardEase * C.BattlesPerDifficulty));
        return from + (C.Difficulties[d].RewardScale - from) * t;
    }

    // Gear materials and tomes follow the difficulty's rewardScale, except from Boss Hunts and Endless,
    // whose tables already list the amount for each difficulty.
    void Add(Reward r, double times, bool scaled = true)
    {
        double lc = LevelCost(), rs = scaled ? RewardScale() : 1;
        double bonus = spend.PilgrimsPath ? C.PassBonus : 0;
        xp += r.XpLevels * lc * times * (1 + bonus);
        gold += r.GoldLevels * lc * C.GoldPerXp * times * (1 + bonus);
        Earn(r.Godshards * times);
        devotion += r.Devotion * times;
        gearMats += r.GearMats * times * rs;
        tomes += r.SkillTomes * times * rs;
        tickets += r.SummonTickets * times;
        heroShards += r.HeroShards * times;
        if (r.HeroShards > 0) HeroShardsFrom[src] = (HeroShardsFrom.TryGetValue(src, out var hv) ? hv : 0) + r.HeroShards * times;
        AddVessels(r.Vessels, times);
        if (r.HeroShards > 0) SpendHeroShards();
    }

    // Where Godshards and hero shards come from, for the report.
    string src = "Other";
    public readonly Dictionary<string, double> GodshardsFrom = new Dictionary<string, double>(), HeroShardsFrom = new Dictionary<string, double>();
    public readonly Dictionary<string, int> HeroesFrom = new Dictionary<string, int>();
    void EarnFrom(string from, double g) { var was = src; src = from; Earn(g); src = was; }
    void Earn(double g)
    {
        shards += g; shardsEarned += g;
        GodshardsFrom[src] = (GodshardsFrom.TryGetValue(src, out var v) ? v : 0) + g;
    }
}
