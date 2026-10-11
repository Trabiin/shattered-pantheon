// Works out where every element of a screen lands on a phone, as Unity's canvas does (anchors, pivots,
// offsets, scale, the canvas scaler's Expand mode and RectMask2D clipping), and checks what a player sees:
// nothing outside the phone's safe area, no text cut off or spilling out of its box or off its card, no text
// or tap areas on top of each other, no cards overlapping, and every tap area at least Ui.MinTap units
// across on the 1080-wide page. Text size is estimated from Arial's letter widths (Unity's built-in font), so it is
// close to Unity's, not exact.
using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredPantheon.Game;
using UnityEngine;
using UnityEngine.UI;

static class Layout
{
    // A rectangle in screen pixels.
    public struct Box
    {
        public float X0, Y0, X1, Y1;
        public Box(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
        public float W => X1 - X0;
        public float H => Y1 - Y0;
        public bool Empty => W <= 0 || H <= 0;
        public Box Clip(Box o) => new Box(Math.Max(X0, o.X0), Math.Max(Y0, o.Y0), Math.Min(X1, o.X1), Math.Min(Y1, o.Y1));
        public bool Inside(Box o, float slack) => X0 >= o.X0 - slack && Y0 >= o.Y0 - slack && X1 <= o.X1 + slack && Y1 <= o.Y1 + slack;
        public bool Overlaps(Box o, float slack) => Math.Min(X1, o.X1) - Math.Max(X0, o.X0) > slack && Math.Min(Y1, o.Y1) - Math.Max(Y0, o.Y0) > slack;
        public override string ToString() => $"({X0:0},{Y0:0})-({X1:0},{Y1:0})";
    }

    // A RectTransform's place on screen: the pixel position of its pivot, its pixels per unit, and its rectangle in its own units.
    struct Frame { public float Ox, Oy, Scale, X0, Y0, X1, Y1; }

    static Frame FrameOf(RectTransform rt)
    {
        var parent = rt.parent as RectTransform;
        if (parent == null || rt.GetComponent<Canvas>() != null)
        {
            var scaler = rt.GetComponent<CanvasScaler>();
            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand) throw new NotSupportedException("Layout follows the canvas scaler's Expand mode only");
            float s = Math.Min(Screen.width / scaler.referenceResolution.x, Screen.height / scaler.referenceResolution.y);
            float w = Screen.width / s, h = Screen.height / s;
            return new Frame { Ox = Screen.width / 2f, Oy = Screen.height / 2f, Scale = s, X0 = -w / 2, Y0 = -h / 2, X1 = w / 2, Y1 = h / 2 };
        }
        var p = FrameOf(parent);
        float pw = p.X1 - p.X0, ph = p.Y1 - p.Y0;
        var size = new Vector2((rt.anchorMax.x - rt.anchorMin.x) * pw + rt.sizeDelta.x, (rt.anchorMax.y - rt.anchorMin.y) * ph + rt.sizeDelta.y);
        float ax = p.X0 + pw * (rt.anchorMin.x + (rt.anchorMax.x - rt.anchorMin.x) * rt.pivot.x);
        float ay = p.Y0 + ph * (rt.anchorMin.y + (rt.anchorMax.y - rt.anchorMin.y) * rt.pivot.y);
        float lx = ax + rt.anchoredPosition.x, ly = ay + rt.anchoredPosition.y;
        return new Frame
        {
            Ox = p.Ox + p.Scale * lx, Oy = p.Oy + p.Scale * ly, Scale = p.Scale * rt.localScale.x,
            X0 = -rt.pivot.x * size.x, Y0 = -rt.pivot.y * size.y, X1 = (1 - rt.pivot.x) * size.x, Y1 = (1 - rt.pivot.y) * size.y,
        };
    }

    static Box ToScreen(Frame f, float x0, float y0, float x1, float y1) => new Box(f.Ox + f.Scale * x0, f.Oy + f.Scale * y0, f.Ox + f.Scale * x1, f.Oy + f.Scale * y1);

    public static Box Rect(RectTransform rt) { var f = FrameOf(rt); return ToScreen(f, f.X0, f.Y0, f.X1, f.Y1); }

    // How much of an element shows through the RectMask2Ds above it, from 0 (scrolled out of sight) to 1.
    public static float SeenShare(RectTransform rt)
    {
        var whole = Rect(rt);
        var seen = Visible(rt, whole);
        return seen.Empty ? 0 : seen.W * seen.H / (whole.W * whole.H);
    }

    public static Box SafeArea => new Box(Screen.safeArea.xMin, Screen.safeArea.yMin, Screen.safeArea.xMax, Screen.safeArea.yMax);

    // What's left of an element after the RectMask2Ds above it.
    static Box Visible(Transform t, Box b)
    {
        for (var a = t.parent; a != null; a = a.parent)
            if (a.GetComponent<RectMask2D>() != null) b = b.Clip(Rect((RectTransform)a));
        return b;
    }

