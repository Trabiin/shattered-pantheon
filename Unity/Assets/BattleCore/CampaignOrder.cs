// The order campaign battles open in (doc 06 section 1.1): every battle of a stage in turn, the stage
// boss (battle 4) opening the next stage, the realm boss (stage 10) opening the next realm, and realm
// 10's boss opening the next difficulty (Normal, Hard, Nightmare, Godless). So all of it is one line,
// and a battle is open when the battle before it in that line is cleared.
// Nothing here is stored: what's open is worked out from which battles are cleared, so the game (from
// its save) and the progression simulator (from its own record) can share the same rules.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredPantheon.Battle
{
    public class CampaignOrder
    {
        public static readonly string[] Difficulties = { "normal", "hard", "nightmare", "godless" };

        readonly List<StageDef> battles;
        readonly Dictionary<string, int> index;

        // Takes any list of stages; only campaign battles (with a realm, on a known difficulty) are kept.
        public CampaignOrder(IEnumerable<StageDef> stages)
        {
            battles = stages
                .Where(s => s.RealmNumber > 0 && Array.IndexOf(Difficulties, s.Difficulty) >= 0)
                .OrderBy(s => Array.IndexOf(Difficulties, s.Difficulty))
                .ThenBy(s => s.RealmNumber).ThenBy(s => s.StageNumber).ThenBy(s => s.BattleNumber)
                .ToList();
            index = new Dictionary<string, int>();
            for (int i = 0; i < battles.Count; i++) index.Add(battles[i].Id, i);
        }

        // Every campaign battle in the order they open.
        public IReadOnlyList<StageDef> Battles => battles;

        // The battle's place in the whole campaign (0 is Normal, realm 1, stage 1, battle 1), or -1 if it isn't a campaign battle.
        public int IndexOf(StageDef battle) => battle != null && index.TryGetValue(battle.Id, out int i) ? i : -1;

        // The battle that has to be cleared to open this one; null for the very first battle.
        public StageDef Before(StageDef battle)
        {
            int i = IndexOf(battle);
            return i > 0 ? battles[i - 1] : null;
        }

        // Open battles can be started: the first battle, any battle already cleared (so it can be
        // replayed), and the battle after a cleared one. A battle outside the campaign is never open.
        public bool IsOpen(StageDef battle, Func<string, bool> isCleared)
        {
            if (IndexOf(battle) < 0) return false;
            var before = Before(battle);
            return before == null || isCleared(battle.Id) || isCleared(before.Id);
        }
    }
}
