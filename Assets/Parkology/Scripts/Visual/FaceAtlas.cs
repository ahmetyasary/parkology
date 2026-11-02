using UnityEngine;

namespace Parkology
{
    public enum Face { Happy = 0, Blink = 1, Surprised = 2, Joy = 3, Worried = 4, Mystery = 5, Sleepy = 6, Wink = 7 }

    /// <summary>
    /// Procedurally painted expression sheet (4 x 2 cells). Shapes are signed-distance fields,
    /// so edges are anti-aliased at any size.
    /// </summary>
    public static class FaceAtlas
    {
        const int CellW = 256, CellH = 256, Cols = 4, Rows = 2;
        static Texture2D texture;
        static Color32[] pixels;

        public static Texture2D Texture
        {
            get
            {
                if (texture == null) Paint();
                return texture;
            }
        }

        public static Vector4 Rect(Face f)
        {
            int i = (int)f;
            int col = i % Cols, row = i / Cols;
            // row 0 is the top row of the sheet; texture v grows upward
            return new Vector4(col / (float)Cols, (Rows - 1 - row) / (float)Rows, 1f / Cols, 1f / Rows);
        }

        static readonly Color Ink = new Color(0.12f, 0.145f, 0.31f, 1f);
        static readonly Color White = Color.white;
        static readonly Color Blush = new Color(1f, 0.36f, 0.55f, 0.5f);
        static readonly Color Tongue = new Color(1f, 0.45f, 0.5f, 1f);

        static void Paint()
        {
            int w = CellW * Cols, h = CellH * Rows;
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Faces", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            pixels = new Color32[w * h];
            for (int f = 0; f < Cols * Rows; f++)
            {
                int ox = (f % Cols) * CellW, oy = (Rows - 1 - f / Cols) * CellH;
                for (int y = 0; y < CellH; y++)
                    for (int x = 0; x < CellW; x++)
                    {
                        Color c = Shade((Face)f, new Vector2(x + 0.5f, y + 0.5f));
                        pixels[(oy + y) * w + ox + x] = c;
                    }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            pixels = null;
        }

        // Layer helpers: each returns the new colour after compositing a shape with coverage from its SDF.
        static Color Over(Color dst, Color src, float sdf)
        {
            float a = Mathf.Clamp01(0.5f - sdf) * src.a;
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return new Color(0, 0, 0, 0);
            Color rgb = (src * a + dst * dst.a * (1f - a)) / outA;
            rgb.a = outA;
            return rgb;
        }

        static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
        {
            Vector2 q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
            return (q.magnitude - 1f) * Mathf.Min(r.x, r.y);
        }

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Segment(Vector2 p, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 pa = p - a, ba = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * t).magnitude - thickness * 0.5f;
        }

        /// <summary>Arc from angle a0 to a1 (degrees, counter-clockwise, 0 = +x) with round caps.</summary>
        static float Arc(Vector2 p, Vector2 c, float r, float a0, float a1, float thickness)
        {
            Vector2 d = p - c;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float span = Mathf.Repeat(a1 - a0, 360f);
            float rel = Mathf.Repeat(ang - a0, 360f);
            if (rel <= span) return Mathf.Abs(d.magnitude - r) - thickness * 0.5f;
            Vector2 e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
            Vector2 e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
            return Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - thickness * 0.5f;
        }

        static Color Eye(Color c, Vector2 p, Vector2 at, float rx, float ry)
        {
            c = Over(c, Ink, Ellipse(p, at, new Vector2(rx, ry)));
            c = Over(c, White, Circle(p, at + new Vector2(-rx * 0.3f, ry * 0.38f), rx * 0.42f));
            c = Over(c, White, Circle(p, at + new Vector2(rx * 0.35f, -ry * 0.35f), rx * 0.16f));
            return c;
        }

