using ItemCollector3D.Menus;
using UnityEngine;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// Hides a HUD element while the game is paused (<see cref="GamePause"/>), so the pause menu and the settings panel opened from it never
    /// show the HUD (gold header, joystick, detector) through or around them. Hiding is done with a CanvasGroup (alpha and raycasts), so
    /// the element keeps its own state, layout and active flags and comes back exactly as it was on resume.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class HideWhilePaused : MonoBehaviour
    {
        private CanvasGroup group;

        public bool IsHidden { get; private set; }

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            GamePause.Changed += Apply;
            Apply(GamePause.IsPaused);
        }

        private void OnDisable()
        {
            GamePause.Changed -= Apply;
        }

        private void Apply(bool paused)
        {
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            IsHidden = paused;
            group.alpha = paused ? 0f : 1f;
            group.blocksRaycasts = !paused;
            group.interactable = !paused;
        }
    }
}
