using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// In-game pause: a PAUSE button, and a panel with RESUME, SETTINGS (the same shared settings panel as the Main Menu) and MAIN MENU.
    /// Pausing freezes gameplay through <see cref="GamePause"/>; the Android Back key (Escape) toggles it. The game is always resumed
    /// when leaving (main menu) or when this object is destroyed, so no scene ever starts frozen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private SettingsPanelController settingsPanel;
        [SerializeField] private string mainMenuSceneName = SceneNavigator.MainMenuScene;

        public bool IsPaused => GamePause.IsPaused;
        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.IsOpen;

        private void Awake()
        {
            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(Pause);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(Resume);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OpenSettings);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(GoToMainMenu);
            }

            if (settingsPanel != null)
            {
                settingsPanel.Closed += ShowPausePanel;
                settingsPanel.Hide();
            }

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            GamePause.ModalChanged += HandleModalChanged;
            HandleModalChanged(GamePause.ModalOpen);
        }

        private void OnDisable()
        {
            GamePause.ModalChanged -= HandleModalChanged;
        }

        private void OnDestroy()
        {
            if (settingsPanel != null)
            {
                settingsPanel.Closed -= ShowPausePanel;
            }

            GamePause.Resume(); // never leave the next scene frozen
        }

        /// <summary>A modal panel (LEVEL COMPLETE) owns the screen: no PAUSE button underneath it; it comes back when the panel is closed.</summary>
        private void HandleModalChanged(bool open)
        {
            if (pauseButton == null)
            {
                return;
            }

            pauseButton.gameObject.SetActive(!open && !IsPaused); // the panel resumes the game BEFORE it reports that it closed
        }

        private void Update()
        {
            if (GamePause.ModalOpen)
            {
                return; // the Android Back key belongs to the modal panel
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (IsSettingsOpen)
                {
                    settingsPanel.Close();
                }
                else if (IsPaused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }
        }

        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            GamePause.Pause();
            if (pauseButton != null)
            {
                pauseButton.gameObject.SetActive(false);
            }

            ShowPausePanel();
        }

        public void Resume()
        {
            if (settingsPanel != null)
            {
                settingsPanel.Hide();
            }

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            if (pauseButton != null)
            {
                pauseButton.gameObject.SetActive(true);
            }

            GamePause.Resume();
        }

        public void OpenSettings()
        {
            if (settingsPanel == null)
            {
                return;
            }

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            settingsPanel.Show();
        }

        public void GoToMainMenu()
        {
            SceneNavigator.Load(mainMenuSceneName); // resumes first
        }

        private void ShowPausePanel()
        {
            if (pausePanel != null && IsPaused)
            {
                pausePanel.SetActive(true);
            }
        }
    }
}
