// Reads progression.json (Unity/Assets/Resources/BattleData) into plain objects, and builds the
// generated campaign: 4 difficulties x 10 realms x 10 stages x 4 battles, each scaled to a recommended level.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ShatteredPantheon.Battle;

class Reward
{
    public double XpLevels, GoldLevels, Godshards, Devotion, GearMats, SkillTomes, SummonTickets, HeroShards;
    public Dictionary<int, double> Vessels = new Dictionary<int, double>();   // star rank -> expected count
    public static Reward From(JsonElement e)
    {
        var r = new Reward
        {
            XpLevels = Cfg.N(e, "xpLevels"), GoldLevels = Cfg.N(e, "goldLevels"), Godshards = Cfg.N(e, "godshards"),
            Devotion = Cfg.N(e, "devotion"), GearMats = Cfg.N(e, "gearMats"), SkillTomes = Cfg.N(e, "skillTomes"),
            SummonTickets = Cfg.N(e, "summonTickets"), HeroShards = Cfg.N(e, "heroShards"),
        };
        if (e.TryGetProperty("vessels", out var v)) r.Vessels = Cfg.Vessels(v);
        return r;
    }
    public static List<Reward> List(JsonElement e) => e.EnumerateArray().Select(From).ToList();
}

class Mode { public int UnlockBattles, FreePerDay; public double DevotionCost, Minutes; public List<Reward> ByDifficulty; }

class Difficulty
{
    public string Name;
    public double[] Levels, Stars, Gear, Skill;
    public double Curve, RewardScale = 1;
}

class TimeProfile { public string Name; public double Minutes; public int Sessions; }
class SpendProfile
{
    public string Name;
    public bool PilgrimsPath, ShrineBlessing, StarterOffer;
    public double PackDollarsPerMonth, DevotionRefillsPerDay;
}