    static IEnumerable<Transform> Tree(Transform t) => new[] { t }.Concat(t.Cast<Transform>().SelectMany(Tree));

    static bool Shown(Transform t)
    {
        for (var a = t; a != null; a = a.parent)
        {
            if (!a.gameObject.activeSelf) return false;
            var g = a.GetComponent<CanvasGroup>();
            if (g != null && g.alpha <= 0) return false;
        }
        return true;
    }

    static bool IsTap(Transform t) => t.GetComponent<Button>() != null;
    static bool IsAncestor(Transform a, Transform t) { for (var x = t; x != null; x = x.parent) if (x == a) return true; return false; }
    static string NameOf(Transform t) => t.GetComponent<Text>() is Text x ? $"\"{Plain(x.text).Replace("\n", " ")}\"" : t.name;

    // Checks the screen under `root` on the current phone; returns one line per problem.
    public static List<string> Check(RectTransform root, float minTap)
    {
        var problems = new List<string>();
        var safe = SafeArea;
        float unit = Rect((RectTransform)root.parent).W / ShatteredPantheon.Game.SafeArea.PageSize.x; // pixels per page unit
        var nodes = Tree(root).Where(Shown).ToList();
        var texts = new List<(Transform t, Box b)>();
        var taps = new List<(Transform t, Box b)>();
        var cards = new List<(Transform t, Box b)>();

        foreach (var t in nodes)
        {
            var g = t.GetComponent<Graphic>();
            if (g == null) continue;
            var rt = (RectTransform)t;
            var rect = Rect(rt);
            if (g is Text text)
            {
                if (string.IsNullOrWhiteSpace(Plain(text.text))) continue;
                var fit = Fit(text, rt);
                var drawn = Drawn(text, rt, fit.width, fit.height);
                if (!fit.fits) problems.Add($"{NameOf(t)} doesn't fit its {rt.sizeDelta.x:0}x{rt.sizeDelta.y:0} box (needs about {fit.width:0}x{fit.height:0})");
                for (var a = t.parent; a != null && a != root; a = a.parent)
                    if (a.GetComponent<Image>() is Image back && back.color.a > 0)
                    {
                        if (!drawn.Inside(Rect((RectTransform)a), 1)) problems.Add($"{NameOf(t)} spills off {a.name}");
                        break;
                    }
                var seen = Visible(t, drawn);
                if (seen.Empty) continue;
                if (!seen.Inside(safe, 0.5f)) problems.Add($"{NameOf(t)} is outside the safe area");
                if (t.name != "Popup") texts.Add((t, seen));
            }
            else
            {
                if (g.color.a <= 0 && !IsTap(t)) continue;
                var seen = Visible(t, rect);
                if (seen.Empty) continue;
                if (g.color.a > 0 && !seen.Inside(safe, 0.5f)) problems.Add($"{t.name} is outside the safe area");
                if (IsTap(t))
                {
                    float w = rect.W / unit, h = rect.H / unit;
                    if (w < minTap - 0.5f || h < minTap - 0.5f) problems.Add($"{t.name} is {w:0}x{h:0} units on the page, under {minTap:0}");
                    taps.Add((t, seen));
                }
                // Panels holding content (cards, rows, tiles) don't overlap the panels beside them.
                if (t.Cast<Transform>().Any()) cards.Add((t, seen));
            }
        }

        for (int i = 0; i < texts.Count; i++)
            for (int j = i + 1; j < texts.Count; j++)
                if (texts[i].b.Overlaps(texts[j].b, 1)) problems.Add($"{NameOf(texts[i].t)} overlaps {NameOf(texts[j].t)}");
        foreach (var x in texts)
            foreach (var tap in taps)
                if (!IsAncestor(tap.t, x.t) && x.b.Overlaps(tap.b, 1)) problems.Add($"{NameOf(x.t)} lies on {tap.t.name}");
        for (int i = 0; i < taps.Count; i++)
            for (int j = i + 1; j < taps.Count; j++)
                if (!IsAncestor(taps[i].t, taps[j].t) && !IsAncestor(taps[j].t, taps[i].t) && taps[i].b.Overlaps(taps[j].b, 1)) problems.Add($"{taps[i].t.name} overlaps {taps[j].t.name}");
        for (int i = 0; i < cards.Count; i++)
            for (int j = i + 1; j < cards.Count; j++)
                if (cards[i].t.parent == cards[j].t.parent && cards[i].b.Overlaps(cards[j].b, 1)) problems.Add($"{cards[i].t.name} overlaps {cards[j].t.name}");
        return problems.Distinct().ToList();
    }

    // ---- Text size, estimated as Unity's legacy Text lays it out ----

    const float LineHeight = 1.15f; // Arial's line spacing, in font sizes

    // Arial (Helvetica) letter widths in thousandths of the font size, for ' ' to '~'.
    static readonly int[] Regular = {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
        1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
        333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584 };
    static readonly int[] Bold = {
        278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
        975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
        333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,389,280,389,584 };

