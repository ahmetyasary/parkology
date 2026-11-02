using System.Collections.Generic;
using UnityEngine;

namespace Parkology
{
    /// <summary>Static scenery for one board: studded baseplate, parking lot, the loop road and the depot lanes below it.</summary>
    public static class WorldView
    {
        public const float RoadWidth = 1.05f;

        public static GameObject Build(Transform parent, RingPath ring, int cols, int rows, float[] laneX, float laneHead)
        {
            var root = new GameObject("World");
            root.transform.SetParent(parent, false);

            Add(root, "Ground", Ground(ring, laneX, laneHead), false);
            Add(root, "Roads", Roads(ring, laneX, laneHead), false);
            Add(root, "Lot", Lot(ring, cols, rows), false);
            return root;
        }

        static void Add(GameObject root, string name, Mesh mesh, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Kit.Plastic;
            r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", Color.white);
            block.SetVector("_DecalRect", Vector4.zero);
            block.SetFloat("_Gloss", 0.15f);
            block.SetFloat("_Rim", 0f);
            r.SetPropertyBlock(block);
        }

        static Mesh Ground(RingPath ring, float[] laneX, float laneHead)
        {
            var m = new MeshBuilder();
            Color g = MeshBuilder.Fixed(Palette.Ground);
            m.FlatQuad(new Vector3(-40, 0, -60), new Vector3(-40, 0, 60), new Vector3(40, 0, 60), new Vector3(40, 0, -60), g);
            // Baseplate studs everywhere except under roads, lot and lanes.
            Vector3 c = ring.Center;
            float outerW = ring.HalfW + RoadWidth * 0.5f + 0.25f, outerH = ring.HalfH + RoadWidth * 0.5f + 0.25f;
            Color stud = MeshBuilder.Fixed(Palette.GroundStud);
            for (float z = -26f; z <= 26f; z += 1f)
                for (float x = -9.5f; x <= 9.5f; x += 1f)
                {
                    float wx = c.x + x, wz = c.z + z;
                    if (Mathf.Abs(x) < outerW && Mathf.Abs(z) < outerH) continue;
                    float laneEdge = laneX[laneX.Length - 1] + RoadWidth * 0.5f + 0.3f;
                    if (Mathf.Abs(wx) < laneEdge && wz < c.z) continue; // depot lanes
                    m.Cylinder(new Vector3(wx, 0, wz), Vector3.up, 0.2f, 0.07f, 10, stud);
                }
            return m.Build("Ground");
        }

        static Mesh Roads(RingPath ring, float[] laneX, float laneHead)
        {
            var m = new MeshBuilder();
            Color asphalt = MeshBuilder.Fixed(Palette.Road), edge = MeshBuilder.Fixed(Palette.RoadEdge), line = MeshBuilder.Fixed(Palette.RoadLine);
            float hw = RoadWidth * 0.5f;
            const float y = 0.02f;

            // Loop band
            var inner = new List<Vector3>();
            var outer = new List<Vector3>();
            int steps = Mathf.CeilToInt(ring.Length / 0.08f);
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = ring.Evaluate(ring.Length * i / steps, out Vector3 t);
                Vector3 outward = Vector3.Cross(t, Vector3.up);
                inner.Add(p - outward * hw + Vector3.up * y);
                outer.Add(p + outward * hw + Vector3.up * y);
            }
            m.Band(inner, outer, asphalt, true);
            m.Wall(outer, -0.02f, edge, true, 1f);

            // Dashed centre line
            for (float s = 0.2f; s < ring.Length - 0.3f; s += 0.62f)
            {
                Vector3 a = ring.Evaluate(s, out Vector3 ta), b = ring.Evaluate(s + 0.28f, out Vector3 tb);
                Vector3 na = Vector3.Cross(ta, Vector3.up) * 0.04f, nb = Vector3.Cross(tb, Vector3.up) * 0.04f;
                Vector3 up = Vector3.up * (y + 0.004f);
                m.FlatQuad(a - na + up, a + na + up, b + nb + up, b - nb + up, line);
            }

            // Depot lanes run down from the bottom straight; blocks drive up out of them onto the loop.
            foreach (float lx in laneX)
            {
                float z0 = ring.Center.z - ring.HalfH, z1 = laneHead - 12f;
                m.FlatQuad(new Vector3(lx - hw, y, z1), new Vector3(lx - hw, y, z0), new Vector3(lx + hw, y, z0), new Vector3(lx + hw, y, z1), asphalt);
                for (float z = laneHead + 0.52f; z > z1; z -= 1.05f)
                    m.FlatQuad(new Vector3(lx - 0.3f, y + 0.004f, z - 0.05f), new Vector3(lx - 0.3f, y + 0.004f, z),
                        new Vector3(lx + 0.3f, y + 0.004f, z), new Vector3(lx + 0.3f, y + 0.004f, z - 0.05f), line);
            }
            return m.Build("Roads");
        }

        static Mesh Lot(RingPath ring, int cols, int rows)
        {
            var m = new MeshBuilder();
            Vector3 c = ring.Center;
            float w = ring.HalfW - RoadWidth * 0.5f - 0.15f, h = ring.HalfH - RoadWidth * 0.5f - 0.15f;
            // Raised plate with rounded corners.
            m.RoundedBox(c + new Vector3(0, -0.02f, 0), new Vector3(w * 2f, 0.12f, h * 2f), Mathf.Min(0.5f, ring.Radius - RoadWidth * 0.5f + 0.06f), 2, MeshBuilder.Fixed(Palette.Lot));
            Color lineC = MeshBuilder.Fixed(Palette.LotLine);
            float y = 0.045f;
            // Column separators, and pickup arrows at both ends of every column.
            for (int i = 0; i <= cols; i++)
            {
                float x = c.x + (i - cols * 0.5f);
                float z0 = c.z - rows * 0.5f - 0.05f, z1 = c.z + rows * 0.5f + 0.05f;
                m.FlatQuad(new Vector3(x - 0.03f, y, z0), new Vector3(x - 0.03f, y, z1), new Vector3(x + 0.03f, y, z1), new Vector3(x + 0.03f, y, z0), lineC);
            }
            Color arrow = MeshBuilder.Fixed(Palette.RoadLine * 0.97f);
            for (int i = 0; i < cols; i++)
            {
                float x = c.x + (i - (cols - 1) * 0.5f);
                foreach (float side in new[] { 1f, -1f })
                {
                    float z = c.z + side * (rows * 0.5f + 0.24f);
                    // both arrows point down: blocks join at the top and leave at the bottom
                    Vector3 tip = new Vector3(x, y + 0.002f, z - 0.13f), l = new Vector3(x - 0.14f, y + 0.002f, z + 0.05f), r = new Vector3(x + 0.14f, y + 0.002f, z + 0.05f);
                    m.FlatQuad(l, tip, tip, r, MeshBuilder.Fixed(Palette.LotLine));
                }
            }
            return m.Build("Lot");
        }
    }
}
