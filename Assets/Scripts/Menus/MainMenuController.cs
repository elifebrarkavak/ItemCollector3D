using System;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ItemCollector3D.Menus
{
    /// <summary>Main Menu: PLAY opens the gameplay scene, SETTINGS opens the shared settings panel, EXIT quits.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject buttonsRoot;
        [SerializeField] private SettingsPanelController settingsPanel;
        [SerializeField] private string gameplaySceneName = SceneNavigator.GameplayScene;

        /// <summary>Test hook: when set EXIT calls this instead of quitting the application.</summary>
        public static Action ExitHook;

        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.IsOpen;

        private void Awake()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(Play);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OpenSettings);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(Exit);
            }

            if (settingsPanel != null)
            {
                settingsPanel.Closed += ShowButtons;
                settingsPanel.Hide();
            }
        }

        private void OnDestroy()
        {
            if (settingsPanel != null)
            {
                settingsPanel.Closed -= ShowButtons;
            }
        }

        public void Play()
        {
            SceneNavigator.Load(gameplaySceneName);
        }

        public void OpenSettings()
        {
            if (settingsPanel == null)
            {
                return;
            }

            if (buttonsRoot != null)
            {
                buttonsRoot.SetActive(false);
            }

            settingsPanel.Show();
        }

        public void Exit()
        {
            if (ExitHook != null)
            {
                ExitHook();
                return;
            }

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowButtons()
        {
            if (buttonsRoot != null)
            {
                buttonsRoot.SetActive(true);
            }
        }
    }
}
