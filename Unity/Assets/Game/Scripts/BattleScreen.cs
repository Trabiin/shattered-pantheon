// Milestone 1: plays a fight from the shared battle engine with placeholder cards.
// The engine decides everything; this script only shows its event stream.
// Builds its own UI in code, so it works from an empty scene with just this component.
namespace ShatteredPantheon.Game
{
    // Usings sit inside the namespace so `Battle` means the engine class, not the ShatteredPantheon.Battle namespace.
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    public class BattleScreen : MonoBehaviour
    {
        // Placeholder until the team picker exists: the "Balanced" reference team.
        static readonly string[] DefaultTeam = { "hilde", "solenne", "thessaly", "maren", "pip" };
        static readonly float[] Speeds = { 1, 2, 3 };

        GameData data;
        Battle battle;
        Coroutine playing;
        int stageIndex, speedIndex, seed;
        RectTransform root, overlay;
        Text stageLabel, speedLabel, logLine, resultTitle, resultBody;
        readonly Dictionary<string, UnitCard> cards = new Dictionary<string, UnitCard>();

        void Start()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            data = LoadData();
            seed = Random.Range(1, int.MaxValue);
            BuildUi();
            StartFight();
        }

        static GameData LoadData()
        {
            string T(string name) => Resources.Load<TextAsset>("BattleData/" + name).text;
            return GameData.FromJson(T("rules"), T("heroes"), T("enemies"), T("stages"));
        }

        float SecondsPerAction => (float)(data.Rules.SecondsPerActionAt1x / Speeds[speedIndex]);

        void StartFight()
        {
            if (playing != null) StopCoroutine(playing);
            foreach (var c in cards.Values) Destroy(c.Root.gameObject);
            cards.Clear();
            overlay.gameObject.SetActive(false);

            var stage = data.Stages[stageIndex];
            seed++;
            battle = new Battle(data, Formation.AutoPlace(data, DefaultTeam), stage.Id, seed);
            stageLabel.text = $"{stage.Name}\n<size=26>{Capitalise(stage.Difficulty)}</size>";
            logLine.text = "";
            foreach (var u in battle.Units) cards[u.Key] = new UnitCard(u, root, CardPosition(u));
            playing = StartCoroutine(Play());
        }

        // Enemies on top (back row highest), heroes below (front row nearest the middle).
        Vector2 CardPosition(Unit u)
        {
            var row = battle.Units.Where(x => x.Side == u.Side && x.Row == u.Row).ToList();
            int i = row.IndexOf(u);
            float x = (i - (row.Count - 1) / 2f) * 340f;
            float y = u.Side == "E" ? (u.Row == "back" ? 560 : 250) : (u.Row == "front" ? -170 : -480);
            return new Vector2(x, y);
        }

        IEnumerator Play()
        {
            yield return new WaitForSeconds(0.6f);
            while (!battle.Over)
            {
                int from = battle.Events.Count;
                battle.Step();
                for (int i = from; i < battle.Events.Count; i++) Show(battle.Events[i]);
                foreach (var u in battle.Units) cards[u.Key].Refresh(u);
                yield return new WaitForSeconds(SecondsPerAction);
            }
            ShowResult();
        }

