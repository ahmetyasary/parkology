using System.Runtime.InteropServices;

namespace Parkology
{
    /// <summary>Releases the native iOS launch window once Unity has drawn the matching in-game splash.</summary>
    static class NativeLaunch
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PKHideLaunchScreen();
#endif

        public static void Hide()
        {
#if UNITY_IOS && !UNITY_EDITOR
            PKHideLaunchScreen();
#endif
        }
    }
}
