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

        /// <summary>
        /// True while a full-screen modal panel that is not the pause menu (the LEVEL COMPLETE panel, stage 17C) owns the screen. It pauses the game itself; this flag tells the Pause Menu not to react to the
        /// Android Back key meanwhile and to hide its PAUSE button.
        /// </summary>
        public static bool ModalOpen { get; private set; }

        /// <summary>Raised with true when paused and false when resumed.</summary>
        public static event Action<bool> Changed;

        /// <summary>Raised with true when a modal panel opened and false when it closed.</summary>
        public static event Action<bool> ModalChanged;

        public static void SetModalOpen(bool open)
        {
            if (ModalOpen == open)
            {
                return;
            }

            ModalOpen = open;
            ModalChanged?.Invoke(open);
        }

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