        void Show(BattleEvent e)
        {
            UnitCard actor = e.Actor != null ? cards[e.Actor] : null;
            UnitCard target = e.Target != null ? cards[e.Target] : null;
            switch (e.Kind)
            {
                case EventKind.Turn:
                    actor.Pulse(this, SecondsPerAction);
                    if (e.Detail == "stunned") { Popup(actor, "Stunned", Palette.Status, 30); logLine.text = $"{actor.Name} is stunned"; }
                    else if (e.Detail.StartsWith("ult:")) { Popup(actor, e.Detail.Substring(4), Palette.Ult, 34); logLine.text = $"{actor.Name} unleashes <b>{e.Detail.Substring(4)}</b>"; }
                    else if (e.Detail.StartsWith("skill:")) logLine.text = $"{actor.Name} uses <b>{e.Detail.Substring(6)}</b>";
                    else logLine.text = $"{actor.Name} attacks";
                    break;
                case EventKind.Damage:
                    Popup(target, e.Crit ? $"-{e.Amount}!" : $"-{e.Amount}", Palette.Damage, e.Crit ? 46 : 36);
                    target.Flash(this);
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
                    Popup(target, Capitalise(e.Detail), Palette.Status, 28);
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

        void ShowResult()
        {
            resultTitle.text = battle.Result == "win" ? "Victory" : battle.Result == "lose" ? "Defeat" : "Time's up";
            resultTitle.color = battle.Result == "win" ? Palette.Heal : Palette.Damage;
            var lines = battle.Units.Where(u => u.Side == "A").OrderByDescending(u => u.Stats.Dmg).Select(u =>
                $"{u.Def.Name}  <color=#ff8a80>{u.Stats.Dmg:0} dmg</color>  <color=#9ae6a0>{u.Stats.Heal:0} heal/shield</color>{(u.Alive ? "" : "  (fell)")}");
            int seconds = Mathf.RoundToInt((float)(battle.Actions * data.Rules.SecondsPerActionAt1x));
            resultBody.text = $"Fight length at 1x: {seconds / 60}:{seconds % 60:00}\n\n" + string.Join("\n", lines);
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
        }

        void Popup(UnitCard card, string text, Color color, int size)
        {
            var t = Ui.Label(root, text, size, color, TextAnchor.MiddleCenter, card.Root.anchoredPosition + new Vector2(Random.Range(-40f, 40f), 40), new Vector2(360, 60));
            t.fontStyle = FontStyle.Bold;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            StartCoroutine(FloatAway(t, Mathf.Max(0.7f, SecondsPerAction * 1.2f)));
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
            Destroy(t.gameObject);
        }

        void BuildUi()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = Ui.Panel(canvasGo.transform, "Background", Palette.Background);
            Ui.Stretch(bg.rectTransform);
            root = Ui.Area(canvasGo.transform, "Battlefield", Vector2.zero, new Vector2(1080, 1920));

            // Divider between the two sides.
            Ui.Panel(root, "Divider", new Color(1, 1, 1, 0.08f)).rectTransform.Configure(new Vector2(0, 40), new Vector2(1000, 4));

            stageLabel = Ui.Label(root, "", 40, Color.white, TextAnchor.MiddleLeft, new Vector2(-180, 860), new Vector2(660, 120));
            speedLabel = Ui.MakeButton(root, "1x", new Vector2(250, 860), new Vector2(130, 90), CycleSpeed);
            Ui.MakeButton(root, "Stage", new Vector2(420, 860), new Vector2(170, 90), NextStage);

            logLine = Ui.Label(root, "", 32, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, new Vector2(0, -760), new Vector2(1000, 110));
            Ui.MakeButton(root, "Restart", new Vector2(0, -870), new Vector2(260, 90), StartFight);

            overlay = Ui.Panel(canvasGo.transform, "Result", new Color(0, 0, 0, 0.82f)).rectTransform;
            Ui.Stretch(overlay);
            resultTitle = Ui.Label(overlay, "", 90, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 420), new Vector2(1000, 140));
            resultTitle.fontStyle = FontStyle.Bold;
            resultBody = Ui.Label(overlay, "", 34, Color.white, TextAnchor.UpperCenter, new Vector2(0, 30), new Vector2(1000, 560));
            Ui.MakeButton(overlay, "Fight again", new Vector2(-200, -380), new Vector2(340, 110), StartFight);
            Ui.MakeButton(overlay, "Next stage", new Vector2(200, -380), new Vector2(340, 110), NextStage);
        }

        void CycleSpeed()
        {
            speedIndex = (speedIndex + 1) % Speeds.Length;
            speedLabel.text = Speeds[speedIndex] + "x";
        }

        void NextStage()
        {
            stageIndex = (stageIndex + 1) % data.Stages.Count;
            StartFight();
        }

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    // A placeholder hero or enemy: faction-coloured card with health, shield and energy bars.
    public class UnitCard
    {
        public readonly RectTransform Root;
        public readonly string Name;
        readonly Image face, hpFill, shieldFill, energyFill;
        readonly Text hpText, statusText;
        readonly CanvasGroup group;
        Coroutine pulse;

        public UnitCard(Unit u, RectTransform parent, Vector2 pos)
        {
            Name = u.Def.Name;
            var frame = Ui.Panel(parent, u.Key, u.Boss ? Palette.Ult : new Color(0, 0, 0, 0.6f));
            Root = frame.rectTransform;
            Root.Configure(pos, u.Boss ? new Vector2(320, 270) : new Vector2(300, 250));
            group = frame.gameObject.AddComponent<CanvasGroup>();

            face = Ui.Panel(Root, "Face", Palette.Faction(u.Faction));
            Ui.Stretch(face.rectTransform, 4);

            var name = Ui.Label(Root, u.Def.Name, 30, Color.white, TextAnchor.UpperCenter, new Vector2(0, 75), new Vector2(280, 80));
            name.fontStyle = FontStyle.Bold;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 18; name.resizeTextMaxSize = 30;
            Ui.Label(Root, $"{u.Def.Role} · {u.Faction} · {u.Type}", 20, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, new Vector2(0, 22), new Vector2(290, 30));
            statusText = Ui.Label(Root, "", 20, Palette.Status, TextAnchor.MiddleCenter, new Vector2(0, -12), new Vector2(290, 30));

            hpFill = Bar(Root, new Vector2(0, -55), 30, Palette.Heal, out hpText);
            shieldFill = Ui.Panel(hpFill.transform.parent, "Shield", new Color(Palette.Shield.r, Palette.Shield.g, Palette.Shield.b, 0.7f));
            Ui.Fill(shieldFill.rectTransform, 0, 0.25f);
            hpText.transform.SetAsLastSibling();
            energyFill = Bar(Root, new Vector2(0, -95), 14, Palette.Ult, out _);
            Refresh(u);
        }

