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
    public double CopyPower;  // how a second copy of the kit does against another hero in its place (1 = as well), measured the same way
    public int ReleaseDay;    // 0 = in the launch roster
}

class Owned
{
    public HeroCard Card;
    public int Level = 1, Stars, Gear, Skill = 1;
    public bool Copy;   // a second or later copy of the hero, kept to field
}

class DayLog
{
    public int Day, Difficulty, Battle, Roster, TeamLevel, Stars, Fights, FarmFights, Hunts, Pulls, Released, Legendaries, LegendariesReleased, Epics, EpicsReleased;
    public int TeamCopies;   // team slots taken by a further copy of a hero already in the team
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

    // Every hero the player develops, keyed by card id; copies kept to field are "<card id>#2" and so on.
    public readonly Dictionary<string, Owned> Roster = new Dictionary<string, Owned>();
    // Fodder by star rank, which counts as any hero of that rank when ascending: Vessels, spare copies
    // at level 1, and spare copies the player has levelled to their rank's cap and ascended (doc 07 section 1).
    readonly Dictionary<int, int> vessels = Ranks(), spares = Ranks(), made = Ranks();
    static Dictionary<int, int> Ranks() => Enumerable.Range(1, 6).ToDictionary(r => r, r => 0);
    public int FodderMade;
    double busy;   // minutes of menu work (making fodder) not yet taken from a session
    double xp, gold, shards, devotion, gearMats, tomes, tickets, shardsEarned, heroShards;
    readonly SummonPity pity;   // the guarantee counters
    public double Dollars;
    public int Diff, Battle;   // next campaign battle: difficulty and index within it
    int[,] stars;              // stars earned per difficulty and battle (0 = not cleared, 1 = won, 2-5 challenges)
    int[,] stageChests;   // chests opened per stage (0 to 3)
    int starsTotal, battlesCleared, attemptsToday;
    readonly int[] starsOn, starChestsGiven;   // stars and star track chests per difficulty
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
        stageChests = new int[c.Difficulties.Count, c.Stages];
        starsOn = new int[c.Difficulties.Count]; starChestsGiven = new int[c.Difficulties.Count];
        foreach (var kit in c.Starters) Gain(cards.First(h => h.Kit == kit && h.ReleaseDay == 0), false, "Starters");
        pity = c.Summon.NewPity();
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
        int rosterBefore = Roster.Values.Count(o => !o.Copy);
        double minutesPerSession = time.Minutes / time.Sessions;
        attemptsToday = 0;

        for (int s = 0; s < time.Sessions; s++)
        {
            double minutes = Busy(minutesPerSession);
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
            minutes = PushCampaign(Busy(minutes));
            Upgrade();
            // Devotion first (hunts, farming), then replays for challenge stars with the time left.
            minutes = Hunts(Busy(minutes), 0, true);
            minutes = FarmFights(minutes);
            minutes = Challenges(minutes);
            Upgrade();
            Summon();
            today.MinutesUnused += Math.Max(0, Busy(minutes));
        }

