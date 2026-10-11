// What the player has done, kept in the save file on the device (SaveStore): stars earned on each
// battle, the furthest battle cleared on each difficulty, the team last used on each battle, and
// (through the Wallet) the heroes they own and their items.
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
        readonly CampaignCurve curve;
        readonly SaveStore store;
        readonly Action<string> warn;
        readonly CampaignOrder campaign;

        public SaveData Save { get; private set; }
        public Wallet Wallet { get; }

        public PlayerProgress(GameData data, CampaignCurve curve, SaveStore store, Action<string> warn)
        {
            this.data = data;
            this.curve = curve;
            this.store = store;
            this.warn = warn;
            campaign = new CampaignOrder(data.Stages);
            Wallet = new Wallet(data, curve.Growth, () => Save, Write);
            Save = store.Load(out string problem);
            if (problem != null) warn(problem);
            if (Save.Heroes.Count == 0) StartingHeroes();
        }

        // A player always owns at least one hero, so a save without any is a new game, or one saved
        // before heroes were kept: it gets the new player's heroes.
        // TODO(E9-F2-S1-T5): summons don't exist yet, so a new game owns every test hero. With summons it
        // owns only the 3 starters (progression.json, roster.starters).
        void StartingHeroes() => Wallet.StartingHeroes(data.Heroes.Select(h => h.Id));

        // Heroes the player owns, in the order the hero data lists them.
        public List<UnitDef> OwnedHeroes => data.Heroes.Where(h => Wallet.Owns(h.Id)).ToList();

        // What a hero's listed health and attack are multiplied by in a fight: their rarity, level, star
        // rank and skill level (doc 07 section 1). Gear arrives in E8-F9.
        public double HeroScale(string id)
        {
            var h = Wallet.Hero(id) ?? throw new ArgumentException("Hero not owned: " + id);
            return curve.Growth.StatScale(data.Hero(id).Rarity, h.Level, h.Stars, 0, h.Skill);
        }

        public IReadOnlyList<int> Stars(string battleId) => Save.Stars.TryGetValue(battleId, out var s) ? s : (IReadOnlyList<int>)Array.Empty<int>();
        public bool IsCleared(string battleId) => Stars(battleId).Contains(1);
        public bool IsOpen(StageDef battle) => campaign.IsOpen(battle, IsCleared);
        public string Furthest(string difficulty) => Save.Furthest.TryGetValue(difficulty, out var id) ? id : null;

        // Called when a fight ends, whatever the result, with the heroes who fought, and saved. The first
        // win on a battle shares its battle XP among them.
        public void RecordBattle(StageDef battle, string result, IEnumerable<string> fighters)
        {
            if (result == "win")
            {
                // TODO(E7-F6-S1-T6): replays give no XP until they give the farm reward for Devotion.
                if (!IsCleared(battle.Id)) Wallet.ShareBattleXp(fighters, curve.FirstClearXp(battle));
                // TODO(E7-F5-S3-T2): a win is 1 star until challenge checks award stars 2 to 5.
                var stars = Save.Stars.TryGetValue(battle.Id, out var s) ? s : Save.Stars[battle.Id] = new List<int>();
                if (!stars.Contains(1)) { stars.Add(1); stars.Sort(); }
                string furthest = Furthest(battle.Difficulty);
                var before = furthest == null ? null : data.Stages.Find(b => b.Id == furthest);
                if (before == null || campaign.IndexOf(battle) > campaign.IndexOf(before)) Save.Furthest[battle.Difficulty] = battle.Id;
            }
            Write();
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
            return team ?? Formation.AutoPlace(data, StrongestTeam());
        }

        // The 5 strongest owned heroes, with the strongest Tank and Support among them when there are any:
        // without them most line-ups lose badly at equal strength.
        public List<string> StrongestTeam()
        {
            var ranked = OwnedHeroes.OrderByDescending(h => HeroScale(h.Id)).ToList();
            var team = new[] { "Tank", "Support" }.Select(role => ranked.FirstOrDefault(h => h.Role == role)).Where(h => h != null).ToList();
            team.AddRange(ranked.Except(team).Take(5 - team.Count));
            return team.Select(h => h.Id).ToList();
        }

        List<TeamSlot> Valid(SavedTeam saved)
        {
            if (saved == null || saved.Heroes.Count == 0) return null;
            var team = saved.Heroes.Select(h => new TeamSlot(h.Id, h.Row, h.Slot)).ToList();
            if (team.Any(h => data.Hero(h.Id) == null || !Wallet.Owns(h.Id))) return null; // hero renamed, removed or not owned
            if (team.Select(h => h.Slot).Distinct().Count() != team.Count) return null;
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
}
