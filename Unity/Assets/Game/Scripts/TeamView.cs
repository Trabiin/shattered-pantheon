// Team and formation: pick up to 5 heroes and place them in the front row (2) or back row (3),
// with the stage's enemies shown and each hero's faction/type match-up against them.
// Tap a slot to select it, then tap a hero to put them there. Tap a selected filled slot to empty it.
namespace ShatteredPantheon.Game
{
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;

    public class TeamView : View
    {
        static readonly string[] Slots = { "front0", "front1", "back0", "back1", "back2" };

        readonly StageDef stage;
        readonly Dictionary<string, string> placed = new Dictionary<string, string>(); // slot -> hero id
        string selected;
        RectTransform content;

        public TeamView(GameApp app, StageDef stage) : base(app)
        {
            this.stage = stage;
            foreach (var t in app.Progress.LoadTeam(stage.Id))
                if (Slots.Contains(t.Slot) && !placed.ContainsKey(t.Slot)) placed[t.Slot] = t.Id;
            selected = FirstEmpty() ?? "front0";
        }

        protected override void Build()
        {
            Header(stage.Name, "Choose your team and formation", App.ShowStages);
            Refresh();
        }

        public List<TeamSlot> Team() => Slots.Where(placed.ContainsKey)
            .Select(s => new TeamSlot(placed[s], s.StartsWith("front") ? "front" : "back", s)).ToList();

        string FirstEmpty() => Slots.FirstOrDefault(s => !placed.ContainsKey(s));

        void Refresh()
        {
            if (content != null) Object.Destroy(content.gameObject);
            content = Ui.Area(Root, "Content", Vector2.zero, new Vector2(1080, 1920));

            // Enemies.
            Ui.Label(content, "Enemies", 28, Palette.Muted, TextAnchor.MiddleLeft, new Vector2(0, 770), new Vector2(1000, 40));
            var enemies = stage.Enemies.Select(e => App.Data.Enemies.First(x => x.Id == e.Id)).ToList();
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                var chip = Ui.Panel(content, "Enemy", Palette.Faction(e.Faction), new Vector2((i - (enemies.Count - 1) / 2f) * 195, 680), new Vector2(185, 110)).rectTransform;
                Ui.Label(chip, e.Name, 22, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 18), new Vector2(175, 60)).Bold().FitText(14);
                Ui.Label(chip, $"{e.Faction} · {e.Type}", 18, Palette.Muted, TextAnchor.MiddleCenter, new Vector2(0, -32), new Vector2(175, 30));
            }
            var notes = StageSelectView.StageNotes(App.Data, stage);
            if (notes.Count > 0) Ui.Label(content, string.Join("  ", notes), 24, Palette.Status, TextAnchor.MiddleCenter, new Vector2(0, 580), new Vector2(1000, 60));

            // Formation.
            Ui.Label(content, "Front", 28, Palette.Muted, TextAnchor.MiddleLeft, new Vector2(0, 520), new Vector2(1000, 40));
            Ui.Label(content, "Back", 28, Palette.Muted, TextAnchor.MiddleLeft, new Vector2(0, 280), new Vector2(1000, 40));
            for (int i = 0; i < 2; i++) SlotCard("front" + i, new Vector2((i - 0.5f) * 230, 410));
            for (int i = 0; i < 3; i++) SlotCard("back" + i, new Vector2((i - 1) * 230, 170));

            // Roster.
            Ui.Label(content, "Heroes", 28, Palette.Muted, TextAnchor.MiddleLeft, new Vector2(0, 30), new Vector2(1000, 40));
            var heroes = App.Data.Heroes;
            for (int i = 0; i < heroes.Count; i++)
                RosterCard(heroes[i], new Vector2((i % 5 - 2) * 205, -90 - (i / 5) * 215));

