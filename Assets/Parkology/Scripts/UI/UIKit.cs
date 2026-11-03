using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Parkology
{
    /// <summary>Code-built uGUI helpers. Units are reference pixels of a 1080 x 2340 portrait canvas.</summary>
    public static class UIKit
    {
        static Font font;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static readonly Color Ink = new Color32(0x1F, 0x25, 0x50, 0xFF);
        public static readonly Color Brown = new Color32(0x6B, 0x45, 0x2A, 0xFF);

        public static Font Font => font != null ? font : (font = Resources.Load<Font>("Fonts/RammettoOne-Regular"));

        public static Sprite Sprite(string name)
        {
            if (sprites.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("Art/" + name);
            if (s == null) Debug.LogWarning("Missing sprite " + name);
            sprites[name] = s;
            return s;
        }

        public static RectTransform Node(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Node(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(Transform parent, string sprite, Vector2 anchor, Vector2 pos, Vector2 size, bool sliced = false, bool raycast = false)
        {
            var rt = Node(parent, sprite, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Sprite(sprite);
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = !sliced;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Fill(Transform parent, string name, Color color, bool raycast)
        {
            var rt = Stretch(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Text with best-fit sizing so long Turkish words shrink instead of overflowing.</summary>
        public static Text Text(Transform parent, string key, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, Color? outline = null, float outlineWidth = 5f, bool localized = true, params object[] args)
        {
            var rt = Node(parent, "Text", anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = size;
            t.resizeTextMinSize = Mathf.Max(10, size / 3);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.lineSpacing = 0.9f;
            if (outline.HasValue)
            {
                var o = rt.gameObject.AddComponent<ThickOutline>();
                o.color = outline.Value;
                o.width = outlineWidth;
            }
            if (localized) LocText.Bind(t, key, args);
            else t.text = key;
            return t;
        }

        public static Button Button(Transform parent, string sprite, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, bool sliced = true)
        {
            var img = Image(parent, sprite, anchor, pos, size, sliced, true);
            img.name = "Button " + sprite;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onClick?.Invoke());
            img.gameObject.AddComponent<Press>();
            return b;
        }

        /// <summary>A coloured pill button with a localised label (and optional icon on the left).</summary>
        public static Button LabelButton(Transform parent, string sprite, string key, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, string icon = null, int fontSize = 64)
        {
            var b = Button(parent, sprite, anchor, pos, size, onClick);
            float iconW = 0f;
            if (icon != null)
            {
                iconW = size.y * 0.62f;
                Image(b.transform, icon, new Vector2(0f, 0.5f), new Vector2(size.y * 0.24f + iconW * 0.5f, 6f), new Vector2(iconW, iconW));
            }
            var label = Text(b.transform, key, fontSize, Color.white, new Vector2(0.5f, 0.5f), new Vector2(iconW * 0.5f, 8f),
                new Vector2(size.x - iconW - size.y * 0.5f, size.y * 0.62f), TextAnchor.MiddleCenter, Ink, 6f);
            label.name = "Label";
            return b;
        }
    }

    /// <summary>Squishy press feedback with a tap sound and haptic.</summary>
    public sealed class Press : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        Vector3 rest = Vector3.one;
        bool down;
        public bool Silent;

        void Awake() => rest = transform.localScale;

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            var t = transform;
            Vector3 from = t.localScale;
            Tween.Run(0.08f, k => { if (t) t.localScale = Vector3.Lerp(from, rest * 0.9f, k); }, Ease.OutQuad, null, 0f, t, ("press", t));
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) { if (down) Release(); }

        public void OnPointerClick(PointerEventData e)
        {
            if (Silent) return;
            Sfx.Play("tap", 0.8f);
            Haptics.Play(Haptic.Tap);
        }

        void Release()
        {
            down = false;
            var t = transform;
            Vector3 from = t.localScale;
            Tween.Run(0.32f, k => { if (t) t.localScale = Vector3.LerpUnclamped(from, rest, k); }, Ease.OutElastic, null, 0f, t, ("press", t));
        }
    }

    /// <summary>A solid outline drawn as several offset copies of the glyphs (chunkier than the built-in Outline).</summary>
    public sealed class ThickOutline : BaseMeshEffect
    {
        public Color color = Color.black;
        public float width = 5f;
        static readonly List<UIVertex> verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            int count = verts.Count;
            const int dirs = 12;
            var result = new List<UIVertex>(count * (dirs + 2));
            // drop shadow
            AddCopy(result, count, new Vector2(0, -width * 1.1f));
            for (int i = 0; i < dirs; i++)
            {
                float a = i * Mathf.PI * 2f / dirs;
                AddCopy(result, count, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * width);
            }
            result.AddRange(verts);
            vh.Clear();
            vh.AddUIVertexTriangleStream(result);
        }

        void AddCopy(List<UIVertex> result, int count, Vector2 offset)
        {
            for (int i = 0; i < count; i++)
            {
                var v = verts[i];
                v.position += (Vector3)offset;
                var c = color;
                c.a *= v.color.a / 255f;
                v.color = c;
                result.Add(v);
            }
        }
    }

    /// <summary>Keeps a RectTransform inside the device safe area (notch, home indicator).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int screen;

        void Update()
        {
            var area = Screen.safeArea;
            if (area == applied && screen.x == Screen.width && screen.y == Screen.height) return;
            applied = area;
            screen = new Vector2Int(Screen.width, Screen.height);
            var rt = (RectTransform)transform;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rt.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Slow continuous rotation (reward rays) or bobbing (idle hints).</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float speed = 20f;
        void Update() => transform.Rotate(0, 0, -speed * Time.unscaledDeltaTime);
    }

    /// <summary>Keeps a UI node over a point that moves on screen (a spot in the 3D world).</summary>
    public sealed class ScreenPin : MonoBehaviour
    {
        RectTransform space;
        Func<Vector3> screen;

        public void Track(RectTransform within, Func<Vector3> screenPoint)
        {
            space = within;
            screen = screenPoint;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (space == null || screen == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen(), null, out Vector2 local))
                ((RectTransform)transform).anchoredPosition = local; // the node is anchored at the centre of space
        }
    }

    public sealed class Bob : MonoBehaviour
    {
        public float amount = 0.04f, rate = 2.2f;
        Vector3 rest;
        void Awake() => rest = transform.localScale;
        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * rate * Mathf.PI) * amount;
            transform.localScale = new Vector3(rest.x * s, rest.y * (2f - s), rest.z);
        }
    }
}
