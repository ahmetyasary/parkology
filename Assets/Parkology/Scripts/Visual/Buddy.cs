using UnityEngine;

namespace Parkology
{
    /// <summary>A toy brick character (block, locomotive or wagon): colour, expressions, blinking and squash.</summary>
    public sealed class Buddy : MonoBehaviour
    {
        public Transform Body { get; private set; }
        public int ColorIndex { get; private set; } = -1;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        Face restFace = Face.Happy;
        Face shown = Face.Happy;
        float faceUntil, nextBlink, flash;
        bool hasFace;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RectId = Shader.PropertyToID("_DecalRect");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int AngleId = Shader.PropertyToID("_DecalAngle");
        float faceAngle = float.NaN;

        public static Buddy Create(Mesh mesh, Transform parent, int colorIndex, string name, bool hasFace = true)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = body.AddComponent<MeshRenderer>();
            r.sharedMaterial = Kit.Plastic;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            var b = root.AddComponent<Buddy>();
            b.Body = body.transform;
            b.meshRenderer = r;
            b.block = new MaterialPropertyBlock();
            b.hasFace = hasFace;
            b.nextBlink = Time.time + Random.Range(1f, 4f);
            b.SetColor(colorIndex);
            b.SetRestFace(Face.Happy);
            return b;
        }

        public Color Tint { get; private set; }

        public void SetColor(int index)
        {
            ColorIndex = index;
            Tint = Palette.Block(index);
            Apply();
        }

        public void SetColor(Color c)
        {
            Tint = c;
            Apply();
        }

        public void SetRestFace(Face f)
        {
            restFace = f;
            if (Time.time >= faceUntil) Show(f);
        }

        /// <summary>Shows an expression; with seconds &gt; 0 it returns to the resting face afterwards.</summary>
        public void Express(Face f, float seconds)
        {
            faceUntil = Time.time + seconds;
            Show(f);
        }

        void Show(Face f)
        {
            shown = f;
            Apply();
        }

        public void Flash(float amount = 0.6f) => flash = Mathf.Max(flash, amount);

        public void Squash(float amount = 0.2f) => Tween.Punch(Body, amount, 0.38f);

        void Apply()
        {
            if (block == null) return;
            block.SetColor(ColorId, Tint);
            block.SetVector(RectId, hasFace ? FaceAtlas.Rect(shown) : new Vector4(0, 0, 0, 0));
            block.SetFloat(FlashId, flash);
            block.SetFloat(AngleId, float.IsNaN(faceAngle) ? 0f : faceAngle);
            meshRenderer.SetPropertyBlock(block);
        }

        // The face is painted on top of the block; counter-rotate it by the block's heading so it reads upright.
        void LateUpdate()
        {
            if (!hasFace || Body == null) return;
            float angle = -Body.eulerAngles.y * Mathf.Deg2Rad;
            if (!float.IsNaN(faceAngle) && Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, faceAngle * Mathf.Rad2Deg)) < 0.5f) return;
            faceAngle = angle;
            Apply();
        }

        void Update()
        {
            bool dirty = false;
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - Time.deltaTime * 3f);
                dirty = true;
            }
            if (hasFace)
            {
                float now = Time.time;
                if (shown != restFace && now >= faceUntil)
                {
                    shown = restFace;
                    dirty = true;
                }
                if (restFace == Face.Happy && now >= nextBlink && now >= faceUntil)
                {
                    Express(Face.Blink, 0.13f);
                    nextBlink = now + Random.Range(2.2f, 5.5f);
                    return;
                }
            }
            if (dirty) Apply();
        }
    }
}
