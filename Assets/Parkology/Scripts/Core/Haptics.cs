using System.Runtime.InteropServices;
using UnityEngine;

namespace Parkology
{
    /// <summary>Game feel cues. The numeric values are shared with PKHaptics.mm.</summary>
    public enum Haptic
    {
        Tap = 0,       // UI button, lane tap
        Hop = 1,       // a block jumps toward a train
        Snap = 2,      // a block clicks onto a wagon
        Complete = 3,  // a train is full
        Win = 4,       // level complete
        Fail = 5,      // road jammed
        Heavy = 6,     // hammer smash
        Warning = 7,   // blocked action
        Reveal = 8,    // mystery block shows its colour
        Soft = 9,      // landing / settle
    }

    public static class Haptics
    {
        static float nextLight;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PKHapticsEnable(int enabled);
        [DllImport("__Internal")] static extern void PKHapticsPlay(int kind);
#endif

        public static void Apply()
        {
#if UNITY_IOS && !UNITY_EDITOR
            PKHapticsEnable(Save.Vibration ? 1 : 0);
#endif
        }

        public static void Play(Haptic cue)
        {
            if (!Save.Vibration) return;
            // Rapid light cues are throttled so a burst of hops stays crisp instead of buzzing.
            bool light = cue == Haptic.Hop || cue == Haptic.Soft || cue == Haptic.Tap;
            if (light && Time.unscaledTime < nextLight) return;
            nextLight = Time.unscaledTime + 0.045f;
#if UNITY_IOS && !UNITY_EDITOR
            PKHapticsPlay((int)cue);
#elif UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(cue);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject decorView;
        static int sdk;

        // HapticFeedbackConstants; semantic feedback respects the device's own touch-feedback setting.
        static void PlayAndroid(Haptic cue)
        {
            try
            {
                if (decorView == null)
                {
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                        decorView = window.Call<AndroidJavaObject>("getDecorView");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        sdk = version.GetStatic<int>("SDK_INT");
                }
                int id;
                switch (cue)
                {
                    case Haptic.Tap: id = 3; break;                 // KEYBOARD_TAP
                    case Haptic.Hop: case Haptic.Soft: id = 4; break; // CLOCK_TICK
                    case Haptic.Snap: id = sdk >= 27 ? 7 : 1; break; // KEYBOARD_RELEASE / VIRTUAL_KEY
                    case Haptic.Complete: case Haptic.Win: case Haptic.Reveal: id = sdk >= 30 ? 16 : 1; break; // CONFIRM
                    case Haptic.Fail: case Haptic.Warning: id = sdk >= 30 ? 17 : 0; break; // REJECT / LONG_PRESS
                    default: id = 0; break; // LONG_PRESS (heavy)
                }
                var view = decorView;
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() => view.Call<bool>("performHapticFeedback", id)));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Haptics unavailable: " + e.Message);
            }
        }
#endif
    }
}
