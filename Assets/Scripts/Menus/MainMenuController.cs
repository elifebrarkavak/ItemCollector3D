using System;
using ItemCollector3D.Characters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Main Menu: PLAY opens the Area Selection (which enters the gameplay scene), CHARACTER opens the miner selection, SETTINGS opens the shared
    /// settings panel, EXIT quits. The title and the button column give way while a sub screen is open; the Android Back key (Escape) closes it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button characterButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject buttonsRoot;
        [SerializeField] private GameObject titleRoot;
        [SerializeField] private SettingsPanelController settingsPanel;
        [SerializeField] private CharacterSelectController characterPanel;
        [SerializeField] private AreaSelectController areaPanel;

        /// <summary>Test hook: when set EXIT calls this instead of quitting the application.</summary>
        public static Action ExitHook;

        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.IsOpen;
        public bool IsCharacterSelectOpen => characterPanel != null && characterPanel.IsOpen;
        public bool IsAreaSelectOpen => areaPanel != null && areaPanel.IsOpen;

        private void Awake()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(Play);
            }

            if (characterButton != null)
            {
                characterButton.onClick.AddListener(OpenCharacterSelect);
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
                settingsPanel.Closed += ShowHome;
                settingsPanel.Hide();
            }

            if (characterPanel != null)
            {
                characterPanel.Closed += ShowHome;
                characterPanel.Hide();
            }

            if (areaPanel != null)
            {
                areaPanel.Closed += ShowHome;
                areaPanel.Hide();
            }
        }

        private void OnDestroy()
        {
            if (settingsPanel != null)
            {
                settingsPanel.Closed -= ShowHome;
            }

            if (characterPanel != null)
            {
                characterPanel.Closed -= ShowHome;
            }

            if (areaPanel != null)
            {
                areaPanel.Closed -= ShowHome;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (IsSettingsOpen)
                {
                    settingsPanel.Close();
                }
                else if (IsCharacterSelectOpen)
                {
                    characterPanel.Cancel();
                }
                else if (IsAreaSelectOpen)
                {
                    areaPanel.Cancel();
                }
            }
        }

        /// <summary>PLAY: opens the Area Selection (it no longer loads the gameplay scene directly).</summary>
        public void Play()
        {
            OpenAreaSelect();
        }

        public void OpenAreaSelect()
        {
            if (areaPanel == null)
            {
                return;
            }

            SetHomeVisible(false);
            areaPanel.Open();
        }

        public void OpenSettings()
        {
            if (settingsPanel == null)
            {
                return;
            }

            SetHomeVisible(false);
            settingsPanel.Show();
        }

        public void OpenCharacterSelect()
        {
            if (characterPanel == null)
            {
                return;
            }

            SetHomeVisible(false);
            characterPanel.Open();
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

        private void ShowHome()
        {
            SetHomeVisible(true);
        }

        private void SetHomeVisible(bool visible)
        {
            if (buttonsRoot != null)
            {
                buttonsRoot.SetActive(visible);
            }

            if (titleRoot != null)
            {
                titleRoot.SetActive(visible);
            }
        }
    }
}
