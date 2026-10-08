// Small helpers for building uGUI from code, and the placeholder colour palette.
// Layout uses a 1080x1920 portrait reference with positions measured from the screen centre.
namespace ShatteredPantheon.Game
{
    using UnityEngine;
    using UnityEngine.UI;

    public static class Ui
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

        public static Image Panel(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var img = Panel(parent, name, color);
            img.rectTransform.Configure(pos, size);
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

        public static Text Bold(this Text t) { t.fontStyle = FontStyle.Bold; return t; }

        public static Text FitText(this Text t, int min)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = t.fontSize;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        // A labelled button. The GameObject is named "<label> Button" unless a name is given.
        public static Text MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick, string name = null)
        {
            var img = MakeTapArea(parent, name ?? label + " Button", Palette.ButtonFill, pos, size, onClick);
            return Label(img.rectTransform, label, 34, Color.white, TextAnchor.MiddleCenter, Vector2.zero, size);
        }

        // A coloured panel that can be tapped.
        public static Image MakeTapArea(Transform parent, string name, Color color, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var img = Panel(parent, name, color, pos, size);
            img.raycastTarget = true;
            img.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            return img;
        }

        public static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        public static string Hex(Color c) =>
            "#" + ((int)(Mathf.Clamp01(c.r) * 255)).ToString("x2") + ((int)(Mathf.Clamp01(c.g) * 255)).ToString("x2") + ((int)(Mathf.Clamp01(c.b) * 255)).ToString("x2");
    }

    public static class Palette
    {
        public static readonly Color Background = new Color(0.08f, 0.07f, 0.1f);
        public static readonly Color PanelFill = new Color(1, 1, 1, 0.06f);
        public static readonly Color ButtonFill = new Color(1, 1, 1, 0.14f);
        public static readonly Color Selected = new Color(1f, 0.82f, 0.3f, 0.55f);
        public static readonly Color Muted = new Color(1, 1, 1, 0.6f);
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

        public static Color Difficulty(string d)
        {
            switch (d)
            {
                case "easy": return Heal;
                case "normal": return Shield;
                case "hard": return Burn;
                default: return Damage;
            }
        }
    }
}
