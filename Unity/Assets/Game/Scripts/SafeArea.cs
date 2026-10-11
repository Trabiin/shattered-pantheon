// The safe-area container every screen sits in. Phones keep parts of the screen for a notch or camera
// hole, rounded corners and the home bar; Screen.safeArea is the rest. Screens are laid out on a
// 1080x1920 portrait page (Ui's positions), and this container keeps the whole page inside the safe
// area: centred, and shrunk only when the safe area is smaller than the page. The canvas is never
// smaller than 1080x1920 units (the scaler's Expand mode), so a taller phone adds room around the page
// and never cuts it off. The background stays on the full screen behind it.
namespace ShatteredPantheon.Game
{
    using UnityEngine;

    public class SafeArea : MonoBehaviour
    {
        public static readonly Vector2 PageSize = new Vector2(1080, 1920);

        // Where screens build their UI.
        public RectTransform Page { get; private set; }
        RectTransform area;
        Rect applied;
        int width, height;

        public static SafeArea Create(RectTransform canvas)
        {
            var area = Ui.Area(canvas, "Safe area", Vector2.zero, Vector2.zero);
            var safe = area.gameObject.AddComponent<SafeArea>();
            safe.area = area;
            safe.Page = Ui.Area(area, "Page", Vector2.zero, PageSize);
            safe.Apply();
            return safe;
        }

        // The safe area can change while the game runs: a resized Game view or the Device Simulator in
        // the editor, or a phone that folds or rotates.
        void Update()
        {
            if (Screen.width != width || Screen.height != height || Screen.safeArea != applied) Apply();
        }

        void Apply()
        {
            width = Screen.width;
            height = Screen.height;
            applied = Screen.safeArea;
            if (width <= 0 || height <= 0) return;
            area.anchorMin = new Vector2(applied.xMin / width, applied.yMin / height);
            area.anchorMax = new Vector2(applied.xMax / width, applied.yMax / height);
            area.offsetMin = area.offsetMax = Vector2.zero;
            // Canvas units per screen pixel, as the canvas scaler works them out in Expand mode.
            float units = 1 / Mathf.Min(width / PageSize.x, height / PageSize.y);
            float fit = Mathf.Min(1, applied.width * units / PageSize.x, applied.height * units / PageSize.y);
            Page.localScale = new Vector3(fit, fit, 1);
        }
    }
}
