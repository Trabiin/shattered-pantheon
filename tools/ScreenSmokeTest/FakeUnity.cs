// A tiny stand-in for the parts of Unity the game screens use, with real behaviour where it
// matters for catching bugs: object hierarchy, destroy (touching a destroyed object throws,
// like Unity's MissingReferenceException), coroutines on a simulated clock, Resources loading
// from the Unity project, PlayerPrefs in memory, and button clicks. It is not Unity: layout and rendering are not
// checked here. The real-API compile check runs in CI (.github/workflows/unity-compile.yml).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityEngine
{
    public class MissingReferenceException : Exception { public MissingReferenceException(string m) : base(m) { } }

    public class Object
    {
        internal static readonly List<Object> All = new List<Object>();
        internal bool destroyed;
        public string name;
        protected Object() { All.Add(this); }
        internal void Check() { if (destroyed) throw new MissingReferenceException($"The object '{name}' ({GetType().Name}) has been destroyed but you are still trying to access it."); }
        public static void Destroy(Object o) { if (o is GameObject g) g.DestroyTree(); else if (o != null) o.destroyed = true; }
        public static T FindAnyObjectByType<T>() where T : Object => All.OfType<T>().FirstOrDefault(x => !x.destroyed);
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, null) || a.destroyed ? ReferenceEquals(b, null) || b.destroyed : !ReferenceEquals(b, null) && !b.destroyed && ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public static implicit operator bool(Object o) => o != null;
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => base.GetHashCode();
    }

    public class Component : Object
    {
        internal GameObject go;
        public GameObject gameObject { get { Check(); return go; } }
        public Transform transform { get { Check(); return go.transform; } }
        public T GetComponent<T>() => gameObject.GetComponent<T>();
    }

    public class Behaviour : Component { public bool enabled = true; }

    public class Coroutine { internal IEnumerator Routine; internal MonoBehaviour Owner; internal float WakeAt; internal bool Stopped; }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { internal float Seconds; public WaitForSeconds(float s) { Seconds = s; } }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator e)
        {
            Check();
            var c = new Coroutine { Routine = e, Owner = this, WakeAt = Time.time };
            Scheduler.Add(c);
            Scheduler.Advance(c); // Unity runs a coroutine up to its first yield immediately.
            return c;
        }
        public void StopCoroutine(Coroutine c) { if (c != null) c.Stopped = true; }
    }

    public static class Scheduler
    {
        static readonly List<Coroutine> running = new List<Coroutine>();
        public static readonly List<string> Errors = new List<string>();
        internal static void Add(Coroutine c) => running.Add(c);
        internal static void Advance(Coroutine c)
        {
            try
            {
                if (!c.Routine.MoveNext()) { c.Stopped = true; return; }
                c.WakeAt = c.Routine.Current is WaitForSeconds w ? Time.time + w.Seconds : Time.time + 1e-6f;
            }
            catch (Exception e) { c.Stopped = true; Errors.Add(e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); }
        }
        // One frame: every coroutine that is due takes a step (a coroutine dies with its MonoBehaviour).
        public static void Frame(float dt)
        {
            Time.deltaTime = dt; Time.time += dt;
            foreach (var c in running.ToList())
            {
                if (c.Owner.destroyed) c.Stopped = true;
                if (!c.Stopped && Time.time >= c.WakeAt) Advance(c);
            }
            running.RemoveAll(c => c.Stopped);
        }
        public static int Running => running.Count;
    }

    public class GameObject : Object
    {
        readonly List<Component> components = new List<Component>();
        public bool activeSelf = true;
        public GameObject(string n, params Type[] types)
        {
            name = n;
            // Like Unity, UI components (which require a RectTransform) get one instead of a plain Transform.
            bool rect = types.Any(x => x == typeof(RectTransform) || x == typeof(Canvas) || typeof(UI.Graphic).IsAssignableFrom(x));
            var t = rect ? (Transform)new RectTransform() : new Transform();
            Attach(t);
            foreach (var type in types.Where(x => !typeof(Transform).IsAssignableFrom(x))) Attach((Component)Activator.CreateInstance(type));
        }
        void Attach(Component c) { c.go = this; c.name = name; components.Add(c); if (c is UI.Graphic g) g.Awake(); }
        public Transform transform { get { Check(); return (Transform)components[0]; } }
        public T AddComponent<T>() where T : Component { Check(); var c = (T)Activator.CreateInstance(typeof(T)); Attach(c); return c; }
        public T GetComponent<T>() { Check(); return components.OfType<T>().FirstOrDefault(); }
        public void SetActive(bool b) { Check(); activeSelf = b; }
        internal void DestroyTree()
        {
            foreach (var child in ((Transform)components[0]).children.ToList()) child.go.DestroyTree();
            ((Transform)components[0]).SetParent(null, false);
            destroyed = true;
            foreach (var c in components) c.destroyed = true;
        }
    }

    public class Transform : Component, IEnumerable
    {
        internal Transform parentT;
        internal readonly List<Transform> children = new List<Transform>();
        Vector3 scale = Vector3.one;
        public Transform parent { get { Check(); return parentT; } }
        public void SetParent(Transform p, bool worldPositionStays)
        {
            parentT?.children.Remove(this);
            if (p != null) { p.Check(); p.children.Add(this); }
            parentT = p;
        }
        public void SetAsLastSibling() { Check(); if (parentT != null) { parentT.children.Remove(this); parentT.children.Add(this); } }
        public Vector3 localScale { get { Check(); return scale; } set { Check(); scale = value; } }
        public IEnumerator GetEnumerator() => children.ToList().GetEnumerator();
    }

    public class RectTransform : Transform
    {
        Vector2 aMin, aMax, piv, pos, size, oMin, oMax;
        public Vector2 anchorMin { get { Check(); return aMin; } set { Check(); aMin = value; } }
        public Vector2 anchorMax { get { Check(); return aMax; } set { Check(); aMax = value; } }
        public Vector2 pivot { get { Check(); return piv; } set { Check(); piv = value; } }
        public Vector2 anchoredPosition { get { Check(); return pos; } set { Check(); pos = value; } }
        public Vector2 sizeDelta { get { Check(); return size; } set { Check(); size = value; } }
        public Vector2 offsetMin { get { Check(); return oMin; } set { Check(); oMin = value; } }
        public Vector2 offsetMax { get { Check(); return oMax; } set { Check(); oMax = value; } }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x * b, a.y * b);
        public override string ToString() => $"({x}, {y})";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x * b, a.y * b, a.z * b);
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color Lerp(Color x, Color y, float t) => new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float v, float lo, float hi) => Math.Min(hi, Math.Max(lo, v));
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static int RoundToInt(float f) => (int)Math.Round(f);
    }

    public static class Random
    {
        static readonly System.Random r = new System.Random(1);
        public static int Range(int a, int b) => r.Next(a, b);
        public static float Range(float a, float b) => a + (float)r.NextDouble() * (b - a);
    }

    public static class Time { public static float deltaTime; public static float time; }
    public static class Application { public static int targetFrameRate; }
    public enum ScreenOrientation { Portrait }
    public static class Screen { public static ScreenOrientation orientation; }
    public class TextAsset : Object { public string text; }

    public static class PlayerPrefs
    {
        static readonly Dictionary<string, object> store = new Dictionary<string, object>();
        public static int GetInt(string k, int d = 0) => store.TryGetValue(k, out var v) ? (int)v : d;
        public static void SetInt(string k, int v) => store[k] = v;
        public static string GetString(string k, string d = "") => store.TryGetValue(k, out var v) ? (string)v : d;
        public static void SetString(string k, string v) => store[k] = v;
        public static void Save() { }
        public static void DeleteAll() => store.Clear();
    }
    public class Font : Object { }

    public static class Resources
    {
        public static string Root; // Unity/Assets/Resources
        public static T Load<T>(string path) where T : Object
        {
            var file = Path.Combine(Root, path + ".json");
            if (typeof(T) != typeof(TextAsset) || !File.Exists(file)) return null;
            return (T)(Object)new TextAsset { name = path, text = File.ReadAllText(file) };
        }
        public static T GetBuiltinResource<T>(string path) where T : Object =>
            path == "LegacyRuntime.ttf" && typeof(T) == typeof(Font) ? (T)(Object)new Font { name = path } : throw new ArgumentException("No built-in resource " + path);
    }

    public enum TextAnchor { UpperLeft, UpperCenter, MiddleLeft, MiddleCenter }
    public enum FontStyle { Normal, Bold }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum RenderMode { ScreenSpaceOverlay }
    public class Canvas : Behaviour { public RenderMode renderMode; }
    public class CanvasGroup : Behaviour { float a = 1; public float alpha { get { Check(); return a; } set { Check(); a = value; } } }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public class UnityEvent
    {
        readonly List<UnityAction> listeners = new List<UnityAction>();
        public void AddListener(UnityAction a) => listeners.Add(a);
        public void Invoke() { foreach (var l in listeners.ToList()) l(); }
    }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }
    public class EventSystem : UIBehaviour { }
    public class StandaloneInputModule : UIBehaviour { }
}

