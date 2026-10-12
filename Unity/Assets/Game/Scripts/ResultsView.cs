// Results: who won, how long it took, and what each hero did (damage dealt, healing and
// shields given, damage taken, and when they fell), so the player can see why they won or lost.
// After a win: the battle XP each hero got, everything else the win gave and any chest it opened, and
// a Next battle button to go straight on.
namespace ShatteredPantheon.Game
{
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;

    public class ResultsView : View
    {
        readonly StageDef stage;
        readonly List<TeamSlot> team;
        readonly Battle battle;
        readonly BattleRewards rewards;

        public ResultsView(GameApp app, StageDef stage, List<TeamSlot> team, Battle battle, BattleRewards rewards) : base(app)
        {
            this.stage = stage;
            this.team = team;
            this.battle = battle;
            this.rewards = rewards;
        }

        public string Title => battle.Result == "win" ? "Victory" : battle.Result == "lose" ? "Defeat" : "Time's up";

        protected override void Build()
        {
            Ui.Label(Root, Title, 96, battle.Result == "win" ? Palette.Heal : Palette.Damage, TextAnchor.MiddleCenter, new Vector2(0, 760), new Vector2(1000, 140)).Bold();
            int seconds = Mathf.RoundToInt((float)(battle.Actions * App.Data.Rules.SecondsPerActionAt1x));
            int fallen = battle.Units.Count(u => u.Side == "A" && !u.Alive);
            Ui.Label(Root, $"{stage.Name}  ·  {seconds / 60}:{seconds % 60:00} at 1x  ·  {fallen} hero{(fallen == 1 ? "" : "es")} fell", 28, Palette.Muted, TextAnchor.MiddleCenter, new Vector2(0, 660), new Vector2(1000, 50));
            if (battle.Result != "win") Ui.Label(Root, Hint(), 28, Palette.Status, TextAnchor.MiddleCenter, new Vector2(0, 590), new Vector2(1000, 70));

            // One row per hero, with bars scaled to the best value in the team.
            var heroes = battle.Units.Where(u => u.Side == "A").ToList();
            double maxDmg = Mathf.Max(1, (float)heroes.Max(u => u.Stats.Dmg));
            double maxHeal = Mathf.Max(1, (float)heroes.Max(u => u.Stats.Heal));
            double maxTaken = Mathf.Max(1, (float)heroes.Max(u => u.Stats.Taken));
            Ui.Label(Root, "Damage dealt", 24, Palette.Damage, TextAnchor.MiddleCenter, new Vector2(-80, 500), new Vector2(240, 40));
            Ui.Label(Root, "Heal + shield", 24, Palette.Heal, TextAnchor.MiddleCenter, new Vector2(170, 500), new Vector2(240, 40));
            Ui.Label(Root, "Damage taken", 24, Palette.Muted, TextAnchor.MiddleCenter, new Vector2(390, 500), new Vector2(200, 40));
            for (int i = 0; i < heroes.Count; i++)
            {
                var u = heroes[i];
                float y = 410 - i * 130;
                var row = Ui.Panel(Root, "Row: " + u.Id, Palette.PanelFill, new Vector2(0, y), new Vector2(1000, 115)).rectTransform;
                Ui.Panel(row, "Faction", Palette.Faction(u.Faction), new Vector2(-494, 0), new Vector2(12, 115));
                string fell = u.Alive ? "" : $"\n<size=20><color=#ff6b61>Fell at action {u.Stats.DiedAt}</color></size>";
                Ui.Label(row, u.Def.Name + fell + XpLine(u), 26, Color.white, TextAnchor.MiddleLeft, new Vector2(-340, 0), new Vector2(280, 100)).Bold().FitText(16);
                StatBar(row, new Vector2(-80, 0), u.Stats.Dmg / maxDmg, u.Stats.Dmg, Palette.Damage);
                StatBar(row, new Vector2(170, 0), u.Stats.Heal / maxHeal, u.Stats.Heal, Palette.Heal);
                // The last bar stops short of the row's right edge (a margin like the name's on the left).
                StatBar(row, new Vector2(390, 0), u.Stats.Taken / maxTaken, u.Stats.Taken, new Color(1, 1, 1, 0.45f), 170);
            }

            if (battle.Result == "win") Rewards(new Vector2(0, -350));

            Ui.MakeButton(Root, "Retry", new Vector2(-340, -620), new Vector2(300, 120), () => App.ShowBattle(stage, team, battle.HeroFormation));
            Ui.MakeButton(Root, "Change team", new Vector2(0, -620), new Vector2(340, 120), () => App.ShowTeam(stage));
            Ui.MakeButton(Root, "Stages", new Vector2(340, -620), new Vector2(300, 120), App.ShowStages);
            // The battle the win opened (or the next one, on a replay), while it's one the campaign screen shows.
            var next = battle.Result == "win" ? App.Progress.NextBattle(stage) : null;
            if (next != null && CampaignView.Battles(App.Data).Contains(next))
                Ui.MakeButton(Root, "Next battle", new Vector2(0, -790), new Vector2(640, 120), () => App.ShowTeam(next));
        }

