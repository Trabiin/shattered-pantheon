// One simulated player, played day by day. Campaign battles are real fights on the battle engine;
// everything else (rewards, upgrades, summons, star challenges, farming) follows progression.json.
using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredPantheon.Battle;

class HeroCard
{
    public string Id, Kit, Name, Rarity, Faction, Role;
    public double KitPower;   // sqrt(health x attack) of the kit, so heroes compare across roles
}

class Owned
{
    public HeroCard Card;
    public int Level = 1, Stars, Gear, Skill = 1;
}

class DayLog
{
    public int Day, Difficulty, Battle, Roster, TeamLevel, Stars, Fights, FarmFights;
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
    readonly Dictionary<int, int> fodder = new Dictionary<int, int> { [3] = 0, [4] = 0, [5] = 0, [6] = 0 };
    double xp, gold, shards, devotion, gearMats, tomes, tickets, shardsEarned;
    int pullsSinceEpic, pullsSinceLegendary, pullsTotal;
    public int Diff, Battle;   // next campaign battle: difficulty and index within it
    int[,] stars;              // stars earned per difficulty and battle (0 = not cleared, 1 = won, 2-5 challenges)
    int starsTotal, starChestsGiven, battlesCleared, attemptsToday;
    bool legendaryPicked, epicPicked;
    readonly HashSet<string> codexDone = new HashSet<string>();
    public readonly List<DayLog> Days = new List<DayLog>();
    DayLog today;
    public int RealFights;
    public Dictionary<string, int> DoneDay = new Dictionary<string, int>();   // milestone -> day
    public int FirstLegendaryDay = -1;

