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
        [Tooltip("The three-page introduction shown right after a successful NEW GAME (stage 17A).")]
        [SerializeField] private IntroSlidesController introPanel;

        /// <summary>Test hook: when set EXIT calls this instead of quitting the application.</summary>
        public static Action ExitHook;

        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.IsOpen;
        public bool IsCharacterSelectOpen => characterPanel != null && characterPanel.IsOpen;
        public bool IsAreaSelectOpen => areaPanel != null && areaPanel.IsOpen;
        public bool IsIntroOpen => introPanel != null && introPanel.IsOpen;

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
                settingsPanel.NewGameAvailable = true; // the NEW GAME button lives in the Main Menu's Settings only
                settingsPanel.Closed += ShowHome;
                settingsPanel.NewGameStarted += ShowIntro;
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

            if (introPanel != null)
            {
                introPanel.Finished += HandleIntroFinished;
                introPanel.Hide();
            }
        }

        private void OnDestroy()
        {
            if (settingsPanel != null)
            {
                settingsPanel.Closed -= ShowHome;
                settingsPanel.NewGameStarted -= ShowIntro;
            }

            if (introPanel != null)
            {
                introPanel.Finished -= HandleIntroFinished;
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
                if (IsIntroOpen)
                {
                    CloseIntro(); // the Android Back key closes the introduction and returns to the Main Menu
                }
                else if (IsSettingsOpen)
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

        /// <summary>NEW GAME succeeded (the save is already fresh): the introduction comes first, on top of everything.</summary>
        private void ShowIntro()
        {
            if (introPanel != null)
            {
                introPanel.Open();
            }
        }

        /// <summary>
        /// SKIP or LET'S PLAY: on to the existing Character Selection (the Settings panel and its "NEW GAME STARTED" note are closed). The reset already happened once, in the NEW GAME dialog:
        /// nothing here touches the save, the settings or the miner.
        /// </summary>
        private void HandleIntroFinished(bool skipped)
        {
            introPanel.Hide();
            if (settingsPanel != null)
            {
                settingsPanel.Hide();
            }

            OpenCharacterSelect();
        }

        /// <summary>The Android Back key during the introduction: close it and return to the Main Menu (no reset, nothing else changes).</summary>
        public void CloseIntro()
        {
            if (introPanel != null)
            {
                introPanel.Hide();
            }

            if (settingsPanel != null)
            {
                settingsPanel.Hide();
            }

            ShowHome();
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
