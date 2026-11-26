using System;
using ItemCollector3D.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// The ONE settings panel, used by both the Main Menu and the Pause Menu (same prefab). Three sliders write straight into
    /// <see cref="GameSettings"/> (saved to PlayerPrefs on every change) and the sliders show the stored values when the panel opens.
    /// In the Main Menu only (<see cref="NewGameAvailable"/>) a NEW GAME button asks "START A NEW GAME?"; CANCEL changes nothing, START NEW GAME
    /// calls <see cref="ProgressService.StartNewGame"/> on the menu's own progress service (the one the Area Selection reads), which saves the
    /// fresh progress first. Settings (volumes, camera) and the chosen miner are never touched.
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

        [Header("NEW GAME (shown in the Main Menu only)")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private GameObject newGameDialog;
        [SerializeField] private TMP_Text newGameTitle;
        [SerializeField] private TMP_Text newGameBody;
        [SerializeField] private Button cancelNewGameButton;
        [SerializeField] private Button confirmNewGameButton;
        [Tooltip("The settings window itself (banner, sliders, BACK, NEW GAME): hidden while the NEW GAME question is open so nothing of it shows around the question.")]
        [SerializeField] private GameObject settingsWindow;
        [Tooltip("Empty = the scene's ProgressManager (the one the Area Selection uses).")]
        [SerializeField] private ProgressManager progress;

        public const string ConfirmTitle = "START A NEW GAME?";
        public const string ConfirmBody = "Your gold, collected treasures and area progress will be reset. This cannot be undone.";
        public const string DoneTitle = "NEW GAME STARTED";
        public const string DoneBody = "Your progress was reset. Good luck in Green Mine!";
        public const string ErrorTitle = "COULD NOT START";
        public const string ErrorBody = "Your save could not be changed. Nothing was reset.";

        /// <summary>Horizontal distance of BACK and NEW GAME from the centre when they stand side by side.</summary>
        public const float ButtonPairOffset = 290f;

        public enum NewGameState
        {
            Closed,
            Confirm,
            Done,
            Failed,
        }

        private bool newGameAvailable;
        private bool questionOnly; // opened straight from the Main Menu's NEW GAME button: leaving the question leaves the panel
        private bool listenersAdded;
        private bool refreshing; // changing a slider's min/max clamps its value and fires onValueChanged: ignore that

        public Slider MusicSlider => musicSlider;
        public Slider SfxSlider => sfxSlider;
        public Slider CameraSlider => cameraSlider;
        public bool IsOpen => gameObject.activeSelf;

        public Button NewGameButton => newGameButton;
        public Button CancelNewGameButton => cancelNewGameButton;
        public Button ConfirmNewGameButton => confirmNewGameButton;
        public GameObject NewGameDialog => newGameDialog;
        public NewGameState DialogState { get; private set; }
        public bool IsNewGameDialogOpen => DialogState != NewGameState.Closed;
        public string DialogTitle => newGameTitle != null ? newGameTitle.text : string.Empty;
        public string DialogBody => newGameBody != null ? newGameBody.text : string.Empty;

        /// <summary>Whether the NEW GAME button exists in this panel (the Main Menu turns it on; the Pause Menu never does).</summary>
        public bool NewGameAvailable
        {
            get => newGameAvailable;
            set
            {
                newGameAvailable = value;
                ApplyNewGameLayout();
            }
        }

        /// <summary>Raised after START NEW GAME saved the fresh progress.</summary>
        public event Action NewGameStarted;

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
            ApplyNewGameLayout();
            CloseNewGameDialog();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            questionOnly = false;
            CloseNewGameDialog();
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

            if (newGameButton != null)
            {
                newGameButton.onClick.AddListener(OpenNewGameDialog);
            }

            if (cancelNewGameButton != null)
            {
                cancelNewGameButton.onClick.AddListener(CancelNewGame);
            }

            if (confirmNewGameButton != null)
            {
                confirmNewGameButton.onClick.AddListener(ConfirmNewGame);
            }
        }

        public void Close()
        {
            if (IsNewGameDialogOpen)
            {
                // Escape / Back with the question open only closes the question (it never confirms it). Opened straight from the Main Menu there is no settings window behind it: back to the menu.
                CloseNewGameDialog();
                if (questionOnly)
                {
                    LeaveQuestionOnly();
                }

                return;
            }

            Hide();
            Closed?.Invoke();
        }

        /// <summary>
        /// The Main Menu's NEW GAME button: opens the panel with ONLY the question on screen (the settings window is not shown behind it). CANCEL, Back and the close paths return to the Main Menu; only
        /// START NEW GAME resets anything.
        /// </summary>
        public void ShowNewGameQuestion()
        {
            if (!newGameAvailable || newGameDialog == null)
            {
                return;
            }

            gameObject.SetActive(true);
            questionOnly = true;
            ShowDialog(NewGameState.Confirm);
        }

        private void LeaveQuestionOnly()
        {
            questionOnly = false;
            Hide();
            Closed?.Invoke();
        }

        // ------------------------------------------------------------------ NEW GAME

        private void ApplyNewGameLayout()
        {
            if (newGameButton != null)
            {
                newGameButton.gameObject.SetActive(newGameAvailable);
                var rect = (RectTransform)newGameButton.transform;
                rect.anchoredPosition = new Vector2(ButtonPairOffset, rect.anchoredPosition.y);
            }

            if (backButton != null)
            {
                var rect = (RectTransform)backButton.transform;
                rect.anchoredPosition = new Vector2(newGameAvailable && newGameButton != null ? -ButtonPairOffset : 0f, rect.anchoredPosition.y);
            }
        }

        /// <summary>NEW GAME pressed: only asks. Nothing is changed.</summary>
        public void OpenNewGameDialog()
        {
            if (!newGameAvailable || newGameDialog == null)
            {
                return;
            }

            ShowDialog(NewGameState.Confirm);
        }

        /// <summary>CANCEL: closes the question; no data is touched.</summary>
        public void CancelNewGame()
        {
            CloseNewGameDialog();
            if (questionOnly)
            {
                LeaveQuestionOnly();
            }
        }

        /// <summary>START NEW GAME (or OK once the result is shown).</summary>
        public void ConfirmNewGame()
        {
            switch (DialogState)
            {
                case NewGameState.Confirm:
                    // one step at a time: the state leaves Confirm before anything runs, so a double tap cannot reset twice
                    ProgressService service = ResolveService();
                    bool ok = service != null && service.StartNewGame();
                    ShowDialog(ok ? NewGameState.Done : NewGameState.Failed);
                    if (ok)
                    {
                        NewGameStarted?.Invoke();
                    }

                    break;
                case NewGameState.Done:
                case NewGameState.Failed:
                    CloseNewGameDialog();
                    if (questionOnly)
                    {
                        LeaveQuestionOnly();
                    }

                    break;
            }
        }

        private ProgressService ResolveService()
        {
            if (progress == null)
            {
                progress = FindAnyObjectByType<ProgressManager>();
            }

            if (progress == null)
            {
                return null;
            }

            progress.Initialize();
            return progress.Service;
        }

        private void ShowDialog(NewGameState state)
        {
            DialogState = state;
            if (newGameDialog != null)
            {
                newGameDialog.SetActive(state != NewGameState.Closed);
            }

            if (settingsWindow != null)
            {
                settingsWindow.SetActive(state == NewGameState.Closed); // nothing of the settings window peeks around the question
            }

            if (newGameTitle != null)
            {
                newGameTitle.text = state switch
                {
                    NewGameState.Done => DoneTitle,
                    NewGameState.Failed => ErrorTitle,
                    _ => ConfirmTitle,
                };
            }

            if (newGameBody != null)
            {
                newGameBody.text = state switch
                {
                    NewGameState.Done => DoneBody,
                    NewGameState.Failed => ErrorBody,
                    _ => ConfirmBody,
                };
            }

            bool confirming = state == NewGameState.Confirm;
            if (cancelNewGameButton != null)
            {
                cancelNewGameButton.gameObject.SetActive(confirming);
            }

            if (confirmNewGameButton != null)
            {
                TMP_Text label = confirmNewGameButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = confirming ? "START NEW GAME" : "OK";
                }

                var rect = (RectTransform)confirmNewGameButton.transform;
                rect.anchoredPosition = new Vector2(confirming ? ButtonPairOffset : 0f, rect.anchoredPosition.y);
            }
        }

        private void CloseNewGameDialog()
        {
            DialogState = NewGameState.Closed;
            if (newGameDialog != null)
            {
                newGameDialog.SetActive(false);
            }

            if (settingsWindow != null)
            {
                settingsWindow.SetActive(true);
            }
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
