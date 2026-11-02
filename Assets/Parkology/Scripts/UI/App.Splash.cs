using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Parkology
{
    public sealed partial class App
    {
        /// <summary>
        /// Branded loading screen: the same sky and logo as the native launch storyboard, then a little brick train
        /// chugs in along a track while the game really loads (meshes, faces, sounds, first level).
        /// </summary>
        sealed class Splash
        {
            public RectTransform root, logo, train, fill;
            public Text percent;
            public float barWidth;
            readonly List<RectTransform> cars = new List<RectTransform>();
            RectTransform chimney;

            public IEnumerator Run(App app)
            {
                float start = Time.unscaledTime;
                float shown = 0f, target = 0f;
                float trainX = -1400f;
                float puffTimer = 0f;
                // The logo is already where the native launch screen had it; give it a happy wobble.
                Tween.Delay(0.25f, () => Tween.Punch(logo, 0.14f, 0.7f));

                var steps = Load(app);
                bool loading = true;
                while (true)
                {
                    if (loading)
                    {
                        if (steps.MoveNext()) target = steps.Current;
                        else { loading = false; target = 1f; }
                    }
                    float minTime = Mathf.Clamp01((Time.unscaledTime - start) / 2.4f);
                    float goal = Mathf.Min(target, minTime);
                    shown = Mathf.MoveTowards(shown, goal, Time.unscaledDeltaTime * 1.4f);
                    fill.sizeDelta = new Vector2(Mathf.Max(70f, barWidth * shown), fill.sizeDelta.y);
                    percent.text = Mathf.RoundToInt(shown * 100f) + "%";

                    // the train rolls toward the middle as loading progresses
                    float aim = Mathf.Lerp(-900f, 80f, shown);
                    trainX = Mathf.Lerp(trainX, aim, Time.unscaledDeltaTime * 3f);
                    Animate(trainX, ref puffTimer, true);
                    if (!loading && shown >= 0.999f) break;
                    yield return null;
                }
                // toot and roll away
                Sfx.Play("toot", 0.8f);
                Haptics.Play(Haptic.Complete);
                float t = 0f;
                while (t < 0.9f)
                {
                    t += Time.unscaledDeltaTime;
                    trainX += Time.unscaledDeltaTime * Mathf.Lerp(200f, 2600f, t / 0.9f);
                    Animate(trainX, ref puffTimer, false);
                    yield return null;
                }
            }

            void Animate(float x, ref float puffTimer, bool chug)
            {
                train.anchoredPosition = new Vector2(x, train.anchoredPosition.y);
                float time = Time.unscaledTime;
                for (int i = 0; i < cars.Count; i++)
                {
                    float bob = Mathf.Abs(Mathf.Sin(time * 9f + i * 0.9f)) * 8f;
                    var c = cars[i];
                    c.anchoredPosition = new Vector2(c.anchoredPosition.x, bob);
                    c.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 9f + i * 0.9f) * 1.5f);
                }
                puffTimer -= Time.unscaledDeltaTime;
                if (puffTimer <= 0f)
                {
                    puffTimer = 0.32f;
                    Puff();
                    if (chug) Sfx.Play("chug", 0.25f, 1f, 0, 0.2f);
                }
            }

            void Puff()
            {
                var p = UIKit.Image(root, "dot", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
                p.rectTransform.position = chimney.position;
                p.color = new Color(1, 1, 1, 0.85f);
                var rt = p.rectTransform;
                Vector2 from = rt.anchoredPosition;
                Tween.Run(1.1f, k =>
                {
                    if (!rt) return;
                    rt.anchoredPosition = from + new Vector2(-120f * k, 160f * k);
                    rt.localScale = Vector3.one * (0.6f + k * 1.4f);
                    p.color = new Color(1, 1, 1, 0.85f * (1f - k));
                }, Ease.OutQuad, () => { if (rt) Object.Destroy(rt.gameObject); }, 0f, rt);
            }

            IEnumerator<float> Load(App app)
            {
                yield return 0.05f;
                Loc.Load();
                yield return 0.1f;
                var faces = FaceAtlas.Texture;
                yield return 0.25f;
                Kit.Build();
                yield return 0.4f;
                foreach (float k in Sfx.LoadAll()) yield return 0.4f + k * 0.4f;
                Sfx.ApplyMusic();
                var spec = LevelGenerator.Create(Save.Level);
                app.prepared = spec;
                yield return 0.92f;
                // warm the font atlas for both languages
                UIKit.Font.RequestCharactersInTexture("ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZabcçdefgğhıijklmnoöprsştuüvyz0123456789%+/!?.", 64);
                yield return 1f;
                _ = faces;
            }

            public void AddCar(RectTransform car, bool engine)
            {
                cars.Add(car);
                if (engine) chimney = (RectTransform)car.Find("Chimney");
            }
        }

        LevelSpec prepared;

        Splash BuildSplash()
        {
            var s = new Splash();
            s.root = UIKit.Stretch(screenLayer, "Splash");
            var bg = UIKit.Image(s.root, "splash-bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 2340));
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1080f / 2340f;
            bg.preserveAspect = false;

            // Same placement as the native launch storyboard: 89% of the width, centred at 32% from the top.
            var logoHolder = UIKit.Node(s.root, "Logo Holder", new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(960, 252));
            var logo = UIKit.Image(logoHolder, "logo", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 252));
            s.logo = logo.rectTransform;

            // Track + train
            var track = UIKit.Node(s.root, "Track", new Vector2(0.5f, 0.43f), Vector2.zero, new Vector2(1400, 60));
            for (int i = -4; i <= 4; i++) UIKit.Image(track, "rail", new Vector2(0.5f, 0.5f), new Vector2(i * 196f, -128f), new Vector2(200, 60));
            s.train = UIKit.Node(track, "Train", new Vector2(0.5f, 0.5f), new Vector2(-1400f, 0), new Vector2(10, 10));
            string[] wagons = { "train-wagon-blue", "train-wagon-yellow", "train-wagon-green", "train-wagon-purple" };
            float x = 0f;
            var engineHolder = UIKit.Node(s.train, "Engine", new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(10, 10));
            UIKit.Image(engineHolder, "train-engine", new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(336, 264));
            UIKit.Node(engineHolder, "Chimney", new Vector2(0.5f, 0.5f), new Vector2(70, 120), new Vector2(10, 10));
            s.AddCar(engineHolder, true);
            for (int i = 0; i < wagons.Length; i++)
            {
                x -= 252f;
                var holder = UIKit.Node(s.train, "Wagon", new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(10, 10));
                UIKit.Image(holder, wagons[i], new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(240, 240));
                s.AddCar(holder, false);
            }

            // Loading bar
            var bar = UIKit.Node(s.root, "Bar", new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(760, 74));
            UIKit.Image(bar, "bar-track", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 74), true);
            var fill = UIKit.Image(bar, "bar-fill", new Vector2(0f, 0.5f), new Vector2(8, 0), new Vector2(70, 58), true);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            s.fill = fill.rectTransform;
            s.barWidth = 744f;
            s.percent = UIKit.Text(bar, "0%", 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(300, 60), TextAnchor.MiddleCenter, UIKit.Ink, 4f, false);
            UIKit.Text(s.root, "loading", 52, Color.white, new Vector2(0.5f, 0.24f), new Vector2(0, 100), new Vector2(800, 80), TextAnchor.MiddleCenter, UIKit.Ink, 5f);
            UIKit.Text(s.root, "© Smartlogy", 30, new Color(1, 1, 1, 0.8f), new Vector2(0.5f, 0.04f), Vector2.zero, new Vector2(600, 50), TextAnchor.MiddleCenter, null, 0f, false);
            return s;
        }
    }
}
