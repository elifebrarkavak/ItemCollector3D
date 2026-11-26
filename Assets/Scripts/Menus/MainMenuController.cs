using System;
using ItemCollector3D.Characters;
using ItemCollector3D.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Main Menu: the big button opens the Area Selection (CONTINUE GAME, when a game is saved) or starts the first adventure (START ADVENTURE, on a real first launch: no save file at all), CHARACTER
    /// opens the miner selection, SETTINGS opens the shared settings panel, NEW GAME (only when a game is saved) asks the safe NEW GAME question, EXIT quits. The title and the button column give way while a
    /// sub screen is open; the Android Back key (Escape) closes it.
    /// <para>
    /// First adventure (stage 18B): START ADVENTURE -> the three introduction pages -> CHARACTER SELECTION -> AREA SELECTION -> Green Mine -> the five step tutorial. Nothing is written on START: only when
    /// the introduction is finished or skipped is the save created (the same fresh game as NEW GAME, with the tutorial pending), so closing the app in the middle of the introduction is still a first
    /// launch and shows it again. A save that exists, even with 0 gold, is never a first launch and is never pushed into the introduction or the tutorial.
    /// </para>
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
        [Tooltip("The three-page introduction shown right after a successful NEW GAME (stage 17A) and on a first launch (18B).")]
        [SerializeField] private IntroSlidesController introPanel;

        [Header("Continue / New game (stage 18B)")]
        [Tooltip("Empty = the ProgressManager of this prefab.")]
        [SerializeField] private ProgressManager progress;
        [SerializeField] private TMP_Text playLabel;
        [Tooltip("The small line under the big button: 655 GOLD · 2 AREAS UNLOCKED (only when a game is saved).")]
        [SerializeField] private TMP_Text playSummary;
        [SerializeField] private Button newGameButton;

        public const string StartText = "START ADVENTURE";
        public const string ContinueText = "CONTINUE GAME";

        private bool firstAdventure;      // START ADVENTURE was pressed: the save is created when the introduction ends
        private bool continueToAreas;     // the introduction led to the miner selection: SELECT goes on to the Area Selection

        /// <summary>Test hook: when set EXIT calls this instead of quitting the application.</summary>
        public static Action ExitHook;

        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.IsOpen;
        public bool IsCharacterSelectOpen => characterPanel != null && characterPanel.IsOpen;
        public bool IsAreaSelectOpen => areaPanel != null && areaPanel.IsOpen;
        public bool IsIntroOpen => introPanel != null && introPanel.IsOpen;

        /// <summary>True on a real first launch: no saved game exists (a save with 0 gold is a game in progress, not a first launch).</summary>
        public bool IsFirstRun => Service != null && !Service.HasSave;

        public Button PlayButton => playButton;
        public Button NewGameButton => newGameButton;
        public Button ExitButton => exitButton;
        public string PlayLabelText => playLabel != null ? playLabel.text : string.Empty;
        public string PlaySummaryText => playSummary != null && playSummary.gameObject.activeSelf ? playSummary.text : string.Empty;
        public bool NewGameVisible => newGameButton != null && newGameButton.gameObject.activeSelf;
        public bool IsFirstAdventureRunning => firstAdventure;

        private ProgressService Service
        {
            get
            {
                progress = progress != null ? progress : GetComponent<ProgressManager>();
                if (progress == null)
                {
                    return null;
                }

                progress.Initialize();
                return progress.Service;
            }
        }

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

            if (newGameButton != null)
            {
                newGameButton.onClick.AddListener(OpenNewGameQuestion);
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
                characterPanel.Closed += HandleCharacterClosed;
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

        private void Start()
        {
            RefreshHome();
            if (SceneNavigator.ConsumeAreaSelectionRequest())
            {
                OpenAreaSelect(); // coming back from a finished level: straight to the Area Selection (BACK still leads to the Main Menu)
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
                characterPanel.Closed -= HandleCharacterClosed;
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

        /// <summary>
        /// CONTINUE GAME: opens the Area Selection (it never loads the gameplay scene directly). START ADVENTURE (a real first launch, no saved game): opens the introduction first; nothing is saved yet.
        /// </summary>
        public void Play()
        {
            if (IsFirstRun)
            {
                firstAdventure = true;
                ShowIntro();
                return;
            }

            OpenAreaSelect();
        }

        /// <summary>
        /// NEW GAME (Main Menu, only when a game is saved): only ASKS. The question is the existing confirmation dialog of the Settings panel; the save is reset only after the player confirms it there.
        /// </summary>
        public void OpenNewGameQuestion()
        {
            if (settingsPanel == null || !NewGameVisible)
            {
                return;
            }

            SetHomeVisible(false);
            settingsPanel.NewGameAvailable = true;
            settingsPanel.ShowNewGameQuestion();
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

            if (firstAdventure)
            {
                // the first adventure starts for real now: the save file is created (a fresh game with the Green Mine tutorial pending, exactly like NEW GAME). SKIP and LET'S PLAY are the same here.
                firstAdventure = false;
                ProgressService service = Service;
                if (service != null && !service.HasSave && !service.StartNewGame())
                {
                    ProgressLog.Warning("The first adventure could not be saved: the introduction was skipped without a tutorial.");
                }
            }

            continueToAreas = true;
            OpenCharacterSelect();
        }

        /// <summary>SELECT on the miner screen that followed the introduction goes on to the Area Selection; BACK, or the CHARACTER button, return to the Main Menu as always.</summary>
        private void HandleCharacterClosed()
        {
            bool goOn = continueToAreas && characterPanel != null && characterPanel.ClosedByConfirm;
            continueToAreas = false;
            if (goOn)
            {
                OpenAreaSelect();
            }
            else
            {
                ShowHome();
            }
        }

        /// <summary>The Android Back key during the introduction: close it and return to the Main Menu (no reset, nothing else changes).</summary>
        public void CloseIntro()
        {
            firstAdventure = false; // closed in the middle: nothing was saved, so a first launch stays a first launch
            continueToAreas = false;
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
            RefreshHome();
            SetHomeVisible(true);
        }

        /// <summary>Re-reads the save: the big button says START ADVENTURE or CONTINUE GAME (with the real total gold and the open areas), NEW GAME appears next to EXIT only for a saved game.</summary>
        public void RefreshHome()
        {
            ProgressService service = Service;
            bool has = service != null && service.HasSave;
            if (playLabel != null)
            {
                playLabel.text = has ? ContinueText : StartText;
                var labelRect = (RectTransform)playLabel.transform;
                labelRect.offsetMin = new Vector2(labelRect.offsetMin.x, has ? 66f : 12f); // a saved game leaves room for the summary line under the label
            }

            if (playSummary != null)
            {
                playSummary.gameObject.SetActive(has);
                if (has)
                {
                    playSummary.text = SummaryText(service.TotalGold, service.UnlockedAreaCount);
                }
            }

            if (newGameButton != null)
            {
                newGameButton.gameObject.SetActive(has);
            }
        }

        /// <summary>"655 GOLD · 2 AREAS UNLOCKED": the player's total gold (the whole game) and how many maps are open.</summary>
        public static string SummaryText(int totalGold, int unlockedAreas)
        {
            return $"{Mathf.Max(0, totalGold)} GOLD \u00B7 {unlockedAreas} {(unlockedAreas == 1 ? "AREA" : "AREAS")} UNLOCKED";
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
