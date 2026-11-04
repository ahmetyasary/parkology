using System.Collections.Generic;
using UnityEngine;

namespace Parkology
{
    /// <summary>Static scenery for one board: studded baseplate, parking lot, the loop road and the depot lanes below it.</summary>
    public static class WorldView
    {
        public const float RoadWidth = 1.05f;
        /// <summary>Top of the raised parking plate; parked blocks stand on it.</summary>
        public const float LotTop = 0.14f;
        const float CurbHeight = 0.13f, CurbWidth = 0.14f;

        public static GameObject Build(Transform parent, RingPath ring, int cols, int rows, float[] laneX, float laneHead)
        {
            var root = new GameObject("World");
            root.transform.SetParent(parent, false);

            Add(root, "Ground", Ground(ring, laneX, laneHead), false);
            Add(root, "Roads", Roads(ring, laneX, laneHead), false);
            Add(root, "Lot", Lot(ring, cols, rows), false);
            return root;
        }

        static GameObject Add(GameObject root, string name, Mesh mesh, bool castShadows, float gloss = 0.15f)
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
            block.SetFloat("_Gloss", gloss);
            block.SetFloat("_Rim", 0f);
            r.SetPropertyBlock(block);
            return go;
        }

        /// <summary>
        /// A shipping crate that seals a whole column, ribbed like a container, with a little block of its key colour
        /// sitting on the lid: clearing a column of that colour opens it.
        /// </summary>
        public static Transform Crate(Transform parent, Vector3 center, int slots, int key)
        {
            var root = new GameObject("Crate");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;
            var m = new MeshBuilder();
            float len = slots - 0.08f;
            Color body = MeshBuilder.Fixed(Palette.Crate), rib = MeshBuilder.Fixed(Palette.CrateRib), cap = MeshBuilder.Fixed(Palette.CrateEnd);
            m.RoundedBox(new Vector3(0, 0.42f, 0), new Vector3(0.9f, 0.8f, len), 0.08f, 2, body);
            for (float z = -len * 0.5f + 0.45f; z < len * 0.5f - 0.4f; z += 0.32f)
                m.RoundedBox(new Vector3(0, 0.83f, z), new Vector3(0.74f, 0.04f, 0.09f), 0.02f, 1, rib);
            foreach (float side in new[] { -1f, 1f })
                m.RoundedBox(new Vector3(0, 0.44f, side * (len * 0.5f - 0.12f)), new Vector3(0.96f, 0.86f, 0.26f), 0.07f, 2, cap);
            Add(root, "Box", m.Build("Crate"), true, 0.3f);
            var badge = Buddy.Create(Kit.Block, root.transform, key, "Key");
            badge.transform.localPosition = new Vector3(0, 0.86f, 0);
            badge.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            badge.transform.localScale = Vector3.one * 0.62f;
            badge.gameObject.AddComponent<Hover>();
            return root.transform;
        }

        /// <summary>Gentle float so the key block on a crate catches the eye.</summary>
        sealed class Hover : MonoBehaviour
        {
            float phase;
            void Start() => phase = Random.value * 6f;
            void Update()
            {
                var p = transform.localPosition;
                p.y = 0.86f + Mathf.Abs(Mathf.Sin(Time.time * 2.4f + phase)) * 0.12f;
                transform.localPosition = p;
            }
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
            Color asphalt = MeshBuilder.Fixed(Palette.Road), edge = MeshBuilder.Fixed(Palette.RoadEdge);
            // dashes are painted faintly (the road colour shows through) so they guide without drawing the eye
            Color line = MeshBuilder.Fixed(Color.Lerp(Palette.Road, Palette.RoadLine, 0.15f));
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

            // Striped kerb along the outside of the loop, open where the depot lanes join the bottom straight.
            float laneEdge = laneX[laneX.Length - 1] + hw;
            float bottomZ = ring.Center.z - ring.HalfH;
            for (int i = 0; i < steps; i++)
            {
                int j = (i + 1) % steps;
                Vector3 a = outer[i], b = outer[j];
                if (a.z < bottomZ + 0.3f && b.z < bottomZ + 0.3f && Mathf.Abs(a.x) < laneEdge && Mathf.Abs(b.x) < laneEdge) continue;
                Vector3 na = (outer[i] - inner[i]).normalized, nb = (outer[j] - inner[j]).normalized;
                Kerb(m, a, b, na, nb, (int)(ring.Length * i / steps / 0.5f) % 2 == 0);
            }
            // Kerbs down both outer sides of the depot block.
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * laneEdge, z0 = bottomZ - hw, z1 = laneHead - 12f;
                Vector3 n = new Vector3(side, 0, 0);
                for (float z = z0; z > z1; z -= 0.5f)
                    Kerb(m, new Vector3(x, y, z), new Vector3(x, y, Mathf.Max(z - 0.5f, z1)), n, n, Mathf.RoundToInt((z0 - z) / 0.5f) % 2 == 0);
            }

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

        /// <summary>One kerb stone from a to b (on the road edge), built outward along na/nb.</summary>
        static void Kerb(MeshBuilder m, Vector3 a, Vector3 b, Vector3 na, Vector3 nb, bool stripe)
        {
            Color c = MeshBuilder.Fixed(stripe ? Palette.Kerb : Palette.Cream);
            Vector3 up = Vector3.up * CurbHeight;
            Vector3 a2 = a + na * CurbWidth, b2 = b + nb * CurbWidth;
            Vector3 inward = -(na + nb).normalized;
            // the road-facing side catches a little less light than the top
            m.Panel(a, a + up, b + up, b, inward, MeshBuilder.Fixed((stripe ? Palette.Kerb : Palette.Cream) * 0.88f));
            m.Panel(a + up, a2 + up, b2 + up, b + up, Vector3.up, c);
            m.Panel(a2, a2 + up, b2 + up, b2, -inward, c);
        }

        static Mesh Lot(RingPath ring, int cols, int rows)
        {
            var m = new MeshBuilder();
            Vector3 c = ring.Center;
            float w = ring.HalfW - RoadWidth * 0.5f - 0.15f, h = ring.HalfH - RoadWidth * 0.5f - 0.15f;
            // Raised plate with rounded corners.
            m.RoundedBox(c + new Vector3(0, (LotTop - 0.1f) * 0.5f, 0), new Vector3(w * 2f, LotTop + 0.1f, h * 2f), Mathf.Min(0.5f, ring.Radius - RoadWidth * 0.5f + 0.06f), 2, MeshBuilder.Fixed(Palette.Lot));
            Color lineC = MeshBuilder.Fixed(Palette.LotLine);
            float y = LotTop + 0.005f;
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
