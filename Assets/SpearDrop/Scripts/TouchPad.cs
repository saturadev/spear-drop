using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameTouch
{
    /// On-screen touch controls for phones. In WebGL on iOS Safari and Android Chrome the Input System
    /// only gets Touchscreen events, so mouse and keyboard reads stay idle there. This reads every touch
    /// from the Input System and hands each one to the first control it lands on: a round button, a
    /// floating stick (a zone of the screen) or, failing those, a free drag (look / orbit).
    /// It draws nothing and claims nothing until the device is a phone or tablet or a touch is seen,
    /// so desktop play with mouse and keyboard is unchanged. It draws its own overlay canvas without a
    /// GraphicRaycaster, so it never blocks UI that goes through the EventSystem.
    [DefaultExecutionOrder(-500)]
    public class TouchPad : MonoBehaviour
    {
        public static TouchPad I;
        public static bool TouchSeen;
        static float lastTouch = -100f;

        /// Touch controls are shown and read.
        public static bool Active => TouchSeen || (Application.isMobilePlatform && Touchscreen.current != null);
        /// A finger was down a moment ago: lets callers ignore mouse events a browser may emulate from a tap.
        public static bool RecentTouch => Time.unscaledTime - lastTouch < 0.6f;

        public class Button
        {
            public string label;
            public bool visible = true;
            /// Held down this frame, went down this frame, came up this frame.
            public bool held, pressed, released;
            internal Vector2 anchor, offset;
            internal float radius;
            internal Color color;
            internal int touch = -1;
            internal RectTransform rt, area;
            internal Image fill, ring;
            internal TextMeshProUGUI text;
            public void SetLabel(string s) { label = s; if (text) text.text = s; }
        }

        public readonly List<Button> buttons = new List<Button>();
        /// When true the overlay is hidden and nothing is read (a bot is playing, a menu is up).
        public bool hidden;

        // stick
        bool hasStick;
        Rect stickZone;
        Vector2 stickHomeAnchor, stickHomeOffset;
        float stickRadius;
        int stickTouch = -1;
        Vector2 stickOrigin;
        public Vector2 Stick { get; private set; }
        public bool StickHeld => stickTouch != -1;
        RectTransform stickBase, stickKnob;

        // free drag
        public Rect dragZone = new Rect(0, 0, 1, 1);
        int dragTouch = -1;
        public Vector2 DragDelta { get; private set; }
        public bool Dragging => dragTouch != -1;
        /// A touch went down this frame that no button took.
        public bool Tapped { get; private set; }

        Canvas canvas;
        RectTransform safe;
        Rect lastSafe;
        TMP_FontAsset font;
        Material fontMat;
        readonly Dictionary<int, Vector2> lastPos = new Dictionary<int, Vector2>();
        readonly HashSet<int> down = new HashSet<int>();
        readonly List<int> stale = new List<int>();
        static Sprite disc, ringSprite;

        public static TouchPad Create(TMP_FontAsset font, Material fontMat, int sortingOrder = 40)
        {
            var go = new GameObject("TouchPad", typeof(RectTransform));
            var tp = go.AddComponent<TouchPad>();
            tp.font = font; tp.fontMat = fontMat;
            tp.canvas = go.AddComponent<Canvas>();
            tp.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tp.canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            tp.safe = new GameObject("Safe", typeof(RectTransform)).GetComponent<RectTransform>();
            tp.safe.SetParent(go.transform, false);
            tp.FitSafe();
            MakeSprites();
            I = tp;
            return tp;
        }

        void OnDestroy() { if (I == this) I = null; }

        /// A round button. anchor is a corner or edge of the safe area (0..1), offset is in canvas units
        /// (1080 x 1920 reference) from that anchor to the button's centre, radius in canvas units.
        public Button AddButton(string label, Vector2 anchor, Vector2 offset, float radius, Color color, float fontSize = 44f)
        {
            var b = new Button { label = label, anchor = anchor, offset = offset, radius = radius, color = color };
            b.rt = new GameObject("Touch " + label, typeof(RectTransform)).GetComponent<RectTransform>();
            b.rt.SetParent(safe, false);
            b.rt.anchorMin = b.rt.anchorMax = anchor;
            b.rt.pivot = new Vector2(0.5f, 0.5f);
            b.rt.anchoredPosition = offset;
            b.rt.sizeDelta = Vector2.one * radius * 2f;
            b.fill = Img(b.rt, disc, new Color(0.05f, 0.05f, 0.08f, 0.32f), radius * 2f);
            b.ring = Img(b.rt, ringSprite, new Color(color.r, color.g, color.b, 0.9f), radius * 2f);
            var tgo = new GameObject("Label", typeof(RectTransform));
            tgo.transform.SetParent(b.rt, false);
            var t = tgo.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            if (fontMat) t.fontSharedMaterial = fontMat;
            t.text = label;
            t.fontSize = Mathf.Max(28f, fontSize);
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            b.text = t;
            buttons.Add(b);
            return b;
        }

        /// Makes an existing screen-space overlay UI element (a prompt box, a card) tappable. Draws nothing;
        /// it only takes touches while the element is active.
        public Button AddArea(RectTransform area)
        {
            var b = new Button { label = area.name, area = area };
            buttons.Add(b);
            return b;
        }

        /// A floating stick: a touch that starts inside zone (normalized screen rect, origin bottom left)
        /// becomes the stick's centre; dragging away up to radius (canvas units) gives -1..1 on each axis.
        public void AddStick(Rect zone, Vector2 homeAnchor, Vector2 homeOffset, float radius)
        {
            hasStick = true;
            stickZone = zone; stickHomeAnchor = homeAnchor; stickHomeOffset = homeOffset; stickRadius = radius;
            stickBase = new GameObject("Touch Stick", typeof(RectTransform)).GetComponent<RectTransform>();
            stickBase.SetParent(safe, false);
            stickBase.anchorMin = stickBase.anchorMax = homeAnchor;
            stickBase.anchoredPosition = homeOffset;
            stickBase.sizeDelta = Vector2.one * radius * 2f;
            Img(stickBase, disc, new Color(0.05f, 0.05f, 0.08f, 0.25f), radius * 2f);
            Img(stickBase, ringSprite, new Color(1f, 1f, 1f, 0.7f), radius * 2f);
            stickKnob = Img(stickBase, disc, new Color(1f, 1f, 1f, 0.75f), radius * 0.9f).rectTransform;
        }

        Image Img(RectTransform parent, Sprite s, Color c, float size)
        {
            var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = s; img.color = c; img.raycastTarget = false;
            img.rectTransform.sizeDelta = Vector2.one * size;
            return img;
        }

        void FitSafe()
        {
            var sa = Screen.safeArea;
            if (sa == lastSafe && safe.anchorMax != Vector2.zero) return;
            lastSafe = sa;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            safe.anchorMin = new Vector2(sa.xMin / w, sa.yMin / h);
            safe.anchorMax = new Vector2(sa.xMax / w, sa.yMax / h);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        float Scale => canvas ? canvas.scaleFactor : 1f;

        bool OnButton(Button b, Vector2 screen)
        {
            if (b.area != null) return b.area.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(b.area, screen, null);
            Vector2 c = RectTransformUtility.WorldToScreenPoint(null, b.rt.position);
            float r = b.radius * Scale * 1.15f;
            return (screen - c).sqrMagnitude <= r * r;
        }

        static bool InZone(Rect z, Vector2 screen)
        {
            float u = screen.x / Mathf.Max(1, Screen.width), v = screen.y / Mathf.Max(1, Screen.height);
            return z.Contains(new Vector2(u, v));
        }

        void Update()
        {
            foreach (var b in buttons) { b.pressed = b.released = false; }
            DragDelta = Vector2.zero;
            Tapped = false;
            Vector2 stickValue = Vector2.zero;

            bool show = Active && !hidden;
            if (safe.gameObject.activeSelf != show) safe.gameObject.SetActive(show);
            FitSafe();

            var ts = Touchscreen.current;
            down.Clear();
            if (ts != null && show)
            {
                foreach (var t in ts.touches)
                {
                    bool isDown = t.press.isPressed, began = t.press.wasPressedThisFrame;
                    if (!isDown && !began) continue;
                    int id = t.touchId.ReadValue();
                    Vector2 pos = t.position.ReadValue();
                    TouchSeen = true; lastTouch = Time.unscaledTime;
                    if (isDown) down.Add(id);
                    bool fresh = !lastPos.ContainsKey(id);
                    Vector2 delta = fresh ? Vector2.zero : pos - lastPos[id];
                    lastPos[id] = pos;
                    if (fresh) Claim(id, pos);

                    if (id == stickTouch)
                    {
                        Vector2 d = (pos - stickOrigin) / Mathf.Max(1f, stickRadius * Scale);
                        stickValue = Vector2.ClampMagnitude(d, 1f);
                    }
                    else if (id == dragTouch) DragDelta += delta;
                }
            }
            else if (ts != null)
            {
                // keep the "seen" flag honest while hidden
                foreach (var t in ts.touches) if (t.press.wasPressedThisFrame) { TouchSeen = true; lastTouch = Time.unscaledTime; }
            }

            // touches that lifted (or were never held for a whole frame) release what they owned
            stale.Clear();
            foreach (var id in lastPos.Keys) if (!down.Contains(id)) stale.Add(id);
            foreach (var id in stale)
            {
                lastPos.Remove(id);
                foreach (var b in buttons) if (b.touch == id) { b.touch = -1; b.released = true; }
                if (stickTouch == id) stickTouch = -1;
                if (dragTouch == id) dragTouch = -1;
            }
            foreach (var b in buttons)
            {
                b.held = b.touch != -1;
                if (!b.visible && b.touch != -1) { b.touch = -1; b.held = false; b.released = true; }
            }
            Stick = stickTouch != -1 ? stickValue : Vector2.zero;
            Draw();
        }

        void Claim(int id, Vector2 pos)
        {
            foreach (var b in buttons)
            {
                if (!b.visible || b.touch != -1 || !OnButton(b, pos)) continue;
                b.touch = id; b.pressed = true; b.held = true;
                return;
            }
            Tapped = true;
            if (hasStick && stickTouch == -1 && InZone(stickZone, pos))
            {
                stickTouch = id; stickOrigin = pos;
                return;
            }
            if (dragTouch == -1 && InZone(dragZone, pos)) dragTouch = id;
        }

        void Draw()
        {
            if (!safe.gameObject.activeSelf) return;
            foreach (var b in buttons)
            {
                if (b.rt == null) continue;
                if (b.rt.gameObject.activeSelf != b.visible) b.rt.gameObject.SetActive(b.visible);
                bool on = b.touch != -1;
                b.fill.color = on ? new Color(b.color.r, b.color.g, b.color.b, 0.55f) : new Color(0.05f, 0.05f, 0.08f, 0.32f);
                b.rt.localScale = Vector3.one * (on ? 0.94f : 1f);
            }
            if (stickBase)
            {
                if (stickTouch != -1)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)stickBase.parent, stickOrigin, null, out var local);
                    stickBase.anchorMin = stickBase.anchorMax = new Vector2(0.5f, 0.5f);
                    stickBase.anchoredPosition = local;
                    stickKnob.anchoredPosition = Stick * stickRadius;
                }
                else
                {
                    stickBase.anchorMin = stickBase.anchorMax = stickHomeAnchor;
                    stickBase.anchoredPosition = stickHomeOffset;
                    stickKnob.anchoredPosition = Vector2.zero;
                }
            }
        }

        static void MakeSprites()
        {
            if (disc != null) return;
            disc = MakeSprite(128, r => Mathf.Clamp01((0.5f - r) * 128f + 0.5f));
            ringSprite = MakeSprite(128, r => Mathf.Clamp01((0.035f - Mathf.Abs(r - 0.46f)) * 128f + 0.5f));
        }

        static Sprite MakeSprite(int n, System.Func<float, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TouchPad" };
            var px = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = (i + 0.5f) / n - 0.5f, y = (j + 0.5f) / n - 0.5f;
                    px[j * n + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(Mathf.Sqrt(x * x + y * y))) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
