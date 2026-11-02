using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Parkology
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float InBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f || t >= 1f) return t;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }

        public static float OutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
            if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }

    /// <summary>Coroutine-based tweens. Tweens bound to an object stop when it is destroyed.</summary>
    public sealed class Tween : MonoBehaviour
    {
        static Tween runner;
        readonly Dictionary<object, Coroutine> keyed = new Dictionary<object, Coroutine>();

        static Tween Runner
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("Tweens");
                    DontDestroyOnLoad(go);
                    runner = go.AddComponent<Tween>();
                }
                return runner;
            }
        }

        /// <summary>Runs step(eased t) for duration seconds. A key replaces any running tween with the same key.</summary>
        public static Coroutine Run(float duration, Action<float> step, Func<float, float> ease = null, Action done = null,
            float delay = 0f, UnityEngine.Object owner = null, object key = null, bool unscaled = true)
        {
            var r = Runner;
            if (key != null) Stop(key);
            var c = r.StartCoroutine(r.Play(duration, step, ease ?? Ease.OutCubic, done, delay, owner, owner != null, key, unscaled));
            if (key != null && c != null) r.keyed[key] = c;
            return c;
        }

        public static void Stop(object key)
        {
            if (runner == null || key == null) return;
            if (runner.keyed.TryGetValue(key, out var c))
            {
                if (c != null) runner.StopCoroutine(c);
                runner.keyed.Remove(key);
            }
        }

        public static Coroutine Delay(float seconds, Action action, UnityEngine.Object owner = null, bool unscaled = true) =>
            Run(0f, null, null, action, seconds, owner, null, unscaled);

        IEnumerator Play(float duration, Action<float> step, Func<float, float> ease, Action done, float delay,
            UnityEngine.Object owner, bool hasOwner, object key, bool unscaled)
        {
            while (delay > 0f)
            {
                delay -= unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
                if (hasOwner && owner == null) yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                if (hasOwner && owner == null) yield break;
                step?.Invoke(ease(Mathf.Clamp01(t / duration)));
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            if (hasOwner && owner == null) yield break;
            step?.Invoke(ease(1f));
            if (key != null) keyed.Remove(key);
            done?.Invoke();
        }

        // Convenience helpers -------------------------------------------------

        public static Coroutine Scale(Transform t, Vector3 from, Vector3 to, float duration, Func<float, float> ease = null, float delay = 0f, Action done = null)
        {
            if (delay > 0f) t.localScale = from;
            return Run(duration, k => { if (t) t.localScale = Vector3.LerpUnclamped(from, to, k); }, ease, done, delay, t, t);
        }

        public static Coroutine Move(RectTransform t, Vector2 from, Vector2 to, float duration, Func<float, float> ease = null, float delay = 0f, Action done = null)
        {
            if (delay > 0f) t.anchoredPosition = from;
            return Run(duration, k => { if (t) t.anchoredPosition = Vector2.LerpUnclamped(from, to, k); }, ease, done, delay, t, ("move", t));
        }

        public static Coroutine Fade(CanvasGroup g, float from, float to, float duration, float delay = 0f, Action done = null)
        {
            if (delay > 0f) g.alpha = from;
            return Run(duration, k => { if (g) g.alpha = Mathf.LerpUnclamped(from, to, k); }, Ease.Linear, done, delay, g, g);
        }

        /// <summary>A quick squash-and-stretch "boing" around the current scale.</summary>
        public static void Punch(Transform t, float amount = 0.18f, float duration = 0.35f)
        {
            if (!t) return;
            // Re-punching mid-animation must not bake the squashed scale in as the new rest scale.
            if (!punchRest.TryGetValue(t, out Vector3 baseScale)) punchRest[t] = baseScale = t.localScale;
            Run(duration, k =>
            {
                if (!t) return;
                float s = Mathf.Sin(k * Mathf.PI * 2.5f) * (1f - k) * amount;
                t.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z * (1f + s));
            }, Ease.Linear, () => { if (t) t.localScale = baseScale; punchRest.Remove(t); }, 0f, t, ("punch", t));
        }

        static readonly Dictionary<Transform, Vector3> punchRest = new Dictionary<Transform, Vector3>();
    }
}
