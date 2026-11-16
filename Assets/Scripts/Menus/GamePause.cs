using System;
using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Global pause: freezes gameplay by setting Time.timeScale to 0 (everything driven by deltaTime stops: player movement, dig timer,
    /// animations, particles) and pausing audio (a source with ignoreListenerPause, like the music, keeps playing). Systems that read
    /// input without deltaTime (the camera orbit) check <see cref="IsPaused"/>. Always restores the previous time scale on resume.
    /// </summary>
    public static class GamePause
    {
        private static float previousTimeScale = 1f;

        public static bool IsPaused { get; private set; }

        /// <summary>Raised with true when paused and false when resumed.</summary>
        public static event Action<bool> Changed;

        public static void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            IsPaused = true;
            Changed?.Invoke(true);
        }

        public static void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            Time.timeScale = previousTimeScale;
            AudioListener.pause = false;
            IsPaused = false;
            Changed?.Invoke(false);
        }
    }
}