        // The battle XP the hero's card got, and the level it reached.
        string XpLine(Unit u)
        {
            string card = team.FirstOrDefault(t => t.Slot == u.Slot)?.Id;
            var xp = rewards.Heroes.FirstOrDefault(h => h.Card == card);
            if (xp == null) return "";
            string level = xp.LevelAfter > xp.LevelBefore ? $"  ·  Level {xp.LevelAfter}!" : xp.Capped ? "  ·  Level cap" : "";
            return $"\n<size=20><color=#ffd24a>+{xp.Xp} XP{level}</color></size>";
        }

        // What the win gave besides battle XP, and the chests it opened.
        void Rewards(Vector2 pos)
        {
            var panel = Ui.Panel(Root, "Rewards", Palette.PanelFill, pos, new Vector2(1000, 230)).rectTransform;
            Ui.Label(panel, rewards.FirstWin ? "First clear" : "Replay", 30, Palette.Ult, TextAnchor.MiddleCenter, new Vector2(0, 80), new Vector2(960, 50)).Bold();
            string items = rewards.Items.Count == 0 ? "Battle XP only" : string.Join("   ·   ", Item.All.Where(rewards.Items.ContainsKey).Select(i => Item.Describe(i, rewards.Items[i])));
            string chests = string.Concat(rewards.Chests.Select(c => $"\n<color=#ffd24a>{c} opened!</color>"));
            Ui.Label(panel, items + chests, 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0, -25), new Vector2(960, 160)).FitText(18).gameObject.name = "Rewards: items";
        }

        static void StatBar(RectTransform row, Vector2 pos, double share, double value, Color color, float width = 230)
        {
            var back = Ui.Panel(row, "Bar", new Color(0, 0, 0, 0.4f), pos, new Vector2(width, 44));
            Ui.Fill(Ui.Panel(back.rectTransform, "Fill", color).rectTransform, (float)share, 1);
            Ui.Label(back.rectTransform, value.ToString("0"), 24, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(width, 44)).Bold();
        }

        // A nudge towards the pre-fight choices, read from what happened in the fight.
        string Hint()
        {
            if (battle.Result == "timeout") return "Too slow: bring more damage, or stop the enemy healing.";
            if (battle.Log.Any(l => l.Contains("completes a ritual"))) return "The ritual healed the boss. Godstruck or Hush interrupts it.";
            var firstDown = battle.Units.Where(u => u.Side == "A" && !u.Alive).OrderBy(u => u.Stats.DiedAt).FirstOrDefault();
            if (firstDown != null && firstDown.Row == "back") return $"{firstDown.Def.Name} fell first from the back row. Put a sturdier hero in front of them, or a tank who can intercept.";
            return "Try a different team or formation: placement, type match-ups and doctrines all matter.";
        }
    }
}
