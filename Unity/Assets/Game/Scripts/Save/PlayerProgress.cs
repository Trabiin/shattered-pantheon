// What the player has done, kept in the save file on the device (SaveStore): stars earned on each
// battle, the furthest battle cleared on each difficulty, the team of hero cards last used on each
// battle, and (through the Wallet) the hero cards they own and their items, which every win adds to.
// Which battles are open isn't saved: CampaignOrder works it out from the cleared battles.
// Every change is written straight away; GameApp also writes when the app is paused or closed.
// Plain C# with no Unity code, so the save tests run without Unity (tools/SaveTests).
namespace ShatteredPantheon.Game
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ShatteredPantheon.Battle;

    public class PlayerProgress
    {
        readonly GameData data;
        readonly CampaignRewards rewards;
        readonly SaveStore store;
        readonly Action<string> warn;
        readonly CampaignOrder campaign;

        public SaveData Save { get; private set; }
        public Wallet Wallet { get; }

        public PlayerProgress(GameData data, CampaignRewards rewards, SaveStore store, Action<string> warn)
        {
            this.data = data;
            this.rewards = rewards;
            this.store = store;
            this.warn = warn;
            campaign = new CampaignOrder(data.Stages);
            Wallet = new Wallet(data, rewards.Curve.Growth, () => Save, Write);
            Save = store.Load(out string problem);
            if (problem != null) warn(problem);
            if (Save.Heroes.Count == 0) StartingHeroes();
        }

        // A player always owns at least one hero, so a save without any is a new game, or one saved
        // before heroes were kept: it gets the new player's heroes.
        // TODO(E9-F2-S1-T5): summons don't exist yet, so a new game owns every test hero. With summons it
        // owns only the 3 starters (progression.json, roster.starters).
        void StartingHeroes() => Wallet.StartingHeroes(data.Heroes.Select(h => h.Id));

        // The hero cards the player owns, in the order the hero data lists their heroes, copies after the
        // first. A card whose hero no longer exists in the game data is left out.
        public List<OwnedHero> Cards => data.Heroes
            .SelectMany(h => Save.Heroes.Values.Where(c => c.Hero == h.Id).OrderBy(c => c.Id.Length).ThenBy(c => c.Id, StringComparer.Ordinal))
            .ToList();

        // The hero a card is a copy of.
        public UnitDef HeroOf(string card) => data.Hero(Wallet.Hero(card)?.Hero ?? "") ?? throw new ArgumentException("Hero not owned: " + card);

        // What a card's listed health and attack are multiplied by in a fight: its hero's rarity, and its
        // level, star rank and skill level (doc 07 section 1). Gear arrives in E8-F9.
        public double HeroScale(string card)
        {
            var c = Wallet.Hero(card);
            return rewards.Curve.Growth.StatScale(HeroOf(card).Rarity, c.Level, c.Stars, 0, c.Skill);
        }

        // The team of cards as the battle engine takes it: each slot names the card's hero and carries the
        // card's strength.
        public List<TeamSlot> FightingTeam(IEnumerable<TeamSlot> cards) => cards
            .Select(t => new TeamSlot(HeroOf(t.Id).Id, t.Row, t.Slot) { HpScale = HeroScale(t.Id), AtkScale = HeroScale(t.Id) })
            .ToList();

        public IReadOnlyList<int> Stars(string battleId) => Save.Stars.TryGetValue(battleId, out var s) ? s : (IReadOnlyList<int>)Array.Empty<int>();
        public bool IsCleared(string battleId) => Stars(battleId).Contains(1);
        public bool IsOpen(StageDef battle) => campaign.IsOpen(battle, IsCleared);
        public string Furthest(string difficulty) => Save.Furthest.TryGetValue(difficulty, out var id) ? id : null;

        // The player's current campaign battle, where rewards counted in level-ups are valued: the one after
        // the furthest battle cleared on any difficulty (the last battle once everything is cleared).
        public StageDef CurrentBattle()
        {
            int furthest = Save.Furthest.Values.Select(id => campaign.IndexOf(data.Stages.Find(b => b.Id == id))).DefaultIfEmpty(-1).Max();
            return campaign.Battles[Math.Min(furthest + 1, campaign.Battles.Count - 1)];
        }

        // Called when a fight ends, whatever the result, with the hero cards that fought and the fight's
        // seed, and saved. A win gives the battle's reward from progression.json (CampaignRewards): the
        // first win its first-clear reward (on a stage boss with the stage clear and its Vessel chance), a
        // replay the farm reward. Their XP is battle XP, shared by the heroes who fought; everything else
        // goes into the inventory. The win's star and the challenge stars it met (2 to 5) are saved, and
        // stars can open the stage's chests (6, 12 and 20 of its stars) and the star track's (every 100 on a difficulty).
        // The seed makes the reward's chances (a 40% Vessel) the same whenever the same fight is checked.
        public BattleRewards RecordBattle(StageDef battle, string result, IEnumerable<string> cards, int seed = 0, IEnumerable<int> challengeStars = null)
        {
            var got = new BattleRewards();
            if (result == "win")
            {
                var rng = new Random(seed);
                got.FirstWin = !IsCleared(battle.Id);
                var (diff, k) = rewards.Curve.Position(got.FirstWin ? battle : CurrentBattle());
                // TODO(E10-F3-S1-T2): replays are free until Devotion exists; then each costs rewards.ReplayDevotion.
                var loot = (got.FirstWin ? rewards.FirstWin(diff, k) : rewards.Replay(rewards.Curve.Position(battle).diff, diff, k)).Whole(rng);
                var levels = cards.Distinct().Where(Wallet.Owns).ToDictionary(c => c, c => Wallet.Hero(c).Level);
                var shares = Wallet.ShareBattleXp(cards, (long)loot.Xp);
                got.Heroes = levels.Select(c => new HeroXp { Card = c.Key, Xp = shares.TryGetValue(c.Key, out var x) ? x : 0, Capped = !shares.ContainsKey(c.Key), LevelBefore = c.Value, LevelAfter = Wallet.Hero(c.Key).Level }).ToList();
                loot.Xp = 0;
                got.Add(loot);

                var stars = Save.Stars.TryGetValue(battle.Id, out var s) ? s : Save.Stars[battle.Id] = new List<int>();
                int stageBefore = StageStars(battle), difficultyBefore = DifficultyStars(battle);
                var earned = new[] { 1 }.Concat((challengeStars ?? Enumerable.Empty<int>()).Where(n => n >= 2 && n <= 5)).Distinct().Where(n => !stars.Contains(n)).ToList();
                stars.AddRange(earned);
                stars.Sort();
                got.Add(rewards.Stars(earned.Count(n => n > 1), diff, k).Whole(rng));
                for (int chest = rewards.StageChestsAt(stageBefore); chest < rewards.StageChestsAt(StageStars(battle)); chest++)
                {
                    got.Chests.Add($"Stage chest ({rewards.StageChestStars[chest]}★)");
                    got.Add(rewards.StageChest(chest, diff, k).Whole(rng));
                }
                for (int chest = rewards.StarChestsAt(difficultyBefore); chest < rewards.StarChestsAt(DifficultyStars(battle)); chest++)
                {
                    got.Chests.Add($"Star track chest ({(chest + 1) * rewards.StarChestEvery}★)");
                    got.Add(rewards.StarTrackChest(diff, k).Whole(rng));
                }
                Wallet.Earn(got.Items);

                string furthest = Furthest(battle.Difficulty);
                var before = furthest == null ? null : data.Stages.Find(b => b.Id == furthest);
                if (before == null || campaign.IndexOf(battle) > campaign.IndexOf(before)) Save.Furthest[battle.Difficulty] = battle.Id;
            }
            Write();
            return got;
        }

        // The stars earned on a battle's stage (its battles share the stage chests), and on its difficulty (the star track).
        int StageStars(StageDef battle) => StarsWhere(b => b.Difficulty == battle.Difficulty && b.RealmNumber == battle.RealmNumber && b.StageNumber == battle.StageNumber);
        int DifficultyStars(StageDef battle) => StarsWhere(b => b.Difficulty == battle.Difficulty);
        int StarsWhere(Func<StageDef, bool> battles) => campaign.Battles.Where(battles).Sum(b => Stars(b.Id).Count);

        // The battle after this one in the order battles open, if it's open (a win opens it); null after the last.
        public StageDef NextBattle(StageDef battle)
        {
            int i = campaign.IndexOf(battle);
            var next = i >= 0 && i + 1 < campaign.Battles.Count ? campaign.Battles[i + 1] : null;
            return next != null && IsOpen(next) ? next : null;
        }

        public void SaveTeam(string battleId, List<TeamSlot> team, string formation)
        {
            Save.Teams[battleId] = Save.LastTeam = new SavedTeam(formation, team);
            Write();
        }

        // The formation saved with that team ("2-3" or "3-2"); a team that doesn't fill its row of three can't show it.
        public string LoadFormation(string battleId, List<TeamSlot> team)
        {
            string f = (Save.Teams.TryGetValue(battleId, out var t) ? t : Save.LastTeam)?.Formation;
            return (f == "2-3" || f == "3-2") && team.All(s => Battle.SlotsOf(f).Contains(s.Slot)) ? f : Battle.FormationOf(team.Select(s => s.Slot));
        }

        // The team last used on this battle, else the last team used anywhere, else the strongest team.
        public List<TeamSlot> LoadTeam(string battleId)
        {
            var team = Valid(Save.Teams.TryGetValue(battleId, out var t) ? t : null) ?? Valid(Save.LastTeam);
            return team ?? AutoPlace(StrongestTeam());
        }

        // The 5 strongest cards, with the strongest Tank and Support among them when there are any:
        // without them most line-ups lose badly at equal strength.
        public List<string> StrongestTeam()
        {
            var ranked = Cards.OrderByDescending(c => HeroScale(c.Id)).ToList();
            var team = new[] { "Tank", "Support" }.Select(role => ranked.FirstOrDefault(c => HeroOf(c.Id).Role == role)).Where(c => c != null).ToList();
            team.AddRange(ranked.Except(team).Take(5 - team.Count));
            return team.Select(c => c.Id).ToList();
        }

        // Places these cards with tanks and warriors in front (Formation.AutoPlace), copies of one hero
        // taking their places in the order given.
        public List<TeamSlot> AutoPlace(IList<string> cards)
        {
            var copies = cards.Where(Wallet.Owns).GroupBy(c => HeroOf(c).Id).ToDictionary(g => g.Key, g => new Queue<string>(g));
            var team = Formation.AutoPlace(data, cards.Where(Wallet.Owns).Select(c => HeroOf(c).Id).ToList());
            foreach (var t in team) t.Id = copies[t.Id].Dequeue();
            return team;
        }

        List<TeamSlot> Valid(SavedTeam saved)
        {
            if (saved == null || saved.Heroes.Count == 0) return null;
            var team = saved.Heroes.Select(h => new TeamSlot(h.Id, h.Row, h.Slot)).ToList();
            if (team.Any(h => !Wallet.Owns(h.Id) || data.Hero(Wallet.Hero(h.Id).Hero) == null)) return null; // card not owned, or its hero renamed or removed
            if (team.Select(h => h.Slot).Distinct().Count() != team.Count || team.Select(h => h.Id).Distinct().Count() != team.Count) return null;
            string formation = Battle.FormationOf(team.Select(h => h.Slot));
            if (!team.All(h => Battle.SlotsOf(formation).Contains(h.Slot))) return null; // saved before formations changed
            return team;
        }

        // Writes the save to the device. A failed write (a full disk, say) is reported, never thrown at the player.
        public void Write()
        {
            try { store.Save(Save); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { warn("Couldn't save progress: " + e.Message); }
        }

        // Testing only: forget everything and start a new game.
        public void Reset()
        {
            store.Delete();
            Save = new SaveData();
            StartingHeroes();
        }
    }

    // What a fight gave, for the results screen: everything that went into the inventory, each hero's
    // battle XP, and the chests that opened.
    public class BattleRewards
    {
        public bool FirstWin;
        public Dictionary<string, long> Items = new Dictionary<string, long>();
        public List<HeroXp> Heroes = new List<HeroXp>();
        public List<string> Chests = new List<string>();

        // Adds a whole reward's items (its XP, if any, as XP potions). Campaign rewards give no Devotion.
        public void Add(Loot loot)
        {
            void Put(string item, double n) { if (n > 0) Items[item] = (Items.TryGetValue(item, out var had) ? had : 0) + (long)n; }
            Put(Item.Gold, loot.Gold); Put(Item.XpPotions, loot.Xp); Put(Item.Godshards, loot.Godshards); Put(Item.SummonTickets, loot.SummonTickets);
            Put(Item.GearMats, loot.GearMats); Put(Item.SkillTomes, loot.SkillTomes); Put(Item.HeroShards, loot.HeroShards);
            foreach (var v in loot.Vessels) Put(Item.Vessel(v.Key), v.Value);
        }
    }

    // A hero card's battle XP from one fight, and its level before and after. A card already at its
    // level cap gets no share (Capped).
    public class HeroXp
    {
        public string Card;
        public long Xp;
        public bool Capped;
        public int LevelBefore, LevelAfter;
    }
}