    public Player(Cfg c, GameData data, List<HeroCard> cards, TimeProfile time, SpendProfile spend, long seed)
    {
        C = c; this.data = data; this.cards = cards; this.time = time; this.spend = spend; this.seed = seed;
        rng = new Random((int)(seed * 7919 % int.MaxValue));
        stars = new int[c.Difficulties.Count, c.BattlesPerDifficulty];
        foreach (var kit in c.Starters) Gain(cards.First(h => h.Kit == kit && h.Rarity == "Rare"), false);
        shards = c.StartingGodshards;   // the first 10-pull, a few minutes in
    }

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
            Add(C.ShrinePerHour, Math.Min(away, C.ShrineCapHours));
            devotion += Math.Min(C.DevotionRegenCap, C.DevotionRegenPerDay / time.Sessions);
            if (s == 0)
            {
                minutes -= C.DailyMinutes;
                Add(C.Daily, 1);
                var team = BestTeam(null);
                if (TeamScale(team) >= C.RecScale(Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1)) * C.DailyHardPower) Earn(C.DailyHardGodshards * C.DailyHardCount);
                if (day % 7 == 0) Add(C.Weekly, 1);
                if (day % C.LoginEvery == 0) { tickets += C.LoginTickets; today.BigMoment = true; }
                Earn(spend.GodshardsPerDay);
                devotion += spend.DevotionRefills * C.DevotionRegenCap;
                if (day == C.EpicPickDay && !epicPicked) { epicPicked = true; Pick("Epic"); }
            }
            Upgrade();
            Summon();
            minutes = PushCampaign(minutes);
            Upgrade();
            minutes = Challenges(minutes);
            minutes = FarmFights(minutes);
            today.MinutesUnused += Math.Max(0, minutes);
            Upgrade();
            Summon();
        }

        var t = BestTeam(null);
        today.Difficulty = Diff; today.Battle = Battle; today.Roster = Roster.Count;
        today.TeamLevel = t.Count == 0 ? 0 : (int)Math.Round(t.Average(o => o.Level));
        if (t.Count > 0) { today.TeamStars = t.Average(o => o.Stars); today.TeamGear = t.Average(o => o.Gear); today.TeamSkill = t.Average(o => o.Skill); }
        today.TeamScale = TeamScale(t); today.RecScale = C.RecScale(Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1));
        today.Stars = starsTotal; today.Godshards = shards; today.GodshardsEarned = shardsEarned - before;
        today.Progressed = battlesCleared > clearedBefore;
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
            var team = tries % 2 == 0 ? BestTeam(enemies) : VariantTeam(enemies, tries);
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
        Add(C.FirstClear, 1);
        if (k % C.BattlesPerStage == C.BattlesPerStage - 1) { Add(C.StageClear, 1); today.BigMoment = true; }
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
                    Earn(C.StarGodshards);
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
            Add(C.StarChest, 1);
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
            Add(C.Farm, 1);
            minutes -= each;
            today.FarmFights++;
        }
        return minutes;
    }

    // ---------- Team ----------

    double HeroScale(Owned o) => C.Scale(o.Card.Rarity, o.Level, o.Stars, o.Gear, o.Skill);
    double Power(Owned o) => HeroScale(o) * o.Card.KitPower;
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
        var team = new List<Owned>();
        foreach (var o in Roster.Values.OrderByDescending(Score))
        {
            if (team.Any(t => t.Card.Kit == o.Card.Kit)) continue;
            team.Add(o);
            if (team.Count == 5) break;
        }
        if (team.Count == 5 && !team.Any(t => t.Card.Role == "Tank" || t.Card.Role == "Warrior"))
        {
            var front = Roster.Values.Where(o => (o.Card.Role == "Tank" || o.Card.Role == "Warrior") && team.All(t => t.Card.Kit != o.Card.Kit)).OrderByDescending(Power).FirstOrDefault();
            if (front != null) { team.RemoveAt(4); team.Add(front); }
        }
        return team;
    }

    readonly Dictionary<(int, int), int> lossesHere = new Dictionary<(int, int), int>();

    // A line-up drawn from the 10 strongest heroes, keeping at least one front-liner.
    List<Owned> VariantTeam(List<UnitDef> enemies, int variant)
    {
        var best = BestTeam(enemies);
        var pool = Roster.Values.OrderByDescending(Power).GroupBy(o => o.Card.Kit).Select(g => g.First()).Take(10).ToList();
        if (pool.Count <= 5) return best;
        var r = new Random(variant * 7919 + Diff * 31 + Battle);
        for (int tries = 0; tries < 20; tries++)
        {
            var team = pool.OrderBy(_ => r.Next()).Take(5).ToList();
            if (team.Any(t => t.Card.Role == "Tank" || t.Card.Role == "Warrior")) return team;
        }
        return best;
    }

    List<TeamSlot> Place(List<Owned> team)
    {
        var placed = Formation.AutoPlace(data, team.Select(o => o.Card.Kit).ToList());
        foreach (var s in placed)
        {
            var o = team.First(t => t.Card.Kit == s.Id);
            s.HpScale = s.AtkScale = HeroScale(o);
        }
        return placed;
    }

    // ---------- Upgrades ----------

    void Upgrade()
    {
        var team = BestTeam(null);
        var core = Roster.Values.OrderByDescending(Power).Take(8).ToList();
        // Gear first, up to what the current battle recommends, so gold isn't all spent on levels.
        var rec = C.Recommended(Math.Min(Diff, C.Difficulties.Count - 1), Math.Min(Battle, C.BattlesPerDifficulty - 1));
        UpgradeGear(team, (int)Math.Ceiling(rec.gear));
        // Levels: the lowest-level team hero first; the bench only once the team is capped.
        foreach (var group in new[] { team, core })
            while (true)
            {
                var o = group.Where(h => h.Level < C.Cap(h.Stars)).OrderBy(h => h.Level).FirstOrDefault();
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
                if (C.CodexStars.TryGetValue(o.Stars, out var r) && codexDone.Add(o.Card.Id + "*" + o.Stars)) Earn(r);
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
            if (rank <= 3) return false;
            int need = 1 + C.AscendFodder[rank - 1];
            if (!Ensure(rank - 1, need)) return false;
            fodder[rank - 1] -= need;
            fodder[rank]++;
        }
        return true;
    }

    // ---------- Heroes and summons ----------

    void Gain(HeroCard card, bool fromPull)
    {
        if (Roster.ContainsKey(card.Id))
        {
            fodder[(int)C.RarityStars[card.Rarity]]++;
            return;
        }
        Roster[card.Id] = new Owned { Card = card, Stars = (int)C.RarityStars[card.Rarity] };
        if (today != null) { today.NewHero = true; if (card.Rarity != "Rare") today.BigMoment = true; }
        if (card.Rarity == "Legendary" && FirstLegendaryDay < 0) FirstLegendaryDay = today?.Day ?? 0;
        if (fromPull || today != null)
        {
            Earn(C.CodexFirst[card.Rarity]);
            var faction = cards.Where(h => h.Faction == card.Faction).ToList();
            double share = (double)faction.Count(h => Roster.ContainsKey(h.Id)) / faction.Count;
            foreach (var kv in C.CodexFaction)
                if (share >= kv.Key - 1e-9 && codexDone.Add(card.Faction + "%" + kv.Key)) { Earn(kv.Value); if (kv.Value >= 300 && today != null) today.BigMoment = true; }
        }
    }

    void Summon()
    {
        while (tickets >= 1 || shards >= C.SummonCost)
        {
            if (tickets >= 1) tickets--; else shards -= C.SummonCost;
            pullsTotal++; pullsSinceEpic++; pullsSinceLegendary++;
            string rarity;
            double roll = rng.NextDouble();
            if (pullsSinceLegendary >= C.LegendaryPity || roll < C.Rates["Legendary"]) rarity = "Legendary";
            else if (pullsSinceEpic >= C.EpicEvery || (C.FirstTenEpic && pullsTotal == 10 && !Roster.Values.Any(o => o.Card.Rarity != "Rare")) || roll < C.Rates["Legendary"] + C.Rates["Epic"]) rarity = "Epic";
            else rarity = "Rare";
            if (rarity != "Rare") pullsSinceEpic = 0;
            if (rarity == "Legendary") pullsSinceLegendary = 0;
            var pool = cards.Where(h => h.Rarity == rarity).ToList();
            Gain(pool[rng.Next(pool.Count)], true);
        }
    }

    // A free pick: the strongest hero of that rarity not yet owned.
    void Pick(string rarity)
    {
        var c = cards.Where(h => h.Rarity == rarity && !Roster.ContainsKey(h.Id)).OrderByDescending(h => h.KitPower).FirstOrDefault();
        if (c != null) { Gain(c, true); today.BigMoment = true; }
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

    void Add(Reward r, double times)
    {
        double lc = LevelCost(), rs = RewardScale();
        xp += r.XpLevels * lc * times * (1 + spend.Bonus);
        gold += r.GoldLevels * lc * C.GoldPerXp * times * (1 + spend.Bonus);
        Earn(r.Godshards * times);
        devotion += r.Devotion * times;
        gearMats += r.GearMats * times * rs;
        tomes += r.SkillTomes * times * rs;
        tickets += r.SummonTickets * times;
    }

    void Earn(double g) { shards += g; shardsEarned += g; }
}
