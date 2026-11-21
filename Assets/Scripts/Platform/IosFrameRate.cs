using UnityEngine;

namespace ItemCollector3D.Platform
{
    /// <summary>
    /// iOS only: asks for 60 frames per second. Unity does not request a frame rate on its own on a phone (nothing in the game sets one), so an iPhone would
    /// fall back to its default pacing instead of the smooth 60 fps the joystick and camera are tuned for. The iPhone 15 Pro Max can go to 120 Hz, but that
    /// also needs an Info.plist key and costs battery; 60 is the sensible test target. Compiled out on every other platform: Android, Standalone and the
    /// Editor behave exactly as before.
    /// </summary>
    public static class IosFrameRate
    {
        public const int TargetFrameRate = 60;

#if UNITY_IOS
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = TargetFrameRate;
        }
#endif
    }
}