        static Image Bar(RectTransform parent, Vector2 pos, float height, Color color, out Text label)
        {
            var back = Ui.Panel(parent, "Bar", new Color(0, 0, 0, 0.55f));
            back.rectTransform.Configure(pos, new Vector2(270, height));
            var fill = Ui.Panel(back.rectTransform, "Fill", color);
            Ui.Fill(fill.rectTransform, 1, 1);
            label = Ui.Label(back.rectTransform, "", 20, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(270, height));
            return fill;
        }

        public void Refresh(Unit u)
        {
            float hp = (float)(u.Hp / u.MaxHp);
            Ui.Fill(hpFill.rectTransform, hp, 1);
            hpFill.color = hp > 0.5f ? Palette.Heal : hp > 0.25f ? Palette.Burn : Palette.Damage;
            Ui.Fill(shieldFill.rectTransform, Mathf.Min(1, (float)(u.Shield / u.MaxHp)), 0.3f);
            Ui.Fill(energyFill.rectTransform, (float)(u.Energy / 100), 1);
            hpText.text = $"{u.Hp:0} / {u.MaxHp:0}";
            statusText.text = string.Join("  ", u.St.Select(s => s.Type == "channel" ? "RITUAL" : s.Type.ToUpperInvariant()).Distinct());
            group.alpha = u.Alive ? 1 : 0.25f;
        }

        public void Pulse(MonoBehaviour host, float duration)
        {
            if (pulse != null) host.StopCoroutine(pulse);
            pulse = host.StartCoroutine(PulseRoutine(Mathf.Clamp(duration * 0.6f, 0.15f, 0.5f)));
        }

        IEnumerator PulseRoutine(float duration)
        {
            for (float a = 0; a < duration; a += Time.deltaTime)
            {
                Root.localScale = Vector3.one * (1 + 0.1f * Mathf.Sin(a / duration * Mathf.PI));
                yield return null;
            }
            Root.localScale = Vector3.one;
        }

        public void Flash(MonoBehaviour host) => host.StartCoroutine(FlashRoutine());

        IEnumerator FlashRoutine()
        {
            Color baseColor = face.color;
            face.color = Color.Lerp(baseColor, Color.white, 0.6f);
            yield return new WaitForSeconds(0.08f);
            face.color = baseColor;
        }
    }

    static class Palette
    {
        public static readonly Color Background = new Color(0.08f, 0.07f, 0.1f);
        public static readonly Color Damage = new Color(1f, 0.42f, 0.38f);
        public static readonly Color Heal = new Color(0.45f, 0.85f, 0.5f);
        public static readonly Color Shield = new Color(0.55f, 0.85f, 1f);
        public static readonly Color Burn = new Color(1f, 0.65f, 0.25f);
        public static readonly Color Status = new Color(1f, 0.9f, 0.45f);
        public static readonly Color Ult = new Color(1f, 0.82f, 0.3f);
        public static readonly Color Ritual = new Color(0.8f, 0.6f, 1f);

        public static Color Faction(string f)
        {
            switch (f)
            {
                case "Sun": return new Color(0.62f, 0.48f, 0.15f);
                case "Night": return new Color(0.27f, 0.22f, 0.48f);
                case "Wild": return new Color(0.22f, 0.42f, 0.2f);
                case "Sea": return new Color(0.13f, 0.38f, 0.48f);
                case "Forge": return new Color(0.55f, 0.25f, 0.15f);
                default: return new Color(0.3f, 0.3f, 0.32f);
            }
        }
    }

    // Small helpers for building uGUI from code.
    static class Ui
    {
        static Font font;
        static Font DefaultFont => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Area(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.Configure(pos, size);
            return rt;
        }

        // Centre-anchored position and size.
        public static void Configure(this RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        // Fills the parent from the left to `amount` of its width, and from the bottom to `height` of its height.
        public static void Fill(RectTransform rt, float amount, float height)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(amount), height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align, Vector2 pos, Vector2 boxSize)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).Configure(pos, boxSize);
            var t = go.GetComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Text MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var img = Panel(parent, label + " Button", new Color(1, 1, 1, 0.14f));
            img.raycastTarget = true;
            img.rectTransform.Configure(pos, size);
            img.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            return Label(img.rectTransform, label, 34, Color.white, TextAnchor.MiddleCenter, Vector2.zero, size);
        }
    }
}
