using System.Collections.Generic;
using UnityEngine;

namespace Parkology
{
    /// <summary>Lightweight 3D particles (brick debris, confetti, sparkles) and camera shake.</summary>
    public sealed class Fx : MonoBehaviour
    {
        sealed class Bit
        {
            public Transform t;
            public MeshRenderer r;
            public Vector3 v, spin;
            public float life, max, size, gravity;
        }

        static Fx instance;
        readonly List<Bit> live = new List<Bit>();
        readonly Stack<Bit> pool = new Stack<Bit>();
        MaterialPropertyBlock block;
        float shake;
        Transform shakeTarget;
        Vector3 shakeBase;

        public static void Create(Transform parent)
        {
            if (instance) return;
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<Fx>();
            instance.block = new MaterialPropertyBlock();
        }

        Bit Get(Color c)
        {
            Bit b;
            if (pool.Count > 0) b = pool.Pop();
            else
            {
                var go = new GameObject("Bit");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Kit.Debris;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Kit.Plastic;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b = new Bit { t = go.transform, r = r };
            }
            b.t.gameObject.SetActive(true);
            block.SetColor("_Color", c);
            block.SetVector("_DecalRect", Vector4.zero);
            block.SetFloat("_Flash", 0f);
            b.r.SetPropertyBlock(block);
            live.Add(b);
            return b;
        }

        /// <summary>Brick shatters into little studded pieces.</summary>
        public static void Burst(Vector3 at, Color c, int count = 14, float power = 3.2f)
        {
            if (!instance) return;
            for (int i = 0; i < count; i++)
            {
                var b = instance.Get(Random.value < 0.25f ? Color.Lerp(c, Color.white, 0.4f) : c);
                b.t.position = at + Random.insideUnitSphere * 0.25f;
                b.t.rotation = Random.rotation;
                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.6f;
                b.v = dir.normalized * Random.Range(power * 0.5f, power);
                b.spin = Random.insideUnitSphere * 720f;
                b.life = b.max = Random.Range(0.6f, 0.95f);
                b.size = Random.Range(0.7f, 1.3f);
                b.gravity = 14f;
            }
        }

        /// <summary>Small white star-bits that pop upward and fade (sparkle / reveal).</summary>
        public static void Sparkle(Vector3 at, int count = 8, Color? tint = null)
        {
            if (!instance) return;
            for (int i = 0; i < count; i++)
            {
                var b = instance.Get(tint ?? new Color(1f, 0.97f, 0.8f));
                b.t.position = at;
                b.t.rotation = Random.rotation;
                float a = i * Mathf.PI * 2f / count;
                b.v = new Vector3(Mathf.Cos(a), 1.2f, Mathf.Sin(a)) * Random.Range(1.4f, 2.4f);
                b.spin = Random.insideUnitSphere * 400f;
                b.life = b.max = Random.Range(0.45f, 0.65f);
                b.size = Random.Range(0.45f, 0.7f);
                b.gravity = 3f;
            }
        }

        /// <summary>Colourful bricks rain over an area (level complete).</summary>
        public static void Confetti(Vector3 center, float width, float height, int count = 70)
        {
            if (!instance) return;
            for (int i = 0; i < count; i++)
            {
                var b = instance.Get(Palette.Blocks[i % Palette.Blocks.Length]);
                b.t.position = center + new Vector3(Random.Range(-width, width) * 0.5f, Random.Range(5f, 9f), Random.Range(-height, height) * 0.5f);
                b.t.rotation = Random.rotation;
                b.v = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-1f, 1f), Random.Range(-0.6f, 0.6f));
                b.spin = Random.insideUnitSphere * 540f;
                b.life = b.max = Random.Range(1.8f, 2.6f);
                b.size = Random.Range(0.9f, 1.5f);
                b.gravity = 5f;
            }
        }

        public static void Shake(Transform cameraRig, float amount)
        {
            if (!instance || cameraRig == null) return;
            if (instance.shake <= 0f || instance.shakeTarget != cameraRig)
            {
                instance.shakeTarget = cameraRig;
                instance.shakeBase = cameraRig.localPosition;
            }
            instance.shake = Mathf.Max(instance.shake, amount);
        }

        public static void Clear()
        {
            if (!instance) return;
            foreach (var b in instance.live)
            {
                b.t.gameObject.SetActive(false);
                instance.pool.Push(b);
            }
            instance.live.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var b = live[i];
                b.life -= dt;
                if (b.life <= 0f)
                {
                    b.t.gameObject.SetActive(false);
                    live.RemoveAt(i);
                    pool.Push(b);
                    continue;
                }
                b.v.y -= b.gravity * dt;
                b.t.position += b.v * dt;
                if (b.t.position.y < 0.05f && b.v.y < 0f)
                {
                    b.t.position = new Vector3(b.t.position.x, 0.05f, b.t.position.z);
                    b.v = new Vector3(b.v.x * 0.6f, -b.v.y * 0.35f, b.v.z * 0.6f);
                }
                b.t.Rotate(b.spin * dt, Space.Self);
                float k = b.life / b.max;
                b.t.localScale = Vector3.one * b.size * Mathf.Clamp01(k * 3f);
            }

            if (shakeTarget != null)
            {
                if (shake > 0f)
                {
                    shake = Mathf.Max(0f, shake - dt * 1.6f);
                    shakeTarget.localPosition = shakeBase + (Vector3)(Random.insideUnitCircle * shake * 0.35f);
                }
                else
                {
                    shakeTarget.localPosition = shakeBase;
                    shakeTarget = null;
                }
            }
        }
    }
}
