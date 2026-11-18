using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// The ONE settings panel, used by both the Main Menu and the Pause Menu (same prefab). Three sliders write straight into
    /// <see cref="GameSettings"/> (saved to PlayerPrefs on every change) and the sliders show the stored values when the panel opens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPanelController : MonoBehaviour
    {
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider cameraSlider;
        [SerializeField] private TMP_Text musicValueLabel;
        [SerializeField] private TMP_Text sfxValueLabel;
        [SerializeField] private TMP_Text cameraValueLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private Button closeButton;

        private bool listenersAdded;
        private bool refreshing; // changing a slider's min/max clamps its value and fires onValueChanged: ignore that

        public Slider MusicSlider => musicSlider;
        public Slider SfxSlider => sfxSlider;
        public Slider CameraSlider => cameraSlider;
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Raised when the player presses BACK (the owner decides what to show next).</summary>
        public event Action Closed;

        private void Awake()
        {
            EnsureListeners();
        }

        private void OnEnable()
        {
            EnsureListeners();
            Refresh();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Shows the stored values without triggering a change.</summary>
        public void Refresh()
        {
            refreshing = true;
            try
            {
                RefreshSliders();
            }
            finally
            {
                refreshing = false;
            }

            RefreshLabels();
        }

        private void RefreshSliders()
        {
            GameSettings settings = GameSettings.Current;
            if (musicSlider != null)
            {
                musicSlider.minValue = 0f;
                musicSlider.maxValue = 1f;
                musicSlider.SetValueWithoutNotify(settings.MusicVolume);
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.SetValueWithoutNotify(settings.SfxVolume);
            }

            if (cameraSlider != null)
            {
                cameraSlider.minValue = GameSettings.MinCameraSensitivity;
                cameraSlider.maxValue = GameSettings.MaxCameraSensitivity;
                cameraSlider.SetValueWithoutNotify(settings.CameraSensitivity);
            }
        }

        private void EnsureListeners()
        {
            if (listenersAdded)
            {
                return;
            }

            listenersAdded = true;
            if (musicSlider != null)
            {
                musicSlider.onValueChanged.AddListener(value => { if (!refreshing) { GameSettings.Current.SetMusicVolume(value); RefreshLabels(); } });
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.AddListener(value => { if (!refreshing) { GameSettings.Current.SetSfxVolume(value); RefreshLabels(); } });
            }

            if (cameraSlider != null)
            {
                cameraSlider.onValueChanged.AddListener(value => { if (!refreshing) { GameSettings.Current.SetCameraSensitivity(value); RefreshLabels(); } });
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(Close);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
        }

        public void Close()
        {
            Hide();
            Closed?.Invoke();
        }

        private void RefreshLabels()
        {
            GameSettings settings = GameSettings.Current;
            if (musicValueLabel != null)
            {
                musicValueLabel.text = $"{Mathf.RoundToInt(settings.MusicVolume * 100f)}%";
            }

            if (sfxValueLabel != null)
            {
                sfxValueLabel.text = $"{Mathf.RoundToInt(settings.SfxVolume * 100f)}%";
            }

            if (cameraValueLabel != null)
            {
                cameraValueLabel.text = $"{settings.CameraSensitivity:F2}x";
            }
        }
    }
}