namespace UnityEngine.UI
{
    public class Graphic : EventSystems.UIBehaviour
    {
        Color c = Color.white;
        internal void Awake() { }
        public Color color { get { Check(); return c; } set { Check(); c = value; } }
        public bool raycastTarget = true;
        public RectTransform rectTransform => (RectTransform)transform;
    }
    public class Image : Graphic { }
    public class Text : Graphic
    {
        string t = "";
        public Font font;
        public string text { get { Check(); return t; } set { Check(); t = value ?? ""; } }
        public int fontSize;
        public TextAnchor alignment;
        public bool supportRichText, resizeTextForBestFit;
        public int resizeTextMinSize, resizeTextMaxSize;
        public HorizontalWrapMode horizontalOverflow;
        public VerticalWrapMode verticalOverflow;
        public FontStyle fontStyle;
    }
    public class Button : EventSystems.UIBehaviour { public Events.UnityEvent onClick = new Events.UnityEvent(); }
    public class Outline : EventSystems.UIBehaviour { public Color effectColor; }
    public class CanvasScaler : EventSystems.UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize }
        public ScaleMode uiScaleMode;
        public Vector2 referenceResolution;
        public float matchWidthOrHeight;
    }
    public class GraphicRaycaster : EventSystems.UIBehaviour { }
}