        static Color Shade(Face f, Vector2 p)
        {
            var c = new Color(0, 0, 0, 0);
            p.y -= 34f; // features were laid out on a 256 x 180 cell; centre them in the square cell
            const float cx = CellW * 0.5f;
            Vector2 le = new Vector2(cx - 46f, 104f), re = new Vector2(cx + 46f, 104f);
            if (f != Face.Mystery)
            {
                c = Over(c, Blush, Ellipse(p, new Vector2(cx - 76f, 74f), new Vector2(17f, 10f)));
                c = Over(c, Blush, Ellipse(p, new Vector2(cx + 76f, 74f), new Vector2(17f, 10f)));
            }
            switch (f)
            {
                case Face.Happy:
                    c = Eye(c, p, le, 15f, 21f);
                    c = Eye(c, p, re, 15f, 21f);
                    c = Over(c, Ink, Arc(p, new Vector2(cx, 84f), 24f, 205f, 335f, 7f));
                    break;
                case Face.Blink:
                    c = Over(c, Ink, Arc(p, le + new Vector2(0, 6f), 14f, 200f, 340f, 6f));
                    c = Over(c, Ink, Arc(p, re + new Vector2(0, 6f), 14f, 200f, 340f, 6f));
                    c = Over(c, Ink, Arc(p, new Vector2(cx, 84f), 24f, 205f, 335f, 7f));
                    break;
                case Face.Surprised:
                    c = Eye(c, p, le, 17f, 24f);
                    c = Eye(c, p, re, 17f, 24f);
                    c = Over(c, Ink, Ellipse(p, new Vector2(cx, 54f), new Vector2(12f, 15f)));
                    c = Over(c, Tongue, Ellipse(p, new Vector2(cx, 47f), new Vector2(7f, 5f)));
                    break;
                case Face.Joy:
                    c = Over(c, Ink, Arc(p, le - new Vector2(0, 8f), 15f, 20f, 160f, 7f));
                    c = Over(c, Ink, Arc(p, re - new Vector2(0, 8f), 15f, 20f, 160f, 7f));
                    {
                        float mouth = Mathf.Max(Ellipse(p, new Vector2(cx, 74f), new Vector2(28f, 26f)), p.y - 74f);
                        c = Over(c, Ink, mouth);
                        float tongue = Mathf.Max(Ellipse(p, new Vector2(cx, 54f), new Vector2(15f, 10f)), mouth + 1.5f);
                        c = Over(c, Tongue, tongue);
                    }
                    break;
                case Face.Worried:
                    c = Eye(c, p, le + new Vector2(0, -2f), 13f, 18f);
                    c = Eye(c, p, re + new Vector2(0, -2f), 13f, 18f);
                    c = Over(c, Ink, Segment(p, le + new Vector2(-16f, 30f), le + new Vector2(14f, 40f), 7f));
                    c = Over(c, Ink, Segment(p, re + new Vector2(16f, 30f), re + new Vector2(-14f, 40f), 7f));
                    c = Over(c, Ink, Arc(p, new Vector2(cx, 44f), 20f, 40f, 140f, 7f));
                    break;
                case Face.Mystery:
                    {
                        Vector2 hook = new Vector2(cx, 116f);
                        float q = Mathf.Min(Arc(p, hook, 30f, -70f, 200f, 18f), Segment(p, new Vector2(cx + 10f, 88f), new Vector2(cx, 70f), 18f));
                        q = Mathf.Min(q, Segment(p, new Vector2(cx, 70f), new Vector2(cx, 62f), 18f));
                        float dot = Circle(p, new Vector2(cx, 36f), 11f);
                        c = Over(c, Ink, Mathf.Min(q, dot) - 7f);
                        c = Over(c, White, Mathf.Min(q, dot));
                    }
                    break;
                case Face.Sleepy:
                    c = Over(c, Ink, Segment(p, le + new Vector2(-14f, 0), le + new Vector2(14f, 0), 7f));
                    c = Over(c, Ink, Segment(p, re + new Vector2(-14f, 0), re + new Vector2(14f, 0), 7f));
                    c = Over(c, Ink, Ellipse(p, new Vector2(cx, 62f), new Vector2(8f, 9f)));
                    break;
                case Face.Wink:
                    c = Eye(c, p, le, 15f, 21f);
                    c = Over(c, Ink, Arc(p, re - new Vector2(0, 8f), 15f, 20f, 160f, 7f));
                    c = Over(c, Ink, Arc(p, new Vector2(cx, 86f), 26f, 205f, 335f, 7f));
                    break;
            }
            return c;
        }
    }
}