    static float Width(char c, bool bold)
    {
        if (c >= ' ' && c <= '~') return (bold ? Bold : Regular)[c - ' '] / 1000f;
        if (c == '·') return bold ? 0.333f : 0.278f;
        return 0.8f; // ★ ▲ ▼ and other symbols come from a fallback font
    }

    struct Glyph { public char C; public float Size; public bool Bold; }

    // The text's letters with their size and weight, from Unity's rich text tags (<size>, <b>; <color> changes neither).
    static List<Glyph> Glyphs(Text t, float scale)
    {
        var glyphs = new List<Glyph>();
        var sizes = new Stack<float>(); sizes.Push(t.fontSize * scale);
        int bold = t.fontStyle == FontStyle.Bold ? 1 : 0;
        string s = t.text;
        for (int i = 0; i < s.Length; i++)
        {
            if (t.supportRichText && s[i] == '<')
            {
                int end = s.IndexOf('>', i);
                string tag = end > 0 ? s.Substring(i + 1, end - i - 1) : "";
                if (tag.StartsWith("size=")) { sizes.Push(float.Parse(tag.Substring(5)) * scale); i = end; continue; }
                if (tag == "/size") { if (sizes.Count > 1) sizes.Pop(); i = end; continue; }
                if (tag == "b") { bold++; i = end; continue; }
                if (tag == "/b") { bold--; i = end; continue; }
                if (tag.StartsWith("color=") || tag == "/color") { i = end; continue; }
            }
            glyphs.Add(new Glyph { C = s[i], Size = sizes.Peek(), Bold = bold > 0 });
        }
        return glyphs;
    }

    static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");

    // The lines the text wraps to in `boxWidth`, each with its width and height; false if a word is wider than the box.
    static (bool fits, float width, float height) Lay(Text t, float scale, float boxWidth)
    {
        var glyphs = Glyphs(t, scale);
        bool fits = true;
        float widest = 0, height = 0;
        foreach (var hard in Split(glyphs, '\n'))
        {
            float lineW = 0, lineH = hard.Count > 0 ? hard.Max(g => g.Size) : t.fontSize * scale;
            float space = 0;
            foreach (var word in Split(hard, ' '))
            {
                float w = word.Sum(g => Width(g.C, g.Bold) * g.Size);
                if (t.horizontalOverflow == HorizontalWrapMode.Wrap && lineW > 0 && lineW + space + w > boxWidth + 0.5f)
                {
                    widest = Math.Max(widest, lineW); height += lineH * LineHeight;
                    lineW = 0; space = 0; lineH = word.Count > 0 ? word.Max(g => g.Size) : lineH;
                }
                if (w > boxWidth + 0.5f) fits = false;
                lineW += space + w;
                space = Width(' ', false) * (word.Count > 0 ? word[0].Size : t.fontSize * scale);
            }
            widest = Math.Max(widest, lineW); height += lineH * LineHeight;
        }
        return (fits, widest, height);
    }

    static IEnumerable<List<Glyph>> Split(List<Glyph> glyphs, char at)
    {
        var part = new List<Glyph>();
        foreach (var g in glyphs)
        {
            if (g.C == at) { yield return part; part = new List<Glyph>(); }
            else part.Add(g);
        }
        yield return part;
    }

    // Whether the text fits its box (at the largest size best fit allows), and the size it takes up.
    static (bool fits, float width, float height) Fit(Text t, RectTransform rt)
    {
        var f = FrameOf(rt);
        float bw = f.X1 - f.X0, bh = f.Y1 - f.Y0;
        int min = t.resizeTextForBestFit ? t.resizeTextMinSize : t.fontSize;
        (bool fits, float width, float height) laid = default;
        for (int size = t.resizeTextForBestFit ? t.resizeTextMaxSize : t.fontSize; size >= min; size--)
        {
            laid = Lay(t, size / (float)t.fontSize, bw);
            if (laid.fits && laid.height <= bh + 0.5f && laid.width <= bw + 0.5f) return laid;
        }
        return (false, laid.width, laid.height);
    }

    // Where the laid-out text is drawn, from its alignment in the box; text taller than an overflowing box spills out evenly.
    static Box Drawn(Text t, RectTransform rt, float w, float h)
    {
        var f = FrameOf(rt);
        bool left = t.alignment == TextAnchor.UpperLeft || t.alignment == TextAnchor.MiddleLeft;
        bool upper = t.alignment == TextAnchor.UpperLeft || t.alignment == TextAnchor.UpperCenter;
        if (t.verticalOverflow == VerticalWrapMode.Truncate) h = Math.Min(h, f.Y1 - f.Y0);
        float x0 = left ? f.X0 : (f.X0 + f.X1 - w) / 2;
        float y1 = upper ? f.Y1 : (f.Y0 + f.Y1 + h) / 2;
        return ToScreen(f, x0, y1 - h, x0 + w, y1);
    }
}
