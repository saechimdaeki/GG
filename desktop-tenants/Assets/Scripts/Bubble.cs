using UnityEngine;

namespace DesktopTenants
{
    /// <summary>
    /// A pixel-bordered speech bubble (or floating "+₩3,000" popup) using the system Korean font.
    /// Sized to its text every frame it is visible.
    /// </summary>
    public class Bubble : MonoBehaviour
    {
        const float TextHeight = 13f, Pad = 5f;
        static Font font;
        static Sprite dot;

        TextMesh text;
        MeshRenderer textR;
        Transform outer, inner;
        float left, rise;
        Vector3 home;

        /// <summary>Extra height (points) the layout pass adds to keep bubbles from overlapping.</summary>
        public float Lift { get; set; }
        /// <summary>World position before any lift.</summary>
        public Vector3 Base => transform.parent ? transform.parent.TransformPoint(home) : home;
        /// <summary>World-space rectangle the bubble covers at its current lift.</summary>
        public Rect Area
        {
            get
            {
                var c = Base + Vector3.up * Lift;
                var size = outer.localScale;
                return new Rect(c.x - size.x / 2, c.y - size.y / 2, size.x, size.y);
            }
        }
        bool destroyAtEnd;

        /// <summary>Moves the bubble's resting spot (relative to its parent).</summary>
        public void SetHome(Vector3 localPos)
        {
            home = localPos;
            transform.localPosition = home + Vector3.up * Lift;
        }

        /// <summary>The system Korean font every in-world label uses.</summary>
        public static Font SystemFont
        {
            get
            {
                if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "AppleSDGothicNeo-Bold", "Apple SD Gothic Neo", "Arial Unicode MS" }, 64);
                return font;
            }
        }

        public static Bubble Create(Transform parent, Vector3 localPos, int order = 20)
        {
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "AppleSDGothicNeo-Bold", "Apple SD Gothic Neo", "Arial Unicode MS" }, 64);
            if (!dot) dot = Pixels.Make(new[] { "w" }, new System.Collections.Generic.Dictionary<char, Color> { { 'w', Color.white } }, 1, new Vector2(0.5f, 0.5f));
            var b = new GameObject("Bubble").AddComponent<Bubble>();
            b.transform.SetParent(parent, false);
            b.transform.localPosition = localPos;
            b.home = localPos;
            b.outer = Quad(b.transform, Pixels.Ink, order);
            b.inner = Quad(b.transform, new Color(1f, 0.98f, 0.92f), order + 1);
            var t = new GameObject("Text");
            t.transform.SetParent(b.transform, false);
            b.text = t.AddComponent<TextMesh>();
            b.text.font = font;
            b.text.fontSize = 64;
            b.text.characterSize = TextHeight * 10f / 64f;
            b.text.anchor = TextAnchor.MiddleCenter;
            b.text.color = Pixels.Ink;
            b.textR = t.GetComponent<MeshRenderer>();
            b.textR.sharedMaterial = font.material;
            b.textR.sortingOrder = order + 2;
            b.Show(false);
            return b;
        }

        /// <summary>A popup that floats up from <paramref name="worldPos"/> and removes itself.</summary>
        public static void Popup(string msg, Vector3 worldPos)
        {
            var b = Create(null, worldPos, 30);
            b.rise = 30f;
            b.destroyAtEnd = true;
            b.Say(msg, 1.3f);
        }

        static Transform Quad(Transform parent, Color c, int order)
        {
            var r = new GameObject("Bg").AddComponent<SpriteRenderer>();
            r.transform.SetParent(parent, false);
            r.sprite = dot;
            r.color = c;
            r.sortingOrder = order;
            return r.transform;
        }

        /// <summary>Shows <paramref name="msg"/> for <paramref name="seconds"/>; 0 or less keeps it up.</summary>
        public void Say(string msg, float seconds = 2.2f)
        {
            if (string.IsNullOrEmpty(msg)) return;
            text.text = msg;
            left = seconds <= 0 ? float.PositiveInfinity : seconds;
            Show(true);
        }

        public void Hide() { left = 0; Show(false); }

        public bool Visible => textR.enabled;

        void Show(bool on)
        {
            textR.enabled = on;
            outer.gameObject.SetActive(on);
            inner.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (!textR.enabled) return;
            left -= Time.deltaTime;
            if (left <= 0)
            {
                if (destroyAtEnd) Destroy(gameObject);
                else Show(false);
                return;
            }
            if (rise > 0) transform.position += Vector3.up * rise * Time.deltaTime;
            else transform.localPosition = home + Vector3.up * Lift;
            var size = textR.bounds.size;
            outer.localScale = new Vector3(size.x + Pad * 2 + 2, size.y + Pad + 2, 1);
            inner.localScale = new Vector3(size.x + Pad * 2, size.y + Pad, 1);
            // Keep bubbles upright and unflipped whatever the parent does.
            transform.rotation = Quaternion.identity;
        }
    }
}
