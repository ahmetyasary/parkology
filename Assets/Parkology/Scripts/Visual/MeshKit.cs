using System.Collections.Generic;
using UnityEngine;

namespace Parkology
{
    /// <summary>Procedural meshes for the toy-brick look: rounded boxes, studs and wheels.</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        static readonly Vector2 NoDecal = new Vector2(-1f, -1f);

        /// <summary>White with alpha 1 = tinted by the material colour; alpha 0 = fixed colour.</summary>
        public static readonly Color Tinted = Color.white;

        public static Color Fixed(Color c) => new Color(c.r, c.g, c.b, 0f);

        /// <summary>Tint multiplier that keeps the material colour but darkens/lightens it.</summary>
        public static Color Shade(float k) => new Color(k, k, k, 1f);

        /// <summary>
        /// Rounded box centred at center. decalFace: 0 none, otherwise the outward axis that receives the face decal
        /// (e.g. Vector3.back for the front of a block seen by the camera).
        /// </summary>
        public void RoundedBox(Vector3 center, Vector3 size, float radius, int seg, Color color, Vector3 decalFace = default, bool flipDecal = false)
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) - 0.0001f);
            Vector3 inner = h - Vector3.one * radius;
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in axes)
            {
                // u/v span the face; (u, v) is "right, up" for a viewer looking at the face from outside.
                Vector3 u, v;
                if (n == Vector3.up) { u = Vector3.right; v = Vector3.forward; }
                else if (n == Vector3.down) { u = Vector3.right; v = Vector3.back; }
                else { v = Vector3.up; u = Vector3.Cross(n, v); }
                float hu = Mathf.Abs(Vector3.Dot(h, u)), hv = Mathf.Abs(Vector3.Dot(h, v)), hn = Mathf.Abs(Vector3.Dot(h, n));
                float iu = Mathf.Abs(Vector3.Dot(inner, u)), iv = Mathf.Abs(Vector3.Dot(inner, v));
                float[] su = Stops(iu, hu - iu, seg), sv = Stops(iv, hv - iv, seg);
                bool decal = decalFace != Vector3.zero && n == decalFace;
                int start = verts.Count;
                for (int j = 0; j < sv.Length; j++)
                {
                    for (int i = 0; i < su.Length; i++)
                    {
                        Vector3 p = n * hn + u * su[i] + v * sv[j];
                        Vector3 clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 off = p - clamped;
                        Vector3 normal = off.sqrMagnitude > 1e-10f ? off.normalized : n;
                        verts.Add(center + clamped + normal * radius);
                        normals.Add(normal);
                        colors.Add(color);
                        Vector2 uv = new Vector2((su[i] + hu) / (2f * hu), (sv[j] + hv) / (2f * hv));
                        if (flipDecal) uv = Vector2.one - uv;
                        uvs.Add(decal ? uv : NoDecal);
                    }
                }
                int w = su.Length;
                for (int j = 0; j < sv.Length - 1; j++)
                    for (int i = 0; i < w - 1; i++)
                    {
                        int a = start + j * w + i, b = a + w, c = b + 1, d = a + 1;
                        Quad(a, b, c, d, n);
                    }
            }
        }

        // Positions along one face axis: denser near the rounded edges (equal angular steps).
        static float[] Stops(float inner, float r, int seg)
        {
            var list = new List<float>();
            for (int k = seg; k >= 1; k--) list.Add(-inner - r * Mathf.Tan(Mathf.PI * 0.25f * k / seg));
            list.Add(-inner);
            if (inner > 1e-4f) list.Add(inner);
            for (int k = 1; k <= seg; k++) list.Add(inner + r * Mathf.Tan(Mathf.PI * 0.25f * k / seg));
            return list.ToArray();
        }

        /// <summary>Cylinder standing on base along axis, with a slightly bevelled top cap.</summary>
        public void Cylinder(Vector3 basePoint, Vector3 axis, float radius, float height, int segments, Color color, bool bottomCap = false)
        {
            axis.Normalize();
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 side2 = Vector3.Cross(axis, side);
            float bevel = Mathf.Min(radius * 0.25f, height * 0.4f);
            // rings: bottom, below bevel, top-outer(bevelled) ; normals smooth on the side
            int start = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 dir = side * Mathf.Cos(a) + side2 * Mathf.Sin(a);
                Add(basePoint + dir * radius, dir, color);
                Add(basePoint + dir * radius + axis * (height - bevel), dir, color);
                Add(basePoint + dir * (radius - bevel) + axis * height, (dir + axis).normalized, color);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 3, b = a + 3;
                Quad(a, a + 1, b + 1, b, side * 0 + Vector3.zero, true, basePoint, axis);
                Quad(a + 1, a + 2, b + 2, b + 1, Vector3.zero, true, basePoint, axis);
            }
            int capCenter = verts.Count;
            Add(basePoint + axis * height, axis, color);
            int capStart = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 dir = side * Mathf.Cos(a) + side2 * Mathf.Sin(a);
                Add(basePoint + dir * (radius - bevel) + axis * height, axis, color);
            }
            for (int i = 0; i < segments; i++) Tri(capCenter, capStart + i, capStart + i + 1, axis);
            if (bottomCap)
            {
                int bc = verts.Count;
                Add(basePoint, -axis, color);
                int bs = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    Add(basePoint + (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * radius, -axis, color);
                }
                for (int i = 0; i < segments; i++) Tri(bc, bs + i, bs + i + 1, -axis);
            }
        }

        /// <summary>Flat quad lying on XZ at height y (normal up).</summary>
        public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int s = verts.Count;
            Add(a, Vector3.up, color); Add(b, Vector3.up, color); Add(c, Vector3.up, color); Add(d, Vector3.up, color);
            Quad(s, s + 1, s + 2, s + 3, Vector3.up);
        }

        /// <summary>Flat-shaded quad facing along normal.</summary>
        public void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
        {
            int s = verts.Count;
            Add(a, normal, color); Add(b, normal, color); Add(c, normal, color); Add(d, normal, color);
            Quad(s, s + 1, s + 2, s + 3, normal);
        }

        /// <summary>Flat polygon strip (pairs of inner/outer points) at height y, normal up.</summary>
        public void Band(IList<Vector3> inner, IList<Vector3> outer, Color color, bool closed)
        {
            int s = verts.Count;
            for (int i = 0; i < inner.Count; i++)
            {
                Add(inner[i], Vector3.up, color);
                Add(outer[i], Vector3.up, color);
            }
            int n = inner.Count;
            int last = closed ? n : n - 1;
            for (int i = 0; i < last; i++)
            {
                int a = s + i * 2, b = s + ((i + 1) % n) * 2;
                Quad(a, a + 1, b + 1, b, Vector3.up);
            }
        }

        /// <summary>Vertical wall following a polyline (for kerbs), facing outward from the given side.</summary>
        public void Wall(IList<Vector3> line, float height, Color color, bool closed, float outwardSign)
        {
            int n = line.Count;
            int last = closed ? n : n - 1;
            for (int i = 0; i < last; i++)
            {
                Vector3 p0 = line[i], p1 = line[(i + 1) % n];
                Vector3 dir = (p1 - p0).normalized;
                Vector3 normal = Vector3.Cross(Vector3.up, dir) * outwardSign;
                int s = verts.Count;
                Add(p0, normal, color); Add(p0 + Vector3.up * height, normal, color);
                Add(p1 + Vector3.up * height, normal, color); Add(p1, normal, color);
                Quad(s, s + 1, s + 2, s + 3, normal);
            }
        }

        void Add(Vector3 p, Vector3 n, Color c)
        {
            verts.Add(p);
            normals.Add(n);
            colors.Add(c);
            uvs.Add(NoDecal);
        }

        // Emits two triangles and fixes winding so the front face points along the expected normal.
        void Quad(int a, int b, int c, int d, Vector3 expected, bool radial = false, Vector3 origin = default, Vector3 axis = default)
        {
            Vector3 n = expected;
            if (radial)
            {
                Vector3 mid = (verts[a] + verts[c]) * 0.5f - origin;
                n = mid - axis * Vector3.Dot(mid, axis);
                if (n.sqrMagnitude < 1e-8f) n = normals[a];
            }
            Vector3 geo = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (geo.sqrMagnitude < 1e-12f) geo = Vector3.Cross(verts[c] - verts[a], verts[d] - verts[a]);
            if (Vector3.Dot(geo, n) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
        }

        void Tri(int a, int b, int c, Vector3 expected)
        {
            Vector3 geo = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(geo, expected) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetColors(colors);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }
    }

    /// <summary>Shared meshes and materials, created once during loading.</summary>
    public static class Kit
    {
        public static Mesh Block, Hammer, Debris;
        public static Material Plastic, Flat;
        public const float BlockW = 0.84f, BlockH = 0.68f;

        public static void Build()
        {
            if (Block != null) return;
            Plastic = new Material(Resources.Load<Shader>("Shaders/Plastic")) { name = "Plastic", enableInstancing = false };
            Plastic.SetTexture("_MainTex", FaceAtlas.Texture);
            Flat = new Material(Resources.Load<Shader>("Shaders/Flat")) { name = "Flat" };

            // Block car (+Z = direction of travel): a dark chassis with wheels and a tinted brick body whose top
            // carries the face. Four low corner studs keep it symmetric, since the face always turns upright (Buddy).
            var b = new MeshBuilder();
            b.RoundedBox(new Vector3(0, 0.19f, 0), new Vector3(0.8f, 0.18f, 0.86f), 0.07f, 2, MeshBuilder.Shade(0.6f));
            b.RoundedBox(new Vector3(0, 0.46f, 0), new Vector3(BlockW, 0.42f, 0.82f), 0.12f, 3, MeshBuilder.Tinted, Vector3.up);
            for (int i = 0; i < 4; i++)
                b.Cylinder(new Vector3((i % 2 == 0 ? -1 : 1) * 0.3f, 0.66f, (i < 2 ? -1 : 1) * 0.29f), Vector3.up, 0.065f, 0.05f, 12, MeshBuilder.Shade(0.9f));
            AddWheels(b, 0.42f, new[] { -0.26f, 0.26f }, 0.15f);
            Block = b.Build("BlockCar");

            // Hammer used by the smash booster.
            var hm = new MeshBuilder();
            hm.RoundedBox(new Vector3(0, 0.55f, 0), new Vector3(0.12f, 1.1f, 0.12f), 0.05f, 2, MeshBuilder.Fixed(new Color(0.85f, 0.55f, 0.32f)));
            hm.RoundedBox(new Vector3(0, 1.2f, 0), new Vector3(0.75f, 0.36f, 0.36f), 0.12f, 3, MeshBuilder.Tinted);
            Hammer = hm.Build("Hammer");

            var d = new MeshBuilder();
            d.RoundedBox(new Vector3(0, 0.05f, 0), new Vector3(0.16f, 0.1f, 0.16f), 0.03f, 1, MeshBuilder.Tinted);
            d.Cylinder(new Vector3(0, 0.09f, 0), Vector3.up, 0.04f, 0.03f, 8, MeshBuilder.Tinted);
            Debris = d.Build("Debris");
        }

        static void AddWheels(MeshBuilder m, float x, float[] zs, float radius)
        {
            foreach (float z in zs)
                foreach (float side in new[] { -1f, 1f })
                {
                    m.Cylinder(new Vector3(side * (x - 0.06f), radius, z), Vector3.right * side, radius, 0.08f, 16, MeshBuilder.Fixed(Palette.Wheel), true);
                    m.Cylinder(new Vector3(side * (x + 0.02f), radius, z), Vector3.right * side, radius * 0.45f, 0.02f, 10, MeshBuilder.Fixed(Palette.Hub));
                }
        }

    }
}