        var t = BestTeam(null);
        today.Difficulty = Diff; today.Battle = Battle;
        today.TeamLevel = t.Count == 0 ? 0 : (int)Math.Round(t.Average(o => o.Level));
        if (t.Count > 0) { today.TeamStars = t.Average(o => o.Stars); today.TeamGear = t.Average(o => o.Gear); today.TeamSkill = t.Average(o => o.Skill); }
        today.TeamCopies = t.Count - t.Select(o => o.Card.Id).Distinct().Count();
        today.Released = Released.Count();
        var owned = Roster.Values.Where(o => !o.Copy).ToList();
        today.Roster = owned.Count;
        today.Epics = owned.Count(o => o.Card.Rarity == C.Rarities[1]); today.EpicsReleased = Released.Count(h => h.Rarity == C.Rarities[1]); today.Legendaries = owned.Count(o => o.Card.Rarity == C.Rarities[0]);
        today.LegendariesReleased = Released.Count(h => h.Rarity == C.Rarities[0]);
        today.TeamScale = TeamScale(t); today.RecScale = C.RecScale(Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1));
        today.Stars = starsTotal; today.Godshards = shards; today.GodshardsEarned = shardsEarned - before;
        today.Progressed = battlesCleared > clearedBefore;
        if (Debug && !today.Progressed && Diff < C.Difficulties.Count && Days.Count >= 20 && Days.Skip(Days.Count - 20).All(x => !x.Progressed) && Days[^20].Progressed == false && !debugged.Contains((Diff, Battle))) DebugWall();
        today.NewHero |= today.Roster > rosterBefore;
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
            var team = tries < 2 || tries % 2 == 0 ? BestTeam(enemies) : Counter(stage, enemies, tries);
            var slots = Place(team);
            var r = ShatteredPantheon.Battle.Battle.Run(data, slots, stage, seed * 1000003 + Diff * 10007 + Battle * 31 + today.Day * 7919 + attemptsToday, null);
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
            // A player who has already lost a full session here tries once, then goes to grow the team
            // (hunts, farming) instead of spending the whole session on the same battle.
            if (tries >= C.AttemptsBeforeUpgrading * 2) { Upgrade(); Summon(); minutes = Busy(minutes); break; }
            if (losses >= C.AttemptsBeforeUpgrading)
            {
                if (upgradedAfterLoss) break;   // stuck for this session
                Upgrade(); Summon(); minutes = Busy(minutes);
                upgradedAfterLoss = true; losses = 0;
            }
        }
        return minutes;
    }

    void Clear()
    {
        int d = Diff, k = Battle;
        stars[d, k] = Math.Max(stars[d, k], 1);
        starsTotal++; starsOn[d]++; battlesCleared++;
        // The first win's reward, with the stage clear and its Vessel chance on a stage boss: the game's rules (CampaignRewards).
        src = "Campaign"; Add(C.Rewards.FirstWin(d, k));
        if (C.Rewards.IsStageBoss(k)) today.BigMoment = true;
        CheckStarChest(d); CheckStageChests(d, k);
        if (battlesCleared == C.LegendaryPickBattles && !legendaryPicked) { legendaryPicked = true; Pick("Legendary"); }
        Battle++;
        if (C.IsRealmBoss(k)) DoneDay.TryAdd($"{C.Difficulties[d].Name} realm {C.RealmOf(k) + 1}", today.Day);
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
                    stars[d, k]++; starsTotal++; starsOn[d]++;
                    src = "Stars"; Add(C.Rewards.Stars(1, Here.diff, Here.k));
                    minutes -= FightSeconds(60) / 60;
                    CheckStarChest(d); CheckStageChests(d, k);
                }
            }
        return minutes;
    }

    // Stage chests (doc 06 section 1.4): small, medium and large at 6, 12 and 20 of a stage's 20 stars.
    void CheckStageChests(int d, int k)
    {
        int st = C.StageOf(k), first = st * C.BattlesPerStage, got = 0;
        for (int b = first; b < first + C.BattlesPerStage; b++) got += stars[d, b];
        while (stageChests[d, st] < C.Rewards.StageChestsAt(got))
        {
            src = "Stage chests"; Add(C.Rewards.StageChest(stageChests[d, st], Here.diff, Here.k));
            if (stageChests[d, st] == C.Rewards.StageChestStars.Length - 1) today.BigMoment = true;
            stageChests[d, st]++;
        }
    }

    // The star track: a chest every 100 stars on a difficulty (doc 06 section 1.4).
    void CheckStarChest(int d)
    {
        while (starChestsGiven[d] < C.Rewards.StarChestsAt(starsOn[d]))
        {
            starChestsGiven[d]++;
            src = "Stars"; Add(C.Rewards.StarTrackChest(Here.diff, Here.k));
            today.BigMoment = true;
        }
    }

    double FarmFights(double minutes)
    {
        double each = FightSeconds(40) / 60;
        while (minutes >= each)
        {
            if (devotion < C.Rewards.ReplayDevotion) { today.OutOfEnergy = true; break; }
            devotion -= C.Rewards.ReplayDevotion;
            src = "Farming"; Add(C.Rewards.Replay(HuntTier(), Here.diff, Here.k));
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
            string.Join(", ", team.Select(o => $"{o.Card.Kit}/{o.Card.Rarity[0]} L{o.Level} {o.Stars}* g{o.Gear} s{o.Skill}")) + $" | fodder (Vessels/spare/made) {string.Join(" ", vessels.Keys.Select(r => $"{r}:{vessels[r]}/{spares[r]}/{made[r]}"))} xp {xp:0} gold {gold:0} mats {gearMats:0} tomes {tomes:0} farm {Days.Skip(Math.Max(0, Days.Count - 20)).Sum(x => x.FarmFights)} hunts {Days.Skip(Math.Max(0, Days.Count - 20)).Sum(x => x.Hunts)} unused {Days.Skip(Math.Max(0, Days.Count - 20)).Sum(x => x.MinutesUnused):0}m");
    }

    // ---------- Team ----------

    double HeroScale(Owned o) => C.Growth.StatScale(o.Card.Rarity, o.Level, o.Stars, o.Gear, o.Skill);
    // Kit strength counts for a lot: an average kit needs about 15% more growth to match a strong one.
    double Power(Owned o) => HeroScale(o) * Math.Pow(o.Card.KitPower, 2);
    double TeamScale(List<Owned> team) => team.Count == 0 ? 0 : team.Average(HeroScale) * Math.Min(1, team.Count / 5.0);
    // How strong a hero gets for the same growth as any other.
    double Potential(HeroCard c) => C.Growth.RarityBase[c.Rarity] * Math.Pow(c.KitPower, 2);

    // A kept copy is ranked as strong as its hero's first copy, which it's being built to match.
    double BenchPower(Owned o) => Math.Max(Power(o), Power(Roster[o.Card.Id]));

    // The bench of 10 the player levels: the strongest heroes that could share a team.
    List<Owned> Bench() => Pick(Roster.Values.OrderByDescending(BenchPower), 10);

    // The strongest 5 heroes, copies of one hero included (doc 04 section 2.1), nudged toward heroes
    // whose type does well against the enemies, and with a tank and a healer when there are decent ones.
    List<Owned> BestTeam(List<UnitDef> enemies, bool copies = true)
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
        var score = Roster.Values.ToDictionary(o => o, Score);
        var ranked = Roster.Values.OrderByDescending(o => score[o]).ToList();
        var team = new List<Owned>();
        double top = ranked.Count > 0 ? score[ranked[0]] : 0;
        foreach (var role in new[] { "Tank", "Support" })
        {
            var o = ranked.FirstOrDefault(h => h.Card.Role == role && Fits(team, h, copies));
            if (o != null && score[o] >= top * 0.7) team.Add(o);
        }
        // Then the strongest picks, a further copy of a hero counting for what a second copy adds.
        while (team.Count < 5)
        {
            var o = ranked.Where(h => Fits(team, h, copies)).OrderByDescending(h => score[h] * CopyFactor(team, h)).FirstOrDefault();
            if (o == null) break;
            team.Add(o);
        }
        return team;
    }

    // Copies of one hero may share a team. Different heroes on one test kit may not: the kit stands in
    // for heroes with kits of their own, and fielding it twice would fight like copies they aren't.
    static bool Fits(List<Owned> team, Owned o, bool copies = true) => !team.Contains(o) && team.All(t => t.Card.Kit != o.Card.Kit || (copies && t.Card.Id == o.Card.Id));

    static double CopyFactor(List<Owned> team, Owned o) => team.Any(t => t.Card.Id == o.Card.Id) ? Math.Pow(o.Card.CopyPower, 2) : 1;

    // The first heroes in this order that can share a team.
    static List<Owned> Pick(IEnumerable<Owned> heroes, int count = 5)
    {
        var team = new List<Owned>();
        foreach (var o in heroes) if (team.Count < count && Fits(team, o)) team.Add(o);
        return team;
    }

    readonly Dictionary<(int, int), int> lossesHere = new Dictionary<(int, int), int>();

    // A different line-up after a loss: any 5 heroes close to the strongest ones, keeping a tank and
    // a healer when the pool has them. Players rearrange and try counters before grinding (doc 10).
    List<Owned> VariantTeam(List<UnitDef> enemies, int variant, double barFactor = 0.85)
    {
        var best = BestTeam(enemies);
        var all = Roster.Values.OrderByDescending(HeroScale).ToList();
        var strongest = Pick(all);
        double bar = strongest.Count >= 5 ? HeroScale(strongest[4]) * barFactor : 0;
        var pool = all.Where(o => HeroScale(o) >= bar).ToList();
        if (pool.Count <= 5) return best;
        var r = new Random(variant * 7919 + Diff * 31 + Battle);
        for (int tries = 0; tries < 20; tries++)
        {
            var team = Pick(pool.OrderBy(_ => r.Next()));
            bool Has(string role) => team.Any(t => t.Card.Role == role) || pool.All(t => t.Card.Role != role);
            if (Has("Tank") && Has("Support")) return team;
        }
        return best;
    }

    // After a couple of losses the player studies the fight and tries counters (doc 10: rearrange
    // before grinding). Modelled as picking, from the best line-up, the best one without copies and a
    // spread of other line-ups, the one that does best in a few practice fights. Re-thought now and
    // then as the roster grows.
    readonly Dictionary<(int, int), (int at, List<Owned> team)> plans = new Dictionary<(int, int), (int, List<Owned>)>();
    List<Owned> Counter(string stage, List<UnitDef> enemies, int tries)
    {
        if (plans.TryGetValue((Diff, Battle), out var plan) && tries < plan.at * 2 + 4) return plan.team;   // re-think less and less often
        var main = BestTeam(enemies);
        var candidates = new List<List<Owned>> { main, BestTeam(enemies, false) };
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
        // Heroes on the same kit (copies, or heroes the kit stands in for) take its slots in turn.
        var byKit = team.GroupBy(o => o.Card.Kit).ToDictionary(g => g.Key, g => new Queue<Owned>(g));
        foreach (var s in placed) s.HpScale = s.AtkScale = scale > 0 ? scale : HeroScale(byKit[s.Id].Dequeue());
        return placed;
    }

    // ---------- Upgrades ----------

    void Upgrade()
    {
        var team = BestTeam(null);
        // A bench of 10 is levelled together, so there are real line-ups to switch to when a battle needs a counter.
        var core = Bench();
        // Kept copies that fell off the bench and out of the team become fodder; their levels come back as XP.
        foreach (var o in Roster.Values.Where(o => o.Copy && !core.Contains(o) && !team.Contains(o)).ToList())
        {
            Roster.Remove(Roster.First(kv => kv.Value == o).Key);
            xp += XpBetween(1, o.Level);
            spares[o.Stars]++;
        }
        // Gear first, up to what the current battle recommends, so gold isn't all spent on levels.
        var rec = C.Recommended(Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1));
        UpgradeGear(team, (int)Math.Ceiling(rec.gear));
        // Levels: the team up to the recommended level first, then the whole bench, lowest first.
        // Capped team heroes ascend before the bench takes the XP, which making fodder needs for a while.
        // When making their fodder is short of XP, the player saves XP for it instead of levelling the bench.
        foreach (var (group, upTo) in new[] { (team, (int)Math.Ceiling(rec.level)), (core, int.MaxValue) })
        {
            if (group == core && savingXp) break;
            while (true)
            {
                var o = group.Where(h => h.Level < Math.Min(upTo, C.Growth.LevelCap(h.Stars))).OrderBy(h => h.Level).FirstOrDefault();
                if (o == null) break;
                double cost = C.Growth.XpToNext(o.Level);
                if (xp < cost) break;
                xp -= cost; o.Level++;
            }
            if (group == team) Ascend(Growing(team));
        }
        // Then gear beyond the recommendation, and skills, for the team and kept copies.
        var growing = Growing(team);
        UpgradeGear(growing, C.Growth.MaxGear);
        while (true)
        {
            var o = growing.Where(h => h.Skill < C.Growth.MaxSkill).OrderBy(h => h.Skill).FirstOrDefault();
            if (o == null) break;
            double need = C.Growth.TomesToNext(o.Skill);
            if (tomes < need) break;
            tomes -= need; o.Skill++;
        }
        Ascend(growing);
    }

    // The team, then kept copies still being built up to take their place in it.
    List<Owned> Growing(List<Owned> team) => team.Concat(Roster.Values.Where(o => o.Copy && !team.Contains(o))).ToList();

    // Ascension: a capped hero takes fodder of its own star rank, the strongest first.
    bool savingXp;   // a capped team hero is waiting for fodder that needs more XP than the player has
    void Ascend(List<Owned> team)
    {
        savingXp = false;
        foreach (var o in team.Where(h => h.Level >= C.Growth.LevelCap(h.Stars) && h.Stars < C.Growth.MaxStars).OrderByDescending(Power))
            if (TakeFodder(o.Stars, C.Growth.FodderToAscend(o.Stars)))
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
            var o = team.Where(h => h.Gear < Math.Min(upTo, C.Growth.MaxGear)).OrderBy(h => h.Gear).FirstOrDefault();
            if (o == null) break;
            double mats = C.Growth.GearMatsToNext(o.Gear), g = C.Growth.GearGoldToNext(o.Level);
            if (gearMats < mats || gold < g) break;
            gearMats -= mats; gold -= g; o.Gear++;
        }
    }

    // Fodder of a star rank, made from lower-rank spare copies when there isn't enough (doc 07 section 1).
    // Fodder made but not used stays in the pool.
    bool TakeFodder(int rank, int count)
    {
        while (vessels[rank] + spares[rank] + made[rank] < count)
            if (!MakeFodder(rank)) return false;
        Feed(rank, count);
        return true;
    }

    // One fodder hero of this rank: a spare copy a rank lower, levelled to its cap with XP and ascended
    // with that rank's fodder, like any other hero. Vessels can't be levelled, so they're only fodder of
    // their own rank. The XP is on loan: it comes back as potions when the hero is fed (doc 08 section 4).
    bool MakeFodder(int rank)
    {
        int r = rank - 1;
        if (r < 1) return false;
        if (spares[r] + made[r] == 0 && !MakeFodder(r)) return false;
        int feed = C.Growth.FodderToAscend(r);
        while (vessels[r] + spares[r] + made[r] - 1 < feed)
            if (!MakeFodder(r)) return false;
        bool levelled = made[r] > 0;   // already at the cap of the rank below, so it needs fewer levels
        double cost = XpBetween(levelled ? C.Growth.LevelCap(r - 1) : 1, C.Growth.LevelCap(r));
        if (xp < cost) { savingXp = true; return false; }
        xp -= cost;
        if (levelled) made[r]--; else spares[r]--;
        Feed(r, feed);
        made[rank]++; FodderMade++;
        busy += C.FodderMinutes;
        return true;
    }

    // Vessels go first, then spare copies; levelled fodder last, since it can be ascended further.
    void Feed(int rank, int count)
    {
        for (int i = 0; i < count; i++)
            if (vessels[rank] > 0) vessels[rank]--;
            else if (spares[rank] > 0) spares[rank]--;
            else { made[rank]--; xp += XpBetween(1, C.Growth.LevelCap(rank - 1)); }
    }

    // XP from one level to a higher one.
    readonly Dictionary<(int, int), double> xpBetween = new Dictionary<(int, int), double>();
    double XpBetween(int from, int to)
    {
        if (!xpBetween.TryGetValue((from, to), out var x))
            xpBetween[(from, to)] = x = Enumerable.Range(from, Math.Max(0, to - from)).Sum(l => C.Growth.XpToNext(l));
        return x;
    }

    // Menu time spent since the last check comes out of the session's minutes; what doesn't fit waits for the next session.
    double Busy(double minutes)
    {
        double used = Math.Min(busy, Math.Max(0, minutes));
        busy -= used;
        return minutes - used;
    }

    // ---------- Heroes and summons ----------

    void Gain(HeroCard card, bool fromPull, string how = "Summons")
    {
        HeroesFrom[how + (Roster.ContainsKey(card.Id) ? " (duplicate)" : "")] = (HeroesFrom.TryGetValue(how + (Roster.ContainsKey(card.Id) ? " (duplicate)" : ""), out var n) ? n : 0) + 1;
        if (Roster.ContainsKey(card.Id))
        {
            // A duplicate is its own hero. Once the team is fully ascended (until then its rank's fodder
            // is worth more), the player keeps it to field when its hero is in the team and, grown as far,
            // it would clearly beat one of the team's heroes that no kept copy is already meant to replace
            // (a Legendary copy over a Rare, say). Otherwise it's a spare copy for fodder.
            var team = BestTeam(null);
            int weaker = team.Count(t => !t.Copy && t.Card != card && 1.1 * Potential(t.Card) < Potential(card) * Math.Pow(card.CopyPower, 2));
            if (team.Count == 5 && team.Contains(Roster[card.Id]) && team.All(t => t.Stars == C.Growth.MaxStars) && Roster.Values.Count(o => o.Copy) < weaker)
            {
                int copy = 2;
                while (Roster.ContainsKey(card.Id + "#" + copy)) copy++;
                Roster[card.Id + "#" + copy] = new Owned { Card = card, Stars = C.Growth.StartStars(card.Rarity), Copy = true };
            }
            else spares[C.Growth.StartStars(card.Rarity)]++;
            return;
        }
        Roster[card.Id] = new Owned { Card = card, Stars = C.Growth.StartStars(card.Rarity) };
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
        while (true)
        {
            var pay = C.Summon.HowToPay(tickets, shards);
            if (pay == SummonPayment.None) return;
            if (pay == SummonPayment.Ticket) tickets--; else shards -= C.Summon.Cost;
            if (today != null) today.Pulls++;
            Gain(C.Summon.Pull(pity, rng, () => Roster.Values.Any(o => Rank(o.Card.Rarity) <= Rank("Epic")),
                rarity => Released.Where(h => h.Rarity == rarity).ToList()), true);
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
            vessels[kv.Key] += whole;
        }
    }

    // A free pick: the strongest hero of that rarity not yet owned.
    void Pick(string rarity)
    {
        var c = Released.Where(h => h.Rarity == rarity && !Roster.ContainsKey(h.Id)).OrderByDescending(h => h.KitPower).FirstOrDefault();
        if (c != null) { Gain(c, true, "Free picks"); today.BigMoment = true; }
    }

    // ---------- Rewards ----------

    // The current campaign battle, where rewards counted in level-ups are valued (CampaignRewards).
    (int diff, int k) Here => (Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1));

    // A reward from progression.json, valued by the shared rules: XP, gold, gear materials and tomes follow
    // the difficulty's rewardScale, except from Boss Hunts and Endless (scaled: false), whose tables already
    // list the amount for each difficulty.
    void Add(Reward r, double times, bool scaled = true) => Add(C.Rewards.Value(r, Here.diff, Here.k, scaled).Times(times));

    // Amounts are expected values; Vessels are rolled. The Pilgrim's Path adds to XP and gold.
    void Add(Loot l)
    {
        double bonus = spend.PilgrimsPath ? C.PassBonus : 0;
        xp += l.Xp * (1 + bonus);
        gold += l.Gold * (1 + bonus);
        Earn(l.Godshards);
        devotion += l.Devotion;
        gearMats += l.GearMats;
        tomes += l.SkillTomes;
        tickets += l.SummonTickets;
        heroShards += l.HeroShards;
        if (l.HeroShards > 0) HeroShardsFrom[src] = (HeroShardsFrom.TryGetValue(src, out var hv) ? hv : 0) + l.HeroShards;
        AddVessels(l.Vessels, 1);
        if (l.HeroShards > 0) SpendHeroShards();
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