class Cfg
{
    public static double N(JsonElement e, string k, double def = 0) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;
    static double[] Arr(JsonElement e, string k) => e.GetProperty(k).EnumerateArray().Select(x => x.GetDouble()).ToArray();
    static Dictionary<string, double> Map(JsonElement e) => e.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number).ToDictionary(p => p.Name, p => p.Value.GetDouble());
    public static Dictionary<int, double> Vessels(JsonElement e) => Map(e).ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
    static bool B(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

    // Hero growth
    public double LevelGain, StarGain, GearGain, SkillGain;
    public Dictionary<string, double> RarityBase, RarityStars;
    public Dictionary<int, int> LevelCap = new Dictionary<int, int>();
    public int MaxGear, MaxSkill;
    // Costs
    public double XpA, XpExp, GoldPerXp, GearMatsPerTier, GearGoldLevels, TomesPerLevel;
    public Dictionary<int, int> AscendFodder = new Dictionary<int, int>();
    // Roster and summons
    public List<string> Rarities;                        // rarest first
    public Dictionary<string, int> Launch;
    public List<string> Release; public int ReleaseEvery;
    public List<string> Starters; public string StarterRarity;
    public double SummonCost; public Dictionary<string, double> Rates; public bool FirstTenEpic;
    public List<(string rarity, int every)> Guarantees = new List<(string, int)>();
    public Dictionary<string, double> ShardUnlock;
    // Shop (doc 08)
    public List<(double price, double godshards)> Packs = new List<(double, double)>();
    public Reward StarterOffer, PassReward; public double StarterPrice, PassPrice, PassDays, PassBonus;
    public double BlessingPrice, BlessingDays, BlessingNow, BlessingPerDay, BlessingDevotion;
    public double RefillDevotion; public double[] RefillCosts;
    // Campaign
    public List<Difficulty> Difficulties = new List<Difficulty>();
    public int Stages, BattlesPerStage, StagesPerRealm;
    public List<string> Realms = new List<string>();
    public double EnemyFactor, BossFactor;
    public double[] StarSteps, WinBand, BossWinBand, RealmBossWinBand, EarlyWinBand;
    public int EarlyBattles;
    public double RewardEase;
    // Rewards
    public Reward ShrinePerHour, FirstClear, StageClear, StarChest, Farm, Daily, Weekly;
    public double[] StageChestStars; public List<Reward> StageChests;
    public List<Dictionary<int, double>> StageClearVessels;
    public List<Reward> FarmByDifficulty;
    public Mode BossHunts, Endless;
    public double StartingGodshards, LoginEvery, LoginTickets;
    public double ShrineCapHours, StarGodshards, StarChestEvery, FarmCost, DailyMinutes, DailyHardCount, DailyHardGodshards, DailyHardPower;
    public Dictionary<string, double> CodexFirst; public Dictionary<int, double> CodexStars = new Dictionary<int, double>();
    public SortedDictionary<double, double> CodexFaction = new SortedDictionary<double, double>();
    public int LegendaryPickBattles = -1, EpicPickDay = -1;
    public double DevotionRegenPerDay, DevotionRegenCap;
    // Players
    public List<TimeProfile> Times = new List<TimeProfile>();
    public List<SpendProfile> Spends = new List<SpendProfile>();
    public double FightOverhead, ReplaySpeed; public int AttemptsBeforeUpgrading;
    // Targets
    public Dictionary<string, double> NormalDays, HardDays;
    public double WallDays, NewHeroEveryDays, FirstLegendaryDay, MaxDaysWithoutBig;
    public double[] LightFaster;
    public SortedDictionary<int, double[]> CollectionShare = new SortedDictionary<int, double[]>();
    public double LegendaryShareMax, TimePays, LateWallDays;
    public Dictionary<string, double> SpendGapLate = new Dictionary<string, double>();
    public double[] PullsMonth1, PullsLater;

    public int BattlesPerDifficulty => Stages * BattlesPerStage;

    // Godshards for a month's spend on packs: the biggest packs that fit, then smaller ones.
    public double PackGodshards(double dollars)
    {
        double g = 0;
        foreach (var (price, shards) in Packs.OrderByDescending(p => p.price))
            while (dollars >= price - 0.01) { dollars -= price; g += shards; }
        return g;
    }

    public double DollarsPerMonth(SpendProfile s)
    {
        double d = (s.PilgrimsPath ? PassPrice : 0) + (s.ShrineBlessing ? BlessingPrice : 0), left = s.PackDollarsPerMonth;
        foreach (var (price, _) in Packs.OrderByDescending(p => p.price))
            while (left >= price - 0.01) { left -= price; d += price; }
        return d;
    }

    public static Cfg Load(string path)
    {
        var r = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        var c = new Cfg();
        var g = r.GetProperty("heroGrowth");
        c.LevelGain = N(g, "levelGain"); c.StarGain = N(g, "starGain"); c.GearGain = N(g, "gearGain"); c.SkillGain = N(g, "skillGain");
        c.RarityBase = Map(g.GetProperty("rarityBase")); c.RarityStars = Map(g.GetProperty("rarityStars"));
        foreach (var kv in Map(g.GetProperty("levelCapByStars"))) c.LevelCap[int.Parse(kv.Key)] = (int)kv.Value;
        c.MaxGear = (int)N(g, "maxGear"); c.MaxSkill = (int)N(g, "maxSkill");

        var co = r.GetProperty("costs");
        c.XpA = N(co.GetProperty("xpToNext"), "a"); c.XpExp = N(co.GetProperty("xpToNext"), "exp");
        c.GoldPerXp = N(co, "goldPerXp"); c.GearMatsPerTier = N(co.GetProperty("gearMats"), "perTier");
        c.GearGoldLevels = N(co, "gearGoldLevels"); c.TomesPerLevel = N(co.GetProperty("skillTomes"), "perLevel");
        foreach (var kv in Map(co.GetProperty("ascendFodder"))) c.AscendFodder[int.Parse(kv.Key)] = (int)kv.Value;

        var ro = r.GetProperty("roster");
        List<string> Strs(JsonElement e, string k) => e.GetProperty(k).EnumerateArray().Select(x => x.GetString()).ToList();
        c.Rarities = Strs(ro, "rarities");
        c.Launch = Map(ro.GetProperty("launch")).ToDictionary(kv => kv.Key, kv => (int)kv.Value);
        c.Release = Strs(ro, "release"); c.ReleaseEvery = (int)N(ro, "releaseEveryDays");
        c.Starters = Strs(ro, "starters"); c.StarterRarity = ro.GetProperty("starterRarity").GetString();

        var su = r.GetProperty("summon");
        c.SummonCost = N(su, "cost"); c.Rates = Map(su.GetProperty("rates"));
        foreach (var gu in su.GetProperty("guarantees").EnumerateArray()) c.Guarantees.Add((gu.GetProperty("rarity").GetString(), (int)N(gu, "every")));
        c.FirstTenEpic = B(su, "firstTenGuaranteesEpic");
        c.ShardUnlock = Map(r.GetProperty("heroShards").GetProperty("unlock"));

        var sh = r.GetProperty("shop");
        foreach (var pk in sh.GetProperty("godshardPacks").EnumerateArray()) c.Packs.Add((N(pk, "price"), N(pk, "godshards")));
        var so = sh.GetProperty("starterOffer"); c.StarterOffer = Reward.From(so); c.StarterPrice = N(so, "price");
        var pp = sh.GetProperty("pilgrimsPath"); c.PassReward = Reward.From(pp); c.PassPrice = N(pp, "price"); c.PassDays = N(pp, "days"); c.PassBonus = N(pp, "xpGoldBonus");
        var bl = sh.GetProperty("shrineBlessing"); c.BlessingPrice = N(bl, "price"); c.BlessingDays = N(bl, "days"); c.BlessingNow = N(bl, "godshardsNow"); c.BlessingPerDay = N(bl, "godshardsPerDay"); c.BlessingDevotion = N(bl, "devotionPerDay");
        var rf = sh.GetProperty("devotionRefill"); c.RefillDevotion = N(rf, "devotion"); c.RefillCosts = Arr(rf, "godshards");

        var ca = r.GetProperty("campaign");
        foreach (var d in ca.GetProperty("difficulties").EnumerateArray())
            c.Difficulties.Add(new Difficulty { Name = d.GetProperty("name").GetString(), Levels = Arr(d, "levels"), Stars = Arr(d, "stars"), Gear = Arr(d, "gear"), Skill = Arr(d, "skill"), Curve = N(d, "curve", 1), RewardScale = N(d, "rewardScale", 1) });
        c.Stages = (int)N(ca, "stages"); c.BattlesPerStage = (int)N(ca, "battlesPerStage"); c.StagesPerRealm = (int)N(ca, "stagesPerRealm");
        foreach (var rn in ca.GetProperty("realms").EnumerateArray()) c.Realms.Add(rn.GetString());
        c.EnemyFactor = N(ca, "enemyFactor"); c.BossFactor = N(ca, "bossFactor"); c.StarSteps = Arr(ca, "starSteps");
        c.WinBand = Arr(ca, "winBand"); c.BossWinBand = Arr(ca, "bossWinBand"); c.RealmBossWinBand = Arr(ca, "realmBossWinBand");
        c.RewardEase = N(ca, "rewardEase", 1);
        c.EarlyBattles = (int)N(ca, "earlyBattles"); c.EarlyWinBand = Arr(ca, "earlyWinBand");

        var re = r.GetProperty("rewards");
        c.ShrinePerHour = Reward.From(re.GetProperty("shrinePerHour")); c.ShrineCapHours = N(re, "shrineCapHours");
        c.FirstClear = Reward.From(re.GetProperty("firstClear")); c.StageClear = Reward.From(re.GetProperty("stageClear"));
        c.StarGodshards = N(re.GetProperty("star"), "godshards"); c.StarChestEvery = N(re, "starChestEvery");
        c.StarChest = Reward.From(re.GetProperty("starChest"));
        var sc = re.GetProperty("stageChests"); c.StageChestStars = Arr(sc, "stars"); c.StageChests = Reward.List(sc.GetProperty("rewards"));
        c.StageClearVessels = re.GetProperty("stageClearVessels").EnumerateArray().Select(Vessels).ToList();
        c.FarmByDifficulty = Reward.List(re.GetProperty("farmByDifficulty"));
        Mode M(JsonElement e) => new Mode { UnlockBattles = (int)N(e, "unlockBattles"), FreePerDay = (int)N(e, "freePerDay"), DevotionCost = N(e, "devotionCost"), Minutes = N(e, "minutes"), ByDifficulty = Reward.List(e.GetProperty("byDifficulty")) };
        c.BossHunts = M(re.GetProperty("bossHunts")); c.Endless = M(re.GetProperty("endless"));
        c.Farm = Reward.From(re.GetProperty("farm")); c.FarmCost = c.Farm.Devotion; c.Farm.Devotion = 0;
        c.Daily = Reward.From(re.GetProperty("daily")); c.DailyMinutes = N(re.GetProperty("daily"), "minutes");
        var dh = re.GetProperty("dailyHard"); c.DailyHardCount = N(dh, "count"); c.DailyHardGodshards = N(dh, "godshards"); c.DailyHardPower = N(dh, "powerOver");
        c.Weekly = Reward.From(re.GetProperty("weekly"));
        var cx = re.GetProperty("codex");
        c.CodexFirst = Map(cx.GetProperty("firstCopy"));
        foreach (var kv in Map(cx.GetProperty("stars"))) c.CodexStars[int.Parse(kv.Key)] = kv.Value;
        foreach (var kv in Map(cx.GetProperty("factionShare"))) c.CodexFaction[double.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture)] = kv.Value;
        foreach (var m in re.GetProperty("milestones").EnumerateArray())
        {
            if (m.TryGetProperty("legendaryPick", out _)) c.LegendaryPickBattles = (int)N(m, "battles");
            if (m.TryGetProperty("epicPick", out _)) c.EpicPickDay = (int)N(m, "day");
        }
        c.StartingGodshards = N(re, "startingGodshards");
        var lc = re.GetProperty("loginCalendar"); c.LoginEvery = N(lc, "everyDays"); c.LoginTickets = N(lc, "summonTickets");
        c.DevotionRegenPerDay = N(re, "devotionRegenPerDay"); c.DevotionRegenCap = N(re, "devotionRegenCap");

        var pl = r.GetProperty("players");
        foreach (var t in pl.GetProperty("time").EnumerateArray()) c.Times.Add(new TimeProfile { Name = t.GetProperty("name").GetString(), Minutes = N(t, "minutes"), Sessions = (int)N(t, "sessions") });
        foreach (var s in pl.GetProperty("spend").EnumerateArray())
            c.Spends.Add(new SpendProfile
            {
                Name = s.GetProperty("name").GetString(), PilgrimsPath = B(s, "pilgrimsPath"), ShrineBlessing = B(s, "shrineBlessing"), StarterOffer = B(s, "starterOffer"),
                PackDollarsPerMonth = N(s, "packDollarsPerMonth"), DevotionRefillsPerDay = N(s, "devotionRefillsPerDay"),
            });
        c.FightOverhead = N(pl, "secondsPerFightOverhead"); c.ReplaySpeed = N(pl, "replaySpeed"); c.AttemptsBeforeUpgrading = (int)N(pl, "attemptsBeforeUpgrading");

        var ta = r.GetProperty("targets");
        c.NormalDays = Map(ta.GetProperty("normalDays")); c.HardDays = Map(ta.GetProperty("hardDays"));
        c.WallDays = N(ta, "wallDays"); c.NewHeroEveryDays = N(ta, "newHeroEveryDays"); c.FirstLegendaryDay = N(ta, "firstLegendaryDay");
        c.MaxDaysWithoutBig = N(ta, "maxDaysWithoutBigMoment"); c.LightFaster = Arr(ta, "lightSpenderFaster");
        var cl = ta.GetProperty("collection");
        foreach (var kv in cl.GetProperty("rosterShare").EnumerateObject()) c.CollectionShare[int.Parse(kv.Name)] = kv.Value.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        c.LegendaryShareMax = N(cl, "legendaryShareMax");
        c.TimePays = N(ta, "timePays"); c.LateWallDays = N(ta, "lateWallDays"); foreach (var sg in ta.GetProperty("spendGapLate").EnumerateObject()) c.SpendGapLate[sg.Name] = sg.Value.GetDouble();
        c.PullsMonth1 = Arr(ta.GetProperty("pullsPerDayFree"), "month1"); c.PullsLater = Arr(ta.GetProperty("pullsPerDayFree"), "later");
        return c;
    }

    // ---------- Growth and costs ----------

    public double XpToNext(int level) => Math.Round(XpA * Math.Pow(level, XpExp));

    public double Scale(string rarity, double level, double stars, double gear, double skill) =>
        RarityBase[rarity] * (1 + LevelGain * (level - 1)) * (1 + StarGain * (stars - 3)) * (1 + GearGain * gear) * (1 + SkillGain * (skill - 1));

    public int Cap(int stars) => LevelCap.TryGetValue(stars, out var c) ? c : LevelCap.Values.Max();

    static double Lerp(double[] a, double t) => a[0] + (a[1] - a[0]) * t;

    // What an Epic hero should have reached to take on battle k of a difficulty.
    public (double level, double stars, double gear, double skill) Recommended(int diff, int k)
    {
        var d = Difficulties[diff];
        double t = (double)k / (BattlesPerDifficulty - 1);
        return (Lerp(d.Levels, Math.Pow(t, d.Curve)), Lerp(d.Stars, t), Lerp(d.Gear, t), Lerp(d.Skill, t));
    }

    public double RecScale(int diff, int k) { var r = Recommended(diff, k); return Scale("Epic", r.level, r.stars, r.gear, r.skill); }
    public int RecLevel(int diff, int k) => (int)Math.Round(Recommended(diff, k).level);

    // ---------- The generated campaign ----------

    static readonly string[] Fronts = { "knight", "boar", "golem", "raider", "stalker", "acolyte" };
    static readonly string[] Backs = { "archer", "priest", "shaman", "smith", "howler" };

    public static string StageId(int diff, int k) => $"c{diff}-{k}";

    // Enemies are drawn from the test enemies by a seeded recipe: three early on, then four or five,
    // and the Hollow Saint with acolytes as every stage's 10th battle.
    public List<StageDef> BuildCampaign()
    {
        var list = new List<StageDef>();
        for (int d = 0; d < Difficulties.Count; d++)
            for (int k = 0; k < BattlesPerDifficulty; k++)
            {
                var rng = new Random(d * 100003 + k * 7919 + 17);
                bool boss = IsBoss(k);
                double scale = RecScale(d, k) * EnemyFactor * (boss ? BossFactor : 1);
                var st = new StageDef { Id = StageId(d, k), Name = BattleName(k), Realm = Realms[RealmOf(k)], RealmNumber = RealmOf(k) + 1, StageNumber = StageInRealm(k) + 1, BattleNumber = k % BattlesPerStage + 1, Boss = IsRealmBoss(k) ? "realm" : boss ? "stage" : null, Difficulty = Difficulties[d].Name.ToLowerInvariant(), HpScale = scale, AtkScale = scale };
                if (boss)
                {
                    st.Formation = "2-3";
                    st.Enemies.Add(new StageEnemy { Id = "saint", Slot = "front0" });
                    foreach (var s in new[] { "front1", "back0", "back2" }) st.Enemies.Add(new StageEnemy { Id = "acolyte", Slot = s });
                }
                else
                {
                    int count = d == 0 && k < 20 ? 3 : d == 0 && k < 60 ? 4 : 5;
                    st.Formation = rng.NextDouble() < 0.3 ? "3-2" : "2-3";
                    var slots = Battle.SlotsOf(st.Formation).ToList();
                    var fronts = slots.Where(s => s.StartsWith("front")).ToList();
                    var backs = slots.Where(s => s.StartsWith("back")).ToList();
                    int nf = count == 5 ? fronts.Count : 2;
                    for (int i = 0; i < nf; i++)
                    {
                        string id;
                        do id = Fronts[rng.Next(Fronts.Length)];
                        while (st.Enemies.Count(e => e.Id == id) >= 2);
                        st.Enemies.Add(new StageEnemy { Id = id, Slot = fronts[i] });
                    }
                    // At most one healer, and no more than two of any enemy: stacked healers stall most
                    // line-ups at any power, which reads as a wall rather than a puzzle.
                    for (int i = 0; i < count - nf && i < backs.Count; i++)
                    {
                        string id;
                        do id = Backs[rng.Next(Backs.Length)];
                        while (st.Enemies.Count(e => e.Id == id) >= (id == "priest" ? 1 : 2));
                        st.Enemies.Add(new StageEnemy { Id = id, Slot = backs[i] });
                    }
                }
                list.Add(st);
            }
        return list;
    }

    public bool IsBoss(int k) => k % BattlesPerStage == BattlesPerStage - 1;
    public bool IsRealmBoss(int k) => IsBoss(k) && StageOf(k) % StagesPerRealm == StagesPerRealm - 1;
    public int StageOf(int k) => k / BattlesPerStage;                           // 0-based stage in the difficulty
    public int RealmOf(int k) => StageOf(k) / StagesPerRealm;                   // 0-based realm
    public int StageInRealm(int k) => StageOf(k) % StagesPerRealm;              // 0-based
    // "Hearth 3-4": realm, stage in the realm, battle in the stage.
    public string BattleName(int k) => $"{Realms[RealmOf(k)]} {StageInRealm(k) + 1}-{k % BattlesPerStage + 1}";

    // The content pipeline's check (doc 06 section 11.1): random teams of Epic heroes at exactly the
    // recommended power should win each battle within its band. Battles outside it are scaled up or
    // down until they fit, so no generated battle is a surprise spike or a pushover.
    public void Calibrate(GameData data, List<StageDef> stages, int fights = 24)
    {
        System.Threading.Tasks.Parallel.For(0, stages.Count, i =>
        {
            var st = stages[i];
            int d = i / BattlesPerDifficulty, k = i % BattlesPerDifficulty;
            // The first battles teach the game, so they should be won almost every time.
            var band = d == 0 && k < EarlyBattles ? EarlyWinBand : IsRealmBoss(k) ? RealmBossWinBand : IsBoss(k) ? BossWinBand : WinBand;
            var one = new GameData { Rules = data.Rules, Heroes = data.Heroes, Enemies = data.Enemies, Stages = new List<StageDef> { st } };
            double rec = RecScale(d, k);
            for (int iter = 0; iter < 8; iter++)
            {
                var rng = new Random(i * 31 + 7);
                int wins = 0;
                for (int f = 0; f < fights; f++)
                {
                    var ids = data.Heroes.OrderBy(_ => rng.Next()).Take(5).Select(h => h.Id).ToList();
                    var team = Formation.AutoPlace(data, ids);
                    foreach (var t in team) t.HpScale = t.AtkScale = rec;
                    if (Battle.Run(one, team, st.Id, i * 1000 + f + 1).Result == "win") wins++;
                }
                double wr = (double)wins / fights;
                if (wr > band[1]) { st.HpScale *= 1.08; st.AtkScale *= 1.08; }
                else if (wr < band[0]) { st.HpScale *= 0.93; st.AtkScale *= 0.93; }
                else break;
            }
        });
    }

    public static string ToJson(List<StageDef> stages)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder("[\n");
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            sb.Append($" {{\"id\":\"{s.Id}\",\"name\":\"{s.Name}\",\"difficulty\":\"{s.Difficulty}\",\"formation\":\"{s.Formation}\",\"realm\":\"{s.Realm}\",\"realmNumber\":{s.RealmNumber},\"stage\":{s.StageNumber},\"battle\":{s.BattleNumber},{(s.Boss != null ? $"\"boss\":\"{s.Boss}\"," : "")}\"hpScale\":{s.HpScale.ToString("0.###", inv)},\"atkScale\":{s.AtkScale.ToString("0.###", inv)},\"enemies\":[");
            sb.Append(string.Join(",", s.Enemies.Select(e => $"[\"{e.Id}\",\"{e.Slot}\"]")));
            sb.Append(i < stages.Count - 1 ? "]},\n" : "]}\n");
        }
        return sb.Append("]\n").ToString();
    }
}
