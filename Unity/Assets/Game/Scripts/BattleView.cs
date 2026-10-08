// The fight: plays a battle from the shared engine with placeholder cards, then opens the results.
// The engine decides everything; this screen only shows its event stream.
namespace ShatteredPantheon.Game
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;
    using UnityEngine.UI;

    public class BattleView : View
    {
        readonly StageDef stage;
        readonly List<TeamSlot> team;
        readonly Dictionary<string, UnitCard> cards = new Dictionary<string, UnitCard>();
        public Battle Battle { get; private set; }
        public int Seed { get; private set; }
        Text speedLabel, logLine;

        public BattleView(GameApp app, StageDef stage, List<TeamSlot> team) : base(app)
        {
            this.stage = stage;
            this.team = team;
        }

        float SecondsPerAction => (float)(App.Data.Rules.SecondsPerActionAt1x / GameApp.Speeds[App.SpeedIndex]);

        protected override void Build()
        {
            Seed = Random.Range(1, int.MaxValue);
            Battle = new Battle(App.Data, team, stage.Id, Seed);

            Ui.Label(Root, $"{stage.Name}\n<size=26>{Ui.Capitalise(stage.Difficulty)}</size>", 40, Color.white, TextAnchor.MiddleLeft, new Vector2(-180, 860), new Vector2(660, 120)).Bold();
            speedLabel = Ui.MakeButton(Root, GameApp.Speeds[App.SpeedIndex] + "x", new Vector2(250, 860), new Vector2(130, 90), CycleSpeed, "Speed Button");
            Ui.MakeButton(Root, "Retreat", new Vector2(420, 860), new Vector2(190, 90), () => App.ShowTeam(stage));
            Ui.Panel(Root, "Divider", new Color(1, 1, 1, 0.08f), new Vector2(0, 40), new Vector2(1000, 4));
            logLine = Ui.Label(Root, "", 32, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, new Vector2(0, -780), new Vector2(1000, 110));

            foreach (var u in Battle.Units) cards[u.Key] = new UnitCard(u, Root, CardPosition(u));
            Host.StartCoroutine(Play());
        }

        // Enemies on top (back row highest), heroes below (front row nearest the middle).
        Vector2 CardPosition(Unit u)
        {
            var row = Battle.Units.Where(x => x.Side == u.Side && x.Row == u.Row).ToList();
            int i = row.IndexOf(u);
            float x = (i - (row.Count - 1) / 2f) * 340f;
            float y = u.Side == "E" ? (u.Row == "back" ? 560 : 250) : (u.Row == "front" ? -170 : -480);
            return new Vector2(x, y);
        }

        IEnumerator Play()
        {
            yield return new WaitForSeconds(0.6f);
            while (!Battle.Over && !Closed)
            {
                int from = Battle.Events.Count;
                Battle.Step();
                for (int i = from; i < Battle.Events.Count; i++) Show(Battle.Events[i]);
                foreach (var u in Battle.Units) cards[u.Key].Refresh(u);
                yield return new WaitForSeconds(SecondsPerAction);
            }
            if (Closed) yield break;
            logLine.text = Battle.Result == "win" ? "Victory!" : Battle.Result == "lose" ? "Defeat..." : "Time's up";
            yield return new WaitForSeconds(1.2f);
            if (Closed) yield break;
            if (Battle.Result == "win") App.Progress.MarkCleared(stage.Id);
            App.ShowResults(stage, team, Battle);
        }

        void Show(BattleEvent e)
        {
            UnitCard actor = e.Actor != null ? cards[e.Actor] : null;
            UnitCard target = e.Target != null ? cards[e.Target] : null;
            switch (e.Kind)
            {
                case EventKind.Turn:
                    actor.Pulse(SecondsPerAction);
                    if (e.Detail == "stunned") { Popup(actor, "Stunned", Palette.Status, 30); logLine.text = $"{actor.Name} is stunned"; }
                    else if (e.Detail.StartsWith("ult:")) { Popup(actor, e.Detail.Substring(4), Palette.Ult, 34); logLine.text = $"{actor.Name} unleashes <b>{e.Detail.Substring(4)}</b>"; }
                    else if (e.Detail.StartsWith("skill:")) logLine.text = $"{actor.Name} uses <b>{e.Detail.Substring(6)}</b>";
                    else logLine.text = $"{actor.Name} attacks";
                    break;
                case EventKind.Damage:
                    Popup(target, e.Crit ? $"-{e.Amount}!" : $"-{e.Amount}", Palette.Damage, e.Crit ? 46 : 36);
                    target.Flash();
                    break;
                case EventKind.BurnTick:
                    Popup(target, $"-{e.Amount} burn", Palette.Burn, 30);
                    break;
                case EventKind.Heal:
                    if (e.Amount > 0) Popup(target, $"+{e.Amount}", Palette.Heal, 36);
                    break;
                case EventKind.Shield:
                    Popup(target, $"+{e.Amount} shield", Palette.Shield, 30);
                    break;
                case EventKind.StatusAdded:
                    Popup(target, Ui.Capitalise(e.Detail), Palette.Status, 28);
                    break;
                case EventKind.Interrupt:
                    Popup(target, "Interrupted!", Palette.Ult, 36);
                    logLine.text = $"{actor.Name} interrupts the ritual!";
                    break;
                case EventKind.RitualStart:
                    Popup(target, "Ritual begins...", Palette.Ritual, 32);
                    logLine.text = $"{target.Name} begins a healing ritual. Stun or silence to stop it!";
                    break;
                case EventKind.RitualComplete:
                    Popup(target, "Ritual complete", Palette.Ritual, 32);
                    break;
            }
        }

        void Popup(UnitCard card, string text, Color color, int size)
        {
            var t = Ui.Label(Root, text, size, color, TextAnchor.MiddleCenter, card.Root.anchoredPosition + new Vector2(Random.Range(-40f, 40f), 40), new Vector2(360, 60)).Bold();
            t.gameObject.name = "Popup";
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            Host.StartCoroutine(FloatAway(t, Mathf.Max(0.7f, SecondsPerAction * 1.2f)));
        }

        static IEnumerator FloatAway(Text t, float duration)
        {
            var rt = t.rectTransform;
            Vector2 start = rt.anchoredPosition;
            for (float a = 0; a < duration; a += Time.deltaTime)
            {
                float k = a / duration;
                rt.anchoredPosition = start + new Vector2(0, 90 * k);
                var c = t.color; c.a = 1 - k * k; t.color = c;
                yield return null;
            }
            Object.Destroy(t.gameObject);
        }

        void CycleSpeed()
        {
            App.SpeedIndex = (App.SpeedIndex + 1) % GameApp.Speeds.Length;
            speedLabel.text = GameApp.Speeds[App.SpeedIndex] + "x";
        }
    }
}
