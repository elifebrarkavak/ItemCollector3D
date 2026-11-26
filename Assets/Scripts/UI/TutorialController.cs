using System;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// The Green Mine tutorial (stage 17B): five short steps taught during real play, MOVE, LOOK AROUND, FIND TREASURE, DIG, COLLECT. It only watches the existing systems (the player's position, the
    /// camera's orbit input, the <see cref="TreasureDetector"/> signal, the <see cref="TreasureHuntController"/> dig / collect events) and never replaces them: DIG and COLLECT are the normal buttons, the
    /// gold is paid by the normal collect. A step is finished by what the player DID (walked 3 m, turned the camera 30 degrees with their own drag, the detector reached RAPID, a dig finished,
    /// a treasure was collected), never by a tap on the tutorial itself. If the player does something later in the order first (digs or collects early) the tutorial moves on with them, so it can never
    /// lock them; SKIP TUTORIAL is always on screen. It runs only when the save says <see cref="TutorialStatus.Pending"/> (set by NEW GAME), only in Green Mine, and records
    /// <see cref="TutorialStatus.Finished"/> when it is completed or skipped (it never starts again until the next NEW GAME). It is hidden while the game is paused, takes no touches except its own button,
    /// and does no per-frame searching: the nearest-treasure arrow refreshes 4 times a second.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialController : MonoBehaviour
    {
        public enum TutorialStep
        {
            Inactive = 0,
            Move = 1,
            Look = 2,
            Find = 3,
            Dig = 4,
            Collect = 5,
            Complete = 6,
        }

        public const int StepCount = 5;
        public const float MoveMetres = 3f;
        public const float LookDegrees = 30f;
        public const float CompleteShowSeconds = 2.6f;

        /// <summary>The detector step is shown at least this long (the first treasure can be right next to the start: the player should still read what the signal means).</summary>
        public const float FindMinimumSeconds = 3f;
        public const string GreenMineAreaId = "green_mine";

        public static readonly string[] Titles =
        {
            string.Empty,
            "USE THE JOYSTICK TO MOVE",
            "SWIPE TO LOOK AROUND",
            "FOLLOW THE DETECTOR SIGNAL",
            "TAP DIG TO REVEAL THE TREASURE",
            "TAP COLLECT TO EARN GOLD",
            "TUTORIAL COMPLETE!",
        };

        public static readonly string[] Hints =
        {
            string.Empty,
            "Push the stick on the left and walk around.",
            "Drag on the right side of the screen.",
            "SLOW, FAST, RAPID: it beeps faster the closer you get.",
            "Stand close until DIG appears.",
            "The treasure is revealed. Take it!",
            "Well done, treasure hunter!",
        };

        [SerializeField] private ProgressManager progress;
        [SerializeField] private TreasureHuntController hunt;
        [SerializeField] private TreasureDetector detector;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private HudController hud;
        [SerializeField] private RectTransform joystickRect;
        [SerializeField] private RectTransform detectorRect;

        [Header("UI (built into the HUD by the editor tool)")]
        [SerializeField] private GameObject uiRoot;
        [SerializeField] private CanvasGroup plateGroup;
        [SerializeField] private TMP_Text stepLabel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private RectTransform highlight;
        [SerializeField] private Image highlightImage;
        [SerializeField] private RectTransform swipeHint;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private TMP_Text arrowLabel;

        private Transform player;
        private Vector3 lastPlayerPosition;
        private float movedMetres, lookedDegrees, lastYaw;
        private float completeUntil;
        private float stepStarted;
        private float nextArrow;
        private TreasureSpot guided;
        private bool started;
        private bool listening;

        public TutorialStep Step { get; private set; }

        /// <summary>The step number shown to the player (1 .. 5; 0 when not running, 5 while completing).</summary>
        public int StepNumber => Step == TutorialStep.Inactive ? 0 : Mathf.Min((int)Step, StepCount);

        public bool IsRunning => Step != TutorialStep.Inactive;

        public Button SkipButton => skipButton;
        public GameObject UiRoot => uiRoot;
        public RectTransform Highlight => highlight;
        public RectTransform SwipeHint => swipeHint;
        public RectTransform Arrow => arrow;
        public string CurrentTitle => titleLabel != null ? titleLabel.text : string.Empty;
        public string CurrentHint => hintLabel != null ? hintLabel.text : string.Empty;
        public float MovedMetres => movedMetres;
        public float LookedDegrees => lookedDegrees;
        public TreasureSpot GuidedTreasure => guided;

        /// <summary>Raised when the step changes (the new step).</summary>
        public event Action<TutorialStep> StepChanged;

        /// <summary>Raised when the tutorial ended: true = skipped, false = completed.</summary>
        public event Action<bool> Finished;

        private void Awake()
        {
            if (progress == null)
            {
                progress = FindAnyObjectByType<ProgressManager>();
            }

            if (hunt == null)
            {
                hunt = FindAnyObjectByType<TreasureHuntController>();
            }

            if (detector == null)
            {
                detector = FindAnyObjectByType<TreasureDetector>();
            }

            if (cameraController == null)
            {
                cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            }

            if (hud == null)
            {
                hud = FindAnyObjectByType<HudController>();
            }

            if (skipButton != null)
            {
                skipButton.onClick.AddListener(Skip);
            }

            if (uiRoot != null)
            {
                uiRoot.SetActive(false);
            }
        }

        private void Start()
        {
            TryStart();
        }

        private void OnDisable()
        {
            Unlisten();
        }

        /// <summary>Starts the tutorial when the save asks for it: Pending, Green Mine, area open. Safe to call again.</summary>
        public bool TryStart()
        {
            if (started || progress == null)
            {
                return false;
            }

            progress.Initialize();
            ProgressService service = progress.Service;
            if (service == null || service.Tutorial != TutorialStatus.Pending || hunt == null || hunt.AreaId != GreenMineAreaId || !hunt.IsAreaAccessible)
            {
                return false;
            }

            started = true;
            cameraController = cameraController != null ? cameraController : FindAnyObjectByType<ThirdPersonCameraController>();
            player = cameraController != null ? cameraController.Target : null;
            Listen();
            if (uiRoot != null)
            {
                uiRoot.SetActive(true);
            }

            GoTo(TutorialStep.Move);
            return true;
        }

        private void Listen()
        {
            if (listening || hunt == null)
            {
                return;
            }

            listening = true;
            hunt.DigCompleted += HandleDigCompleted;
            hunt.TreasureCollected += HandleCollected;
        }

        private void Unlisten()
        {
            if (!listening || hunt == null)
            {
                listening = false;
                return;
            }

            listening = false;
            hunt.DigCompleted -= HandleDigCompleted;
            hunt.TreasureCollected -= HandleCollected;
        }

        // ------------------------------------------------------------------ steps

        private void GoTo(TutorialStep step)
        {
            Step = step;
            stepStarted = Time.unscaledTime;
            movedMetres = 0f;
            lookedDegrees = 0f;
            if (player == null && cameraController != null)
            {
                player = cameraController.Target;
            }

            if (player != null)
            {
                lastPlayerPosition = player.position;
            }

            if (cameraController != null)
            {
                lastYaw = cameraController.Yaw;
            }

            if (stepLabel != null)
            {
                stepLabel.text = step == TutorialStep.Complete ? string.Empty : $"STEP {(int)step} / {StepCount}";
            }

            if (titleLabel != null)
            {
                titleLabel.text = Titles[(int)step];
            }

            if (hintLabel != null)
            {
                hintLabel.text = Hints[(int)step];
            }

            if (swipeHint != null)
            {
                swipeHint.gameObject.SetActive(step == TutorialStep.Look);
            }

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(step != TutorialStep.Complete);
            }

            if (step == TutorialStep.Find)
            {
                nextArrow = 0f;
                guided = null;
            }
            else
            {
                guided = null;
                if (arrow != null)
                {
                    arrow.parent.gameObject.SetActive(false);
                }
            }

            if (step == TutorialStep.Complete)
            {
                completeUntil = Time.unscaledTime + CompleteShowSeconds;
            }

            UpdateHighlight();
            StepChanged?.Invoke(step);
        }

        private void HandleDigCompleted(TreasureSpot spot)
        {
            // a treasure was revealed: whatever step the player was on, COLLECT is next
            if (IsRunning && Step != TutorialStep.Complete && Step < TutorialStep.Collect)
            {
                GoTo(TutorialStep.Collect);
            }
        }

        private void HandleCollected(TreasureSpot spot, int gold)
        {
            if (IsRunning && Step != TutorialStep.Complete)
            {
                Finish(false);
            }
        }

        /// <summary>SKIP TUTORIAL: closes the tutorial for good (until the next NEW GAME). The game goes on exactly as it was.</summary>
        public void Skip()
        {
            if (IsRunning && Step != TutorialStep.Complete)
            {
                Finish(true);
            }
        }

        private void Finish(bool skipped)
        {
            RecordFinished();
            Unlisten();
            if (skipped)
            {
                Step = TutorialStep.Inactive;
                HideAll();
            }
            else
            {
                GoTo(TutorialStep.Complete); // "TUTORIAL COMPLETE!" for a moment, then it disappears
            }

            Finished?.Invoke(skipped);
        }

        private void RecordFinished()
        {
            if (progress != null && progress.Service != null)
            {
                progress.Service.SetTutorial(TutorialStatus.Finished);
            }
        }

        private void HideAll()
        {
            guided = null;
            if (uiRoot != null)
            {
                uiRoot.SetActive(false);
            }

            if (highlight != null)
            {
                highlight.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ per frame (cheap)

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            bool paused = GamePause.IsPaused;
            if (plateGroup != null)
            {
                plateGroup.alpha = paused ? 0f : 1f; // nothing of the tutorial shows behind the pause menu
            }

            if (paused)
            {
                if (highlight != null && highlight.gameObject.activeSelf)
                {
                    highlight.gameObject.SetActive(false); // the halo lives outside the plate: hide it with the rest
                }

                return;
            }

            if (Step == TutorialStep.Complete)
            {
                if (Time.unscaledTime >= completeUntil)
                {
                    Step = TutorialStep.Inactive;
                    HideAll();
                }

                return;
            }

            switch (Step)
            {
                case TutorialStep.Move:
                    TrackMovement();
                    if (movedMetres >= MoveMetres)
                    {
                        GoTo(TutorialStep.Look);
                    }

                    break;
                case TutorialStep.Look:
                    TrackLook();
                    if (lookedDegrees >= LookDegrees)
                    {
                        GoTo(TutorialStep.Find);
                    }

                    break;
                case TutorialStep.Find:
                    UpdateGuide();
                    if (Time.unscaledTime - stepStarted >= FindMinimumSeconds && detector != null && detector.HasSignal && detector.Proximity >= DetectorSignalIndicator.RapidFromProximity)
                    {
                        GoTo(TutorialStep.Dig);
                    }

                    break;
                case TutorialStep.Dig:
                    // standing close but DIG is not offered yet: say what to do; the existing hint already says "GET CLOSER TO DIG"
                    if (hintLabel != null)
                    {
                        string wanted = hud != null && hud.DigButtonRect != null && hud.DigButtonRect.gameObject.activeInHierarchy ? "DIG is ready. Tap it!" : Hints[(int)TutorialStep.Dig];
                        if (hintLabel.text != wanted)
                        {
                            hintLabel.text = wanted;
                        }
                    }

                    break;
            }

            UpdateHighlight();
        }

        private void TrackMovement()
        {
            if (player == null)
            {
                player = cameraController != null ? cameraController.Target : null;
                if (player != null)
                {
                    lastPlayerPosition = player.position;
                }

                return;
            }

            Vector3 now = player.position;
            Vector3 delta = now - lastPlayerPosition;
            delta.y = 0f;
            lastPlayerPosition = now;
            if (delta.sqrMagnitude < 4f) // a step of the walk, not a teleport (2 m in one frame)
            {
                movedMetres += delta.magnitude;
            }
        }

        private void TrackLook()
        {
            if (cameraController == null)
            {
                return;
            }

            float yaw = cameraController.Yaw;
            if (cameraController.OrbitInputActive) // the player's own drag, not the camera swinging itself in a narrow place
            {
                lookedDegrees += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
            }

            lastYaw = yaw;
        }

        /// <summary>The nearest hidden treasure (refreshed 4 times a second) and an arrow towards it on the screen, until the detector reaches RAPID.</summary>
        private void UpdateGuide()
        {
            if (arrow == null || Time.unscaledTime < nextArrow || player == null || cameraController == null)
            {
                return;
            }

            nextArrow = Time.unscaledTime + 0.25f;
            guided = null;
            float best = float.MaxValue;
            foreach (TreasureSpot spot in TreasureSpot.Active)
            {
                if (spot.State != TreasureState.Hidden)
                {
                    continue;
                }

                Vector3 d = spot.transform.position - player.position;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    guided = spot;
                }
            }

            bool show = guided != null && detector != null && !(detector.HasSignal && detector.Proximity >= DetectorSignalIndicator.RapidFromProximity);
            arrow.parent.gameObject.SetActive(show); // the guide: the arrow and its distance
            if (!show)
            {
                return;
            }

            Vector3 toTarget = guided.transform.position - player.position;
            toTarget.y = 0f;
            Vector3 forward = cameraController.transform.forward;
            forward.y = 0f;
            float angle = Vector3.SignedAngle(forward.normalized, toTarget.normalized, Vector3.up); // + = to the right of the view
            arrow.localRotation = Quaternion.Euler(0f, 0f, -angle + 90f); // the arrow sprite points up at 0
            if (arrowLabel != null)
            {
                arrowLabel.text = Mathf.RoundToInt(Mathf.Sqrt(best)) + " m";
            }
        }

        // ------------------------------------------------------------------ highlight

        private RectTransform TargetOfStep()
        {
            switch (Step)
            {
                case TutorialStep.Move:
                    return joystickRect != null ? joystickRect : (joystickRect = FindRect<VirtualJoystick>());
                case TutorialStep.Find:
                    return detectorRect != null ? detectorRect : (detectorRect = FindRect<DetectorSignalIndicator>());
                case TutorialStep.Dig:
                    return hud != null ? hud.DigButtonRect : null;
                case TutorialStep.Collect:
                    return hud != null ? hud.CollectButtonRect : null;
                default:
                    return null;
            }
        }

        private static RectTransform FindRect<T>() where T : Component
        {
            T found = FindAnyObjectByType<T>();
            return found != null ? found.transform as RectTransform : null;
        }

        private void UpdateHighlight()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            RectTransform target = TargetOfStep();
            bool show = highlight != null && target != null && target.gameObject.activeInHierarchy && !GamePause.IsPaused;
            if (highlight != null && highlight.gameObject.activeSelf != show)
            {
                highlight.gameObject.SetActive(show);
            }

            if (show)
            {
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                Vector3 centre = (corners[0] + corners[2]) * 0.5f;
                highlight.position = centre;
                float scaleToHighlight = target.lossyScale.x / Mathf.Max(1e-4f, highlight.lossyScale.x);
                highlight.sizeDelta = new Vector2(target.rect.width, target.rect.height) * scaleToHighlight * (1.35f + 0.12f * pulse);
                if (highlightImage != null)
                {
                    Color c = highlightImage.color;
                    c.a = 0.55f + 0.4f * pulse;
                    highlightImage.color = c;
                }
            }

            if (swipeHint != null && swipeHint.gameObject.activeSelf)
            {
                swipeHint.localScale = Vector3.one * (1f + 0.05f * pulse);
            }
        }
    }
}
