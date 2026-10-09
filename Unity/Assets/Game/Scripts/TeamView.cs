// Team and formation: pick up to 5 heroes and place them in a 2-3 or 3-2 formation (staggered rows),
// with the stage's enemies shown and each hero's type match-up against them.
// Tap a slot to select it, then tap a hero to put them there. Tap a selected filled slot to empty it.
namespace ShatteredPantheon.Game
{
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;

    public class TeamView : View
    {
        readonly StageDef stage;
        string formation = "2-3";
        string[] Slots => Battle.SlotsOf(formation).ToArray();
        readonly Dictionary<string, string> placed = new Dictionary<string, string>(); // slot -> hero id
        string selected;
        RectTransform content;

        public TeamView(GameApp app, StageDef stage) : base(app)
        {
            this.stage = stage;
            var saved = app.Progress.LoadTeam(stage.Id);
            formation = app.Progress.LoadFormation(stage.Id, saved);
            foreach (var t in saved)
                if (Slots.Contains(t.Slot) && !placed.ContainsKey(t.Slot)) placed[t.Slot] = t.Id;
            selected = FirstEmpty() ?? Slots[0];
        }

        protected override void Build()
        {
            string boss = stage.Boss == "realm" ? "Realm boss  ·  " : stage.Boss == "stage" ? "Stage boss  ·  " : "";
            Header(stage.Name, boss + "Choose your team and formation", App.ShowStages);
            Refresh();
        }

        public string Formation => formation;

        public List<TeamSlot> Team() => Slots.Where(placed.ContainsKey).Select(s => new TeamSlot(placed[s], s)).ToList();

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
                chip.gameObject.name = "Enemy: " + e.Id;
            }
            var notes = CampaignView.StageNotes(App.Data, stage);
            if (notes.Count > 0) Ui.Label(content, string.Join("  ", notes), 24, Palette.Status, TextAnchor.MiddleCenter, new Vector2(0, 580), new Vector2(1000, 60));

            // Formation: rows are staggered, so a row of two stands in the gaps of the row of three.
            Ui.Label(content, "Front", 28, Palette.Muted, TextAnchor.MiddleLeft, new Vector2(0, 520), new Vector2(1000, 40));
            Ui.Label(content, "Back", 28, Palette.Muted, TextAnchor.MiddleLeft, new Vector2(0, 280), new Vector2(1000, 40));
            Ui.MakeButton(content, formation, new Vector2(400, 520), new Vector2(170, 70), SwitchFormation, "Formation Button");
            foreach (var slot in Slots)
                SlotCard(slot, new Vector2((float)Battle.SlotX(formation, slot) * 230, slot.StartsWith("front") ? 410 : 170));

            // Roster.
            Ui.Label(content, "Heroes", 28, Palette.Muted, TextAnchor.MiddleLeft, new Vector2(0, 30), new Vector2(1000, 40));
            var heroes = App.Data.Heroes;
            for (int i = 0; i < heroes.Count; i++)
                RosterCard(heroes[i], new Vector2((i % 5 - 2) * 205, -60 - (i / 5) * 160));

            // Actions.
            int count = placed.Count;
            Ui.Label(content, $"{count}/5 heroes", 30, count > 0 ? Color.white : Palette.Damage, TextAnchor.MiddleCenter, new Vector2(0, -680), new Vector2(600, 50));
            Ui.MakeButton(content, "Auto", new Vector2(-380, -800), new Vector2(220, 110), AutoPlace);
            Ui.MakeButton(content, "Clear", new Vector2(-140, -800), new Vector2(220, 110), () => { placed.Clear(); selected = Slots[0]; Refresh(); });
            var fight = Ui.MakeButton(content, "Fight!", new Vector2(270, -800), new Vector2(400, 130), Fight);
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
            var card = Ui.MakeTapArea(content, "Hero: " + hero.Id, color, pos, new Vector2(195, 150), () => TapHero(hero.Id)).rectTransform;
            Ui.Label(card, hero.Name, 22, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 45), new Vector2(185, 52)).Bold().FitText(14);
            Ui.Label(card, $"{hero.Role} · {hero.Faction} · {hero.Type}", 15, Palette.Muted, TextAnchor.MiddleCenter, new Vector2(0, 8), new Vector2(190, 24)).FitText(11);
            Ui.Label(card, inTeam ? "In team" : MatchupText(hero), 17, inTeam ? Palette.Status : Color.white, TextAnchor.MiddleCenter, new Vector2(0, -42), new Vector2(190, 52));
        }

        // How this hero's type matches up against the stage's enemies on average (damage dealt and taken),
        // with a marker when the hero's god defeated (▲) or fell to (▼) most of the enemies' gods.
        string MatchupText(UnitDef hero)
        {
            var R = App.Data.Rules;
            var enemies = stage.Enemies.Select(e => App.Data.Enemy(e.Id)).ToList();
            double deals = enemies.Average(e => Battle.TypeMultiplier(R, hero.Type, e.Type));
            double takes = enemies.Average(e => Battle.TypeMultiplier(R, e.Type, hero.Type));
            int dom = enemies.Sum(e => Battle.Dominance(R, hero.Faction, e.Faction));
            string mark = dom > 0 ? " <color=#73d980>▲</color>" : dom < 0 ? " <color=#ff6b61>▼</color>" : "";
            return $"Deals {Percent(deals, true)}{mark}\nTakes {Percent(takes, false)}";
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

        // Switches between 2-3 and 3-2, keeping the heroes in the same order (front to back, left to right).
        void SwitchFormation()
        {
            var heroes = Slots.Where(placed.ContainsKey).Select(s => placed[s]).ToList();
            formation = formation == "2-3" ? "3-2" : "2-3";
            placed.Clear();
            for (int i = 0; i < heroes.Count; i++) placed[Slots[i]] = heroes[i];
            selected = FirstEmpty() ?? Slots[0];
            Refresh();
        }

        // Re-places the chosen heroes (or the default team if none) with tanks and warriors in front.
        void AutoPlace()
        {
            var ids = Slots.Where(placed.ContainsKey).Select(s => placed[s]).ToList();
            var team = ids.Count > 0 ? ShatteredPantheon.Battle.Formation.AutoPlace(App.Data, ids) : App.Progress.LoadTeam(stage.Id);
            formation = Battle.FormationOf(team.Select(t => t.Slot));
            placed.Clear();
            foreach (var t in team) placed[t.Slot] = t.Id;
            selected = FirstEmpty() ?? Slots[0];
            Refresh();
        }

        void Fight()
        {
            var team = Team();
            if (team.Count == 0) return;
            App.Progress.SaveTeam(stage.Id, team, formation);
            App.ShowBattle(stage, team, formation);
        }
    }
}
