// The campaign: the realm's 10 stages, each a row of its 4 battles (battle 4 is the stage boss, and
// stage 10's boss the realm boss). Tapping a battle opens the team screen for it. Cleared battles
// come from the save; a testing-only button resets it.
namespace ShatteredPantheon.Game
{
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;
    using UnityEngine.UI;

    public class CampaignView : View
    {
        // The vertical slice is realm 1 on Normal (doc 11); the realm map (E7-F3) brings the others.
        public const int Realm = 1;
        public const string Difficulty = "normal";

        public CampaignView(GameApp app) : base(app) { }

        // The battles shown, in play order.
        public static List<StageDef> Battles(GameData data) => data.Stages
            .Where(s => s.RealmNumber == Realm && s.Difficulty == Difficulty)
            .OrderBy(s => s.StageNumber).ThenBy(s => s.BattleNumber).ToList();

        protected override void Build()
        {
            var battles = Battles(App.Data);
            Header($"Realm {Realm}: {battles[0].Realm}", $"{Ui.Capitalise(Difficulty)}  ·  choose a battle", null);
            float height = 140, gap = 16, top = 700;
            var stages = battles.GroupBy(b => b.StageNumber).ToList();
            for (int i = 0; i < stages.Count; i++)
            {
                var row = Ui.Panel(Root, "Stage " + stages[i].Key, Palette.PanelFill, new Vector2(0, top - i * (height + gap)), new Vector2(1000, height)).rectTransform;
                Ui.Panel(row, "Realm", Palette.Faction(battles[0].Realm), new Vector2(-494, 0), new Vector2(12, height));
                Ui.Label(row, $"Stage {stages[i].Key}", 34, Color.white, TextAnchor.MiddleLeft, new Vector2(-380, 0), new Vector2(200, height)).Bold();
                int n = 0;
                foreach (var battle in stages[i]) BattleTile(row, battle, new Vector2(-185 + n++ * 192, 0), new Vector2(180, height - 24));
            }
            ResetButton();
        }

        // TODO(E16-F5-S1-T1): for testing only; moves to the settings screen in testing builds and is gone from the closed test.
        void ResetButton()
        {
            bool armed = false;
            Text label = null;
            label = Ui.MakeButton(Root, "Reset progress", new Vector2(310, -880), new Vector2(380, 90), () =>
            {
                if (!armed) { armed = true; label.text = "Tap again to reset"; return; }
                App.Progress.Reset();
                App.ShowStages();
            });
            label.fontSize = 28;
        }

        void BattleTile(RectTransform row, StageDef battle, Vector2 pos, Vector2 size)
        {
            var fill = battle.Boss != null ? Palette.Faction(battle.Realm) : Palette.ButtonFill;
            var tile = Ui.MakeTapArea(row, "Battle: " + battle.Id, fill, pos, size, () => App.ShowTeam(battle)).rectTransform;
            string title = battle.Boss == "realm" ? "Realm boss" : battle.Boss == "stage" ? "Boss" : $"Battle {battle.BattleNumber}";
            Ui.Label(tile, title, 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 18), new Vector2(size.x - 10, 50)).Bold().FitText(18);
            string state = App.Progress.IsCleared(battle.Id) ? "<color=#73d980>Cleared</color>" : $"{battle.StageNumber}-{battle.BattleNumber}";
            Ui.Label(tile, state, 22, Palette.Muted, TextAnchor.MiddleCenter, new Vector2(0, -28), new Vector2(size.x - 10, 36));
        }

        // Plain-language hints about what a battle asks of a team, read from the enemies' skills.
        public static List<string> StageNotes(GameData data, StageDef stage)
        {
            var notes = new List<string>();
            var defs = stage.Enemies.Select(e => data.Enemy(e.Id)).Distinct().ToList();
            IEnumerable<ActionDef> Actions(UnitDef d) => d.Skills.Append(d.Ult).Where(k => k != null).SelectMany(k => k.Actions);
            foreach (var d in defs)
            {
                if (Actions(d).Any(a => a.Effect == "channel")) notes.Add($"{d.Name} heals with a ritual: bring Godstruck or Hush.");
                if (Actions(d).Any(a => (a.Target == "backRow" || a.Target == "back") && a.Effect == "damage")) notes.Add($"{d.Name} hits your back row.");
                if (Actions(d).Any(a => a.Target == "lowestAlly" && a.Effect == "heal")) notes.Add($"{d.Name} heals allies: focus it down.");
            }
            var factions = defs.GroupBy(d => d.Faction).Where(g => stage.Enemies.Count(e => data.Enemy(e.Id).Faction == g.Key) >= 2).Select(g => g.Key);
            foreach (var f in factions) notes.Add($"{f} doctrine active.");
            return notes.Distinct().ToList();
        }
    }
}
