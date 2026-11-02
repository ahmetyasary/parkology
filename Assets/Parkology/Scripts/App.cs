using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Parkology
{
    /// <summary>Entry point: builds camera, light, UI and runs the Splash → Home → Game flow.</summary>
    public sealed partial class App : MonoBehaviour
    {
        public static App Instance { get; private set; }
        /// <summary>Set by editor checks to jump straight into a level.</summary>
        public static bool FastBoot;

        public Camera Cam { get; private set; }
        Transform rig, world;
        RectTransform canvasRoot, safe, screenLayer, popupLayer;
        Wipe wipe;
        GameBoard board;
        public GameBoard Board => board;
        public event Action<int> CoinsChanged;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Input.multiTouchEnabled = false;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = 60f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 0;

            BuildWorld();
            BuildCanvas();
            Sfx.Create(transform);
            Fx.Create(transform);
            Haptics.Apply();
        }

        void Start() => StartCoroutine(Boot());

        void BuildWorld()
        {
            world = new GameObject("World Root").transform;
            world.SetParent(transform, false);

            rig = new GameObject("Camera Rig").transform;
            rig.SetParent(transform, false);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = 8f;
            Cam.nearClipPlane = 0.5f;
            Cam.farClipPlane = 120f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.Ground;
            Cam.allowMSAA = true;
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.intensity = 0.88f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.45f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.2f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.36f, 0.44f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.31f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.23f, 0.28f);
        }

        void BuildCanvas()
        {
            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            canvasRoot = (RectTransform)go.transform;
            screenLayer = UIKit.Stretch(canvasRoot, "Screen");
            safe = UIKit.Stretch(canvasRoot, "Safe Area");
            safe.gameObject.AddComponent<SafeArea>();
            popupLayer = UIKit.Stretch(canvasRoot, "Popups");

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
            wipe = Wipe.Create(transform);
        }

        void ClearScreen()
        {
            Tween.Stop("hud");
            foreach (Transform c in screenLayer) Destroy(c.gameObject);
            foreach (Transform c in safe) Destroy(c.gameObject);
            foreach (Transform c in popupLayer) Destroy(c.gameObject);
            if (board != null) Destroy(board.gameObject);
            board = null;
            Fx.Clear();
        }

        public void AddCoins(int amount)
        {
            Save.Coins += amount;
            CoinsChanged?.Invoke(Save.Coins);
        }

        public void Go(Action swap)
        {
            if (wipe.Busy) return;
            wipe.Play(swap);
        }

        IEnumerator Boot()
        {
            Loc.Load();
            if (FastBoot)
            {
                Kit.Build();
                foreach (var _ in Sfx.LoadAll()) { }
                NativeLaunch.Hide();
                StartLevel(Save.Level);
                yield break;
            }
            var splash = BuildSplash();
            yield return null;
            yield return null;
            NativeLaunch.Hide();
            yield return splash.Run(this);
            // New players drop straight into the warm-up levels; the home screen comes after them.
            if (Save.Onboarding) Go(() => StartLevel(Save.Level));
            else Go(ShowHome);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }
    }
}
