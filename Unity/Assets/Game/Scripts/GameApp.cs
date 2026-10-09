// The game's entry point: loads the data, owns the canvas, and switches between screens
// (campaign -> team and formation -> battle -> results). Every screen builds its own UI in code.
namespace ShatteredPantheon.Game
{
    using System.Collections.Generic;
    using ShatteredPantheon.Battle;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    public class GameApp : MonoBehaviour
    {
        public static readonly float[] Speeds = { 1, 2, 3 };

        public GameData Data { get; private set; }
        public PlayerProgress Progress { get; private set; }
        public int SpeedIndex { get; set; }

        RectTransform canvas;
        View current;

        void Start()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            Data = LoadData();
            Progress = new PlayerProgress(Data, new SaveStore(Application.persistentDataPath), Debug.LogWarning);
            BuildCanvas();
            ShowStages();
        }

        // Phones close paused apps without warning, so pausing (or quitting) writes the save.
        void OnApplicationPause(bool paused) { if (paused) Progress?.Write(); }
        void OnApplicationQuit() => Progress?.Write();

        static GameData LoadData()
        {
            string T(string name) => Resources.Load<TextAsset>("BattleData/" + name).text;
            // The battles are the campaign's; stages.json holds the balance simulator's test stages.
            return GameData.FromJson(T("rules"), T("heroes"), T("enemies"), T("campaign"));
        }

        void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvas = (RectTransform)go.transform;
            Ui.Stretch(Ui.Panel(canvas, "Background", Palette.Background).rectTransform);
        }

        public void ShowStages() => Show(new CampaignView(this));
        public void ShowTeam(StageDef stage) => Show(new TeamView(this, stage));
        public void ShowBattle(StageDef stage, List<TeamSlot> team, string formation) => Show(new BattleView(this, stage, team, formation));
        public void ShowResults(StageDef stage, List<TeamSlot> team, Battle battle) => Show(new ResultsView(this, stage, team, battle));

        void Show(View next)
        {
            current?.Close();
            current = next;
            next.Open(Ui.Area(canvas, next.GetType().Name, Vector2.zero, new Vector2(1080, 1920)));
        }
    }

    // One screen. Its UI lives under Root and is destroyed when the next screen opens.
    public abstract class View
    {
        protected readonly GameApp App;
        protected RectTransform Root;
        // Runs this screen's animations; they stop when the screen closes.
        protected CoroutineHost Host;

        protected View(GameApp app) { App = app; }

        public void Open(RectTransform root)
        {
            Root = root;
            Host = root.gameObject.AddComponent<CoroutineHost>();
            Build();
        }

        protected abstract void Build();

        // True once the next screen has opened. Unity destroys objects at the end of the frame, so a
        // coroutine can still resume once after Close; it checks this before touching the game.
        protected bool Closed { get; private set; }

        public virtual void Close()
        {
            Closed = true;
            Object.Destroy(Root.gameObject);
        }

        protected Text Header(string title, string subtitle, UnityEngine.Events.UnityAction back)
        {
            if (back != null) Ui.MakeButton(Root, "Back", new Vector2(-430, 860), new Vector2(160, 90), back);
            var t = Ui.Label(Root, $"{title}\n<size=26>{subtitle}</size>", 40, Color.white, TextAnchor.MiddleLeft, new Vector2(back != null ? 90 : -10, 860), new Vector2(back != null ? 820 : 1000, 120)).Bold();
            return t;
        }
    }
}
