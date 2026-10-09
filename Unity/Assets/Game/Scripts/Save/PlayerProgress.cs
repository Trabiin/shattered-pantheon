// What the player has done, kept in the save file on the device (SaveStore): stars earned on each
// battle, the furthest battle cleared on each difficulty, and the team last used on each battle.
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
        static readonly string[] DefaultTeam = { "hilde", "solenne", "thessaly", "maren", "pip" };

        readonly GameData data;
        readonly SaveStore store;
        readonly Action<string> warn;

        public SaveData Save { get; private set; }

        public PlayerProgress(GameData data, SaveStore store, Action<string> warn)
        {
            this.data = data;
            this.store = store;
            this.warn = warn;
            Save = store.Load(out string problem);
            if (problem != null) warn(problem);
        }

        public IReadOnlyList<int> Stars(string battleId) => Save.Stars.TryGetValue(battleId, out var s) ? s : (IReadOnlyList<int>)Array.Empty<int>();
        public bool IsCleared(string battleId) => Stars(battleId).Contains(1);
        public string Furthest(string difficulty) => Save.Furthest.TryGetValue(difficulty, out var id) ? id : null;

        // Called when a fight ends, whatever the result, and saved.
        public void RecordBattle(StageDef battle, string result)
        {
            if (result == "win")
            {
                // TODO(E7-F5-S3-T2): a win is 1 star until challenge checks award stars 2 to 5.
                var stars = Save.Stars.TryGetValue(battle.Id, out var s) ? s : Save.Stars[battle.Id] = new List<int>();
                if (!stars.Contains(1)) { stars.Add(1); stars.Sort(); }
                string furthest = Furthest(battle.Difficulty);
                var before = furthest == null ? null : data.Stages.Find(b => b.Id == furthest);
                if (before == null || Order(battle).CompareTo(Order(before)) > 0) Save.Furthest[battle.Difficulty] = battle.Id;
            }
            Write();
        }

        static (int, int, int) Order(StageDef b) => (b.RealmNumber, b.StageNumber, b.BattleNumber);

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

        // The team last used on this battle, else the last team used anywhere, else a sensible default.
        public List<TeamSlot> LoadTeam(string battleId)
        {
            var team = Valid(Save.Teams.TryGetValue(battleId, out var t) ? t : null) ?? Valid(Save.LastTeam);
            return team ?? Formation.AutoPlace(data, DefaultTeam);
        }

        List<TeamSlot> Valid(SavedTeam saved)
        {
            if (saved == null || saved.Heroes.Count == 0) return null;
            var team = saved.Heroes.Select(h => new TeamSlot(h.Id, h.Row, h.Slot)).ToList();
            if (team.Any(h => data.Hero(h.Id) == null)) return null; // hero renamed or removed
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
        }
    }
}
