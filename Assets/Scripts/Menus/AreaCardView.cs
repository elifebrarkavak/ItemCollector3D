using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// One big card of the Area Selection: preview picture, name, lock / coming-soon / complete markers, gold progress and one action button.
    /// It only SHOWS an <see cref="AreaCardState"/> (the controller decides what a tap does) and animates a little: the focused card is outlined and glows (all cards keep one size and one row),
    /// the progress bar fills, a tap on a closed card shakes it. Unscaled time, so it also works while the game is paused.
    /// The three action buttons (PLAY / COMING SOON / LOCKED) share one spot; exactly one is shown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AreaCardView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Data")]
        [Tooltip("Id from the AreaCatalog.")]
        [SerializeField] private string areaId = "green_mine";
        [Tooltip("Scene this area loads. Empty = the area has no map yet and shows COMING SOON.")]
        [SerializeField] private string sceneName = "";

        [Header("Content")]
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("The big line: UNLOCKED or LOCKED.")]
        [SerializeField] private TMP_Text statusLabel;
        [Tooltip("Under the bar: TREASURES: x / n.")]
        [SerializeField] private TMP_Text treasuresLabel;
        [Tooltip("When the map opens, in TOTAL gold: UNLOCKS AT 500 TOTAL GOLD.")]
        [SerializeField] private TMP_Text unlockLabel;
        [Tooltip("The check mark (open) and the padlock (closed) in front of the status line: a shape, not only a colour.")]
        [SerializeField] private Color unlockedStatusColor = new Color(0.65f, 0.94f, 0.8f, 1f);
        [SerializeField] private Color lockedStatusColor = new Color(0.78f, 0.82f, 0.95f, 1f);
        [SerializeField] private GameObject statusCheck;
        [SerializeField] private GameObject statusLockIcon;
        [SerializeField] private Image progressFill;
        [SerializeField] private Image previewImage;
        [Tooltip("Pictures that fade (and get a cool tint) while the area is locked: the frame, the preview, the name banner.")]
        [SerializeField] private Image[] fadeImages = Array.Empty<Image>();
        [SerializeField] private Color lockedTint = new Color(0.62f, 0.66f, 0.8f, 1f);
        [SerializeField] private Color lockedPreviewTint = new Color(0.52f, 0.57f, 0.7f, 1f);

        [Header("State markers")]
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private RectTransform lockBadge;
        [SerializeField] private GameObject comingSoonRibbon;
        [SerializeField] private GameObject completeChip;
        [SerializeField] private GameObject unlockedChip;

        [Header("Buttons (one shown)")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button comingSoonButton;
        [SerializeField] private Button lockedButton;

        [Header("Feedback")]
        [SerializeField] private Image focusGlow;
        [Tooltip("The gold outline of the focused card: a shape that tells the focused card apart without relying on colour.")]
        [SerializeField] private Image focusRing;
        [Tooltip("All three cards keep ONE size and ONE row; the focused one is only outlined and glows.")]
        [SerializeField] private float focusedScale = 1f;
        [SerializeField] private float idleScale = 1f;

        private RectTransform rect;
        private AreaCardState state;
        private bool hasState;
        private bool focused;
        private float scale = 1f;
        private float glow;
        private float displayedFill;
        private float deniedTime = -1f;
        private Vector2 restPosition;
        private bool restCaptured;

        public string AreaId => areaId;
        public string SceneName => sceneName;
        public AreaCardState State => state;
        public bool IsFocused => focused;

        /// <summary>The fill the bar is heading for (0..1). The shown fill follows it over a short moment.</summary>
        public float TargetFill { get; private set; }

        public float DisplayedFill => displayedFill;
        public bool IsDenyAnimationPlaying => deniedTime >= 0f;

        public Button PlayButton => playButton;
        public Button ComingSoonButton => comingSoonButton;
        public Button LockedButton => lockedButton;
        public TMP_Text StatusLabel => statusLabel;
        public TMP_Text TreasuresLabel => treasuresLabel;
        public TMP_Text UnlockLabel => unlockLabel;
        public bool StatusCheckVisible => statusCheck != null && statusCheck.activeSelf;
        public bool StatusLockVisible => statusLockIcon != null && statusLockIcon.activeSelf;
        public bool RingVisible => focusRing != null && focusRing.color.a > 0.5f;
        public bool LockVisible => lockOverlay != null && lockOverlay.activeSelf;
        public bool ComingSoonVisible => comingSoonRibbon != null && comingSoonRibbon.activeSelf;
        public bool CompleteVisible => completeChip != null && completeChip.activeSelf;
        public bool UnlockedVisible => unlockedChip != null && unlockedChip.activeSelf;

        /// <summary>Raised when the card body is tapped (not its button).</summary>
        public event Action<AreaCardView> BodyTapped;

        /// <summary>Raised by whichever action button is tapped.</summary>
        public event Action<AreaCardView> ActionTapped;

        public void Configure(string id, string scene)
        {
            areaId = id;
            sceneName = scene ?? "";
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            if (playButton != null) { playButton.onClick.AddListener(RaiseAction); }
            if (comingSoonButton != null) { comingSoonButton.onClick.AddListener(RaiseAction); }
            if (lockedButton != null) { lockedButton.onClick.AddListener(RaiseAction); }
        }

        private void OnEnable()
        {
            CaptureRest();
            deniedTime = -1f;
            scale = focused ? focusedScale : idleScale;
            displayedFill = 0f; // the bar fills up each time the screen opens
            Apply();
        }

        private void OnDisable()
        {
            deniedTime = -1f;
            if (rect != null && restCaptured)
            {
                rect.anchoredPosition = restPosition;
            }
        }

        private void CaptureRest()
        {
            if (rect == null)
            {
                rect = (RectTransform)transform;
            }

            if (!restCaptured)
            {
                restPosition = rect.anchoredPosition;
                restCaptured = true;
            }
        }

        private void RaiseAction()
        {
            ActionTapped?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            BodyTapped?.Invoke(this);
        }

        /// <summary>Shows the state: which marker and which button, the faded look of a locked card, the numbers and the bar target.</summary>
        public void SetState(AreaCardState newState)
        {
            state = newState;
            hasState = true;
            TargetFill = newState.Progress01;

            bool locked = newState.Availability == AreaAvailability.Locked;
            bool soon = newState.Availability == AreaAvailability.ComingSoon;
            bool playable = newState.Availability == AreaAvailability.Playable;

            if (nameLabel != null) { nameLabel.text = newState.DisplayName; }
            if (statusLabel != null) { statusLabel.text = newState.StatusLine; statusLabel.color = locked ? lockedStatusColor : unlockedStatusColor; }
            if (treasuresLabel != null) { treasuresLabel.text = newState.TreasureText; }
            if (unlockLabel != null) { unlockLabel.text = newState.UnlockText; }
            SetActive(statusCheck, !locked);
            SetActive(statusLockIcon, locked);

            SetActive(lockOverlay, locked);
            SetActive(comingSoonRibbon, soon);
            SetActive(completeChip, newState.ShowsCompleted);
            SetActive(unlockedChip, soon);
            if (playButton != null) { playButton.gameObject.SetActive(playable); }
            if (comingSoonButton != null) { comingSoonButton.gameObject.SetActive(soon); }
            if (lockedButton != null) { lockedButton.gameObject.SetActive(locked); }

            // locked: everything on the card goes cool and dim, but the numbers stay fully readable
            foreach (Image image in fadeImages)
            {
                if (image != null)
                {
                    image.color = locked ? lockedTint : Color.white;
                }
            }

            if (previewImage != null)
            {
                previewImage.color = locked ? lockedPreviewTint : Color.white;
            }

            if (progressFill != null && !isActiveAndEnabled)
            {
                progressFill.fillAmount = 0f;
            }

            Apply();
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        public void SetFocused(bool value)
        {
            focused = value;
        }

        /// <summary>A tap on a closed card (locked or coming soon): the card shakes and the lock badge wiggles.</summary>
        public void PlayDenied()
        {
            deniedTime = 0f;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            float goalScale = focused ? focusedScale : idleScale;
            scale = Mathf.Lerp(scale, goalScale, 1f - Mathf.Exp(-14f * dt));
            float goalGlow = focused && hasState && !state.IsLocked ? 0.85f + Mathf.Sin(Time.unscaledTime * 2.4f) * 0.15f : 0f;
            glow = Mathf.Lerp(glow, goalGlow, 1f - Mathf.Exp(-10f * dt));
            displayedFill = Mathf.Lerp(displayedFill, TargetFill, 1f - Mathf.Exp(-4.5f * dt));
            if (Mathf.Abs(displayedFill - TargetFill) < 0.002f)
            {
                displayedFill = TargetFill;
            }

            Vector2 shake = Vector2.zero;
            float wiggle = 0f;
            if (deniedTime >= 0f)
            {
                deniedTime += dt;
                const float duration = 0.42f;
                float k = 1f - Mathf.Clamp01(deniedTime / duration);
                shake.x = Mathf.Sin(deniedTime * 58f) * 15f * k * k;
                wiggle = Mathf.Sin(deniedTime * 46f) * 16f * k;
                if (deniedTime >= duration)
                {
                    deniedTime = -1f;
                }
            }

            if (rect != null)
            {
                rect.anchoredPosition = restPosition + shake;
                rect.localScale = new Vector3(scale, scale, 1f);
            }

            if (lockBadge != null)
            {
                lockBadge.localRotation = Quaternion.Euler(0f, 0f, wiggle);
            }

            Apply();
        }

        private void Apply()
        {
            if (focusGlow != null)
            {
                Color c = focusGlow.color;
                c.a = glow;
                focusGlow.color = c;
            }

            if (focusRing != null)
            {
                Color c = focusRing.color;
                c.a = Mathf.Clamp01(glow * 1.2f);
                focusRing.color = c;
            }

            if (progressFill != null)
            {
                progressFill.fillAmount = displayedFill;
            }
        }
    }
}
