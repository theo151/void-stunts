using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoidStunts
{
    /// <summary>Code-built uGUI in the house style: paper-white glass panels, ink text, volt accents, pill buttons, dot-matrix titles.</summary>
    public static class UIKit
    {
        public static Font Mono, MonoBold, Wide;
        public static readonly Color Paper = new(.957f, .953f, .937f, .86f), Ink = new(.071f, .075f, .078f, 1), InkSoft = new(.071f, .075f, .078f, .55f),
            Volt = new(.831f, 1, 0, 1), Red = new(1, .1f, .1f, 1), White = new(.97f, .965f, .95f, 1), Glass = new(.07f, .075f, .08f, .5f);
        static Sprite pill, round, dot, ring;
        public static Sprite PillSprite => pill; public static Sprite CardSprite => round; public static Sprite DotSprite => dot; public static Sprite RingSprite => ring;

        public static void Init()
        {
            Mono = Resources.Load<Font>("Fonts/IBMPlexMono-Regular"); MonoBold = Resources.Load<Font>("Fonts/IBMPlexMono-SemiBold"); Wide = Resources.Load<Font>("Fonts/Michroma-Regular");
            if (Mono == null) Mono = Font.CreateDynamicFontFromOSFont("Menlo", 16); if (MonoBold == null) MonoBold = Mono; if (Wide == null) Wide = MonoBold;
            pill = RoundSprite(64, 32, 2); round = RoundSprite(64, 18, 2); dot = RoundSprite(32, 16, 0); ring = RoundSprite(64, 32, 3, true);
        }

        /// <summary>A 9-sliced anti-aliased rounded-rect sprite (corner radius r px of a size×size texture).</summary>
        static Sprite RoundSprite(int size, float r, float aa, bool hollow = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size]; float h = size / 2f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + .5f - h) - (h - r), qy = Mathf.Abs(y + .5f - h) - (h - r);
                float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                float a = Mathf.Clamp01(.5f - d / Mathf.Max(1, aa)); if (hollow) a *= Mathf.Clamp01(.5f + (d + 3) / Mathf.Max(1, aa));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px); t.Apply(false, true);
            float b = Mathf.Min(r + 1, h - 1);
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static Canvas MakeCanvas(Transform parent, out RectTransform safe)
        {
            var go = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 10;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; sc.scaleFactor = UIScale.Factor; go.AddComponent<UIScale>(); // 1 unit ≈ 1 iPhone point on any screen
            if (Object.FindFirstObjectByType<EventSystem>() == null) { var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); es.transform.SetParent(parent, false); }
            safe = Rect("Safe", go.transform); safe.gameObject.AddComponent<SafeArea>();
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2? aMin = null, Vector2? aMax = null, Vector2? offMin = null, Vector2? offMax = null)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var r = (RectTransform)go.transform;
            r.anchorMin = aMin ?? Vector2.zero; r.anchorMax = aMax ?? Vector2.one; r.offsetMin = offMin ?? Vector2.zero; r.offsetMax = offMax ?? Vector2.zero; return r;
        }
        /// <summary>Anchor a fixed-size box at a normalized anchor with a pivot and an offset (points).</summary>
        public static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = Rect(name, parent, anchor, anchor); r.pivot = pivot; r.sizeDelta = size; r.anchoredPosition = pos; return r;
        }
        public static Image Panel(RectTransform r, Color c, bool roundSprite = true)
        {
            var img = r.gameObject.AddComponent<Image>(); img.sprite = roundSprite ? round : null; img.type = Image.Type.Sliced; img.color = c; img.pixelsPerUnitMultiplier = 1; return img;
        }
        public static Text Label(Transform parent, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleLeft, Font f = null, string name = "Label")
        {
            var r = Rect(name, parent); var t = r.gameObject.AddComponent<Text>(); t.font = f ?? Mono; t.fontSize = size; t.color = c; t.alignment = a; t.text = s;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; t.supportRichText = false; return t;
        }
        public static Button Pill(Transform parent, string s, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, System.Action onClick, bool primary = false, int fontSize = 12)
        {
            var r = Box("Btn_" + s, parent, anchor, pivot, pos, size); var img = r.gameObject.AddComponent<Image>(); img.sprite = pill; img.type = Image.Type.Sliced;
            img.color = primary ? Ink : Paper;
            var b = r.gameObject.AddComponent<Button>(); var cb = b.colors; cb.pressedColor = new Color(.85f, .85f, .85f); cb.fadeDuration = .06f; b.colors = cb; b.targetGraphic = img;
            b.onClick.AddListener(() => { Haptics.Play(Haptics.Kind.Selection); onClick?.Invoke(); });
            var l = Label(r, s, fontSize, primary ? White : Ink, TextAnchor.MiddleCenter, MonoBold); l.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }
        public static Image Dot(Transform parent, Vector2 anchor, Vector2 pos, float d, Color c, bool hollow = false)
        {
            var r = Box("Dot", parent, anchor, new Vector2(.5f, .5f), pos, new Vector2(d, d)); var img = r.gameObject.AddComponent<Image>(); img.sprite = hollow ? ring : dot; img.color = c; img.raycastTarget = false; return img;
        }
        public static RawImage Matrix(Transform parent, string text, float height, Color fg, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            var tex = DotMatrix.Render(text, 8, Color.white, new Color(0, 0, 0, 0), false, false, 0); tex.filterMode = FilterMode.Bilinear;
            float w = height * tex.width / tex.height; var r = Box("Matrix", parent, anchor, pivot, pos, new Vector2(w, height));
            var ri = r.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.color = fg; ri.raycastTarget = false; return ri;
        }
        public static void SetMatrix(RawImage ri, string text, float height)
        {
            if (ri.texture is Texture2D old) Object.Destroy(old);
            var tex = DotMatrix.Render(text, 8, Color.white, new Color(0, 0, 0, 0), false, false, 0); tex.filterMode = FilterMode.Bilinear; ri.texture = tex;
            ri.rectTransform.sizeDelta = new Vector2(height * tex.width / tex.height, height);
        }
    }

    /// <summary>One UI unit ≈ one iPhone point: the short side of the screen is treated as ~420 units, on phones, tablets and desktop browsers alike.</summary>
    public class UIScale : MonoBehaviour
    {
        public static float Factor => Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 420f, 1f, 4f);
        CanvasScaler sc; void Awake() { sc = GetComponent<CanvasScaler>(); }
        void Update() { if (sc != null && !Mathf.Approximately(sc.scaleFactor, Factor)) sc.scaleFactor = Factor; }
    }

    /// <summary>Keeps a RectTransform inside Screen.safeArea (notch, home indicator, rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last; RectTransform r;
        void Awake() { r = (RectTransform)transform; Apply(); }
        void Update() { if (Screen.safeArea != last) Apply(); }
        void Apply()
        {
            last = Screen.safeArea; if (Screen.width == 0) return;
            r.anchorMin = new Vector2(last.xMin / Screen.width, last.yMin / Screen.height); r.anchorMax = new Vector2(last.xMax / Screen.width, last.yMax / Screen.height);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Taptic Engine feedback via a tiny native plugin on iOS; no-op elsewhere.</summary>
    public static class Haptics
    {
        public enum Kind { Selection = 0, Light = 1, Medium = 2, Heavy = 3, Success = 4, Error = 5 }
        public static bool enabled = true;
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void VS_Haptic(int kind);
#else
        static void VS_Haptic(int kind) { }
#endif
        public static void Play(Kind k) { if (enabled) VS_Haptic((int)k); }
    }
}
