// Stage select: every stage with its difficulty, enemy line-up and what the boss does, so the
// player can plan a team before choosing it.
namespace ShatteredPantheon.Game
{
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;

    public class StageSelectView : View
    {
        public StageSelectView(GameApp app) : base(app) { }

        protected override void Build()
        {
            Header("Shattered Pantheon", "Choose a stage", null);
            var stages = App.Data.Stages;
            float height = 300, gap = 30;
            float top = 700 - height / 2;
            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                var card = Ui.MakeTapArea(Root, "Stage: " + stage.Id, Palette.PanelFill, new Vector2(0, top - i * (height + gap)), new Vector2(1000, height), () => App.ShowTeam(stage)).rectTransform;
                Ui.Panel(card, "Difficulty", Palette.Difficulty(stage.Difficulty), new Vector2(-494, 0), new Vector2(12, height));
                Ui.Label(card, stage.Name, 40, Color.white, TextAnchor.UpperLeft, new Vector2(10, 100), new Vector2(940, 60)).Bold();
                string cleared = App.Progress.IsCleared(stage.Id) ? "   <color=#73d980>Cleared</color>" : "";
                Ui.Label(card, $"<color={Ui.Hex(Palette.Difficulty(stage.Difficulty))}>{Ui.Capitalise(stage.Difficulty)}</color>{cleared}", 28, Color.white, TextAnchor.UpperLeft, new Vector2(10, 50), new Vector2(940, 40));
                Ui.Label(card, EnemySummary(stage), 26, Palette.Muted, TextAnchor.UpperLeft, new Vector2(10, -20), new Vector2(940, 80));
                var notes = StageNotes(App.Data, stage);
                if (notes.Count > 0) Ui.Label(card, string.Join("  ", notes), 24, Palette.Status, TextAnchor.UpperLeft, new Vector2(10, -100), new Vector2(940, 70));
            }
        }

        string EnemySummary(StageDef stage)
        {
            var groups = stage.Enemies.GroupBy(e => e.Id).Select(g =>
            {
                var def = App.Data.Enemies.First(x => x.Id == g.Key);
                string count = g.Count() > 1 ? $" x{g.Count()}" : "";
                return $"{def.Name}{count} <size=20>({def.Faction}, {def.Type})</size>";
            });
            return "Enemies: " + string.Join(", ", groups);
        }

        // Plain-language hints about what the stage asks of a team, read from the enemies' skills.
        public static List<string> StageNotes(GameData data, StageDef stage)
        {
            var notes = new List<string>();
            var defs = stage.Enemies.Select(e => data.Enemies.First(x => x.Id == e.Id)).Distinct().ToList();
            IEnumerable<ActionDef> Actions(UnitDef d) => (d.Skill?.Actions ?? new List<ActionDef>()).Concat(d.Ult?.Actions ?? new List<ActionDef>());
            foreach (var d in defs)
            {
                if (Actions(d).Any(a => a.Effect == "channel")) notes.Add($"{d.Name} heals with a ritual: bring a stun or silence.");
                if (Actions(d).Any(a => a.Target == "backRow" && a.Effect == "damage")) notes.Add($"{d.Name} hits your back row.");
                if (Actions(d).Any(a => a.Target == "lowestAlly" && a.Effect == "heal")) notes.Add($"{d.Name} heals allies: focus it down.");
            }
            return notes.Distinct().ToList();
        }
    }
}