            // Actions.
            int count = placed.Count;
            Ui.Label(content, $"{count}/5 heroes", 30, count > 0 ? Color.white : Palette.Damage, TextAnchor.MiddleCenter, new Vector2(0, -560), new Vector2(600, 50));
            Ui.MakeButton(content, "Auto", new Vector2(-380, -700), new Vector2(220, 110), AutoPlace);
            Ui.MakeButton(content, "Clear", new Vector2(-140, -700), new Vector2(220, 110), () => { placed.Clear(); selected = "front0"; Refresh(); });
            var fight = Ui.MakeButton(content, "Fight!", new Vector2(270, -700), new Vector2(400, 130), Fight);
            fight.fontSize = 44;
            fight.Bold();
        }

        void SlotCard(string slot, Vector2 pos)
        {
            if (slot == selected) Ui.Panel(content, "Selected", Palette.Selected, pos, new Vector2(214, 224));
            string id = placed.TryGetValue(slot, out var h) ? h : null;
            var hero = id != null ? App.Data.Heroes.First(x => x.Id == id) : null;
            var card = Ui.MakeTapArea(content, "Slot: " + slot, hero != null ? Palette.Faction(hero.Faction) : Palette.PanelFill, pos, new Vector2(200, 210), () => TapSlot(slot)).rectTransform;
            if (hero == null)
            {
                Ui.Label(card, slot == selected ? "Tap a hero" : "Empty", 24, Palette.Muted, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(190, 60));
                return;
            }
            Ui.Label(card, hero.Name, 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 55), new Vector2(185, 80)).Bold().FitText(16);
            Ui.Label(card, hero.Role, 22, Palette.Muted, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(185, 30));
            Ui.Label(card, MatchupText(hero), 20, Color.white, TextAnchor.MiddleCenter, new Vector2(0, -60), new Vector2(190, 60));
        }

        void RosterCard(UnitDef hero, Vector2 pos)
        {
            bool inTeam = placed.ContainsValue(hero.Id);
            var color = Palette.Faction(hero.Faction);
            if (inTeam) color.a = 0.35f;
            var card = Ui.MakeTapArea(content, "Hero: " + hero.Id, color, pos, new Vector2(195, 205), () => TapHero(hero.Id)).rectTransform;
            Ui.Label(card, hero.Name, 24, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 60), new Vector2(180, 70)).Bold().FitText(15);
            Ui.Label(card, $"{hero.Role}\n<size=18>{hero.Faction} · {hero.Type}</size>", 22, Palette.Muted, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(185, 60));
            Ui.Label(card, inTeam ? "In team" : MatchupText(hero), 20, inTeam ? Palette.Status : Color.white, TextAnchor.MiddleCenter, new Vector2(0, -65), new Vector2(190, 60));
        }

        // How this hero's faction and type match up against the stage's enemies, on average.
        string MatchupText(UnitDef hero)
        {
            var R = App.Data.Rules;
            var enemies = stage.Enemies.Select(e => App.Data.Enemies.First(x => x.Id == e.Id)).ToList();
            double deals = enemies.Average(e => Battle.Matchup(R, hero.Faction, hero.Type, e.Faction, e.Type));
            double takes = enemies.Average(e => Battle.Matchup(R, e.Faction, e.Type, hero.Faction, hero.Type));
            return $"Deals {Percent(deals, true)}\nTakes {Percent(takes, false)}";
        }

        static string Percent(double multiplier, bool higherIsGood)
        {
            int p = Mathf.RoundToInt((float)((multiplier - 1) * 100));
            if (p == 0) return "even";
            bool good = (p > 0) == higherIsGood;
            return $"<color={(good ? "#73d980" : "#ff6b61")}>{(p > 0 ? "+" : "")}{p}%</color>";
        }

        void TapSlot(string slot)
        {
            if (slot == selected && placed.ContainsKey(slot)) placed.Remove(slot);
            selected = slot;
            Refresh();
        }

        void TapHero(string id)
        {
            var from = placed.FirstOrDefault(p => p.Value == id).Key;
            if (from == selected) { Refresh(); return; }
            if (from != null)
            {
                // Moving a hero already in the team: swap with whoever is in the selected slot.
                if (placed.TryGetValue(selected, out var other)) placed[from] = other; else placed.Remove(from);
            }
            placed[selected] = id;
            selected = FirstEmpty() ?? selected;
            Refresh();
        }

        // Re-places the chosen heroes (or the default team if none) with tanks and melee in front.
        void AutoPlace()
        {
            var ids = Slots.Where(placed.ContainsKey).Select(s => placed[s]).ToList();
            var team = ids.Count > 0 ? Formation.AutoPlace(App.Data, ids) : App.Progress.LoadTeam(stage.Id);
            placed.Clear();
            int front = 0, back = 0;
            foreach (var t in team)
            {
                string slot = t.Row == "front" && front < 2 ? "front" + front++ : back < 3 ? "back" + back++ : "front" + front++;
                placed[slot] = t.Id;
            }
            selected = FirstEmpty() ?? "front0";
            Refresh();
        }

        void Fight()
        {
            var team = Team();
            if (team.Count == 0) return;
            App.Progress.SaveTeam(stage.Id, team);
            App.ShowBattle(stage, team);
        }
    }
}
