using System;
using ItemCollector3D.Audio;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// LEVEL COMPLETE (stage 17C): the celebration shown when EVERY treasure of Green Mine (15) or Rocky Canyon (20) has been collected. Three choices: GO TO the next area, STAY HERE, AREA SELECTION.
    /// <para>
    /// It is NOT the gold goal. "Complete" here means "every treasure id of this scene is in the save" (checked by id against <see cref="ProgressService.IsCollected"/>), so reaching 500 gold, which only
    /// unlocks Rocky Canyon, never opens it. It is also event driven: it is only ever STARTED by <see cref="TreasureHuntController.TreasureCollected"/>, so a save that already has every treasure (an older game
    /// from before 17C) never shows it on loading. The moment the last treasure is collected the celebration is recorded as PENDING in the save (<see cref="ProgressService.MarkCompletionPending"/>); it is
    /// recorded as celebrated, and never shown again until NEW GAME, only when the player answers it (STAY HERE, GO TO or AREA SELECTION, <see cref="ProgressService.MarkCompletionCelebrated"/>). If the player
    /// leaves the scene or the app before that (while it waits for the reward, or with the panel open), the pending record survives and the area shows the celebration once, a moment after the next time it is entered.
    /// </para>
    /// <para>
    /// After the last COLLECT it waits (a handful of boolean reads per frame, only while waiting) until the collect animation, the floating reward text, the AREA COMPLETE / NEW AREA UNLOCKED banners and a running
    /// tutorial are finished, plus a short settle, so nothing is cut off. Then it pauses the game (<see cref="GamePause"/>: the HUD hides, the joystick, the camera and DIG / COLLECT receive nothing; the music keeps
    /// playing) and shows the panel; its full-screen dim also takes every touch. The Android Back key does STAY HERE. The panel adds no scene change infrastructure: GO TO uses <see cref="SceneNavigator"/> after the
    /// same Gold lock check as the Area Selection, AREA SELECTION uses <see cref="SceneNavigator.LoadAreaSelection"/>. Ancient Mine (the last area, stage 17D: GAME COMPLETE) is not handled here.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelCompleteController : MonoBehaviour
    {
        public enum Phase { Idle, Waiting, Open }

        public enum GoResult { NotOpen, Entered, Locked, NoMap }

        /// <summary>Quiet time (no animation, text or banner running) required before the panel opens.</summary>
        public const float SettleSeconds = 0.4f;

        /// <summary>The wait is given up after this long (real time, not counting pause) so a stuck banner can never swallow the celebration.</summary>
        public const float MaxWaitSeconds = 10f;

        /// <summary>A pending celebration of an area that is entered again waits this long first (the scene fades in and the player sees the place before the panel covers it).</summary>
        public const float EntrySeconds = 1f;

        public const float EntranceSeconds = 0.55f;
        public const float JingleVolume = 0.55f;
        public const string TitleText = "LEVEL COMPLETE!";
        public const string StayText = "STAY HERE";
        public const string AreaSelectionText = "AREA SELECTION";

        [Serializable]
        public sealed class AreaStyle
        {
            public string areaId;
            public Sprite picture;
            [Tooltip("The area's colour: the name line, the picture frame, the halo.")]
            public Color accent = Color.white;
            public Color accentLight = Color.white;
        }

        /// <summary>The numbers of the treasures of this scene, by id.</summary>
        public readonly struct AreaSummary
        {
            public AreaSummary(int total, int collected, int areaGold, int collectedGold)
            {
                Total = total;
                Collected = collected;
                AreaGold = areaGold;
                CollectedGold = collectedGold;
            }

            public int Total { get; }
            public int Collected { get; }

            /// <summary>The gold of all treasures of the area (what the area can pay).</summary>
            public int AreaGold { get; }

            /// <summary>The gold of the treasures collected so far (equals <see cref="AreaGold"/> when complete).</summary>
            public int CollectedGold { get; }

            public bool IsComplete => Total > 0 && Collected >= Total;
        }

        [Header("Scene (found in the scene when empty)")]
        [SerializeField] private ProgressManager progress;
        [SerializeField] private TreasureHuntController hunt;
        [SerializeField] private FloatingRewardText rewardText;
        [SerializeField] private ProgressNotificationPresenter notifications;
        [SerializeField] private TutorialController tutorial;
        [SerializeField] private FinalCelebrationController finals;

        [Header("Panel (built by LevelCompleteUiBuilder)")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private RectTransform window;
        [SerializeField] private CanvasGroup windowGroup;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text areaLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text totalLabel;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private Image picture;
        [SerializeField] private Image frame;
        [SerializeField] private Image divider;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button stayButton;
        [SerializeField] private Button areaSelectionButton;
        [SerializeField] private AreaStyle[] styles = Array.Empty<AreaStyle>();

        [Header("Celebration (a handful of images, no particle system)")]
        [SerializeField] private RectTransform trophy;
        [SerializeField] private Image halo;
        [SerializeField] private RectTransform[] burst = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform[] twinkles = Array.Empty<RectTransform>();

        private ProgressService service;
        private TreasureSpot lastSpot;
        private bool subscribed;
        private int waitFrame;
        private float quiet;
        private float waited;
        private float earliestOpenAt;
        private string openedAreaId;
        private float openedAt;
        private float deniedAt = -1f;
        private Vector2 nextRest;
        private bool nextRestCaptured;
        private AudioSource source;
        private AudioClip jingle;
        private Color accentColor = Color.white;

        public Phase CurrentPhase { get; private set; }
        public bool IsOpen => CurrentPhase == Phase.Open;
        public bool IsWaiting => CurrentPhase == Phase.Waiting;

        /// <summary>How many times the panel opened in this scene (it can be once per area and save).</summary>
        public int ShowCount { get; private set; }

        public AreaSummary LastSummary { get; private set; }
        public string NextAreaId { get; private set; }
        public int JinglePlayCount { get; private set; }
        public float LastJingleVolume { get; private set; }

        public Button NextButton => nextButton;
        public Button StayButton => stayButton;
        public Button AreaSelectionButton => areaSelectionButton;
        public GameObject PanelRoot => panelRoot;
        public RectTransform Window => window;
        public string TitleString => titleLabel != null ? titleLabel.text : string.Empty;
        public string AreaString => areaLabel != null ? areaLabel.text : string.Empty;
        public string CountString => countLabel != null ? countLabel.text : string.Empty;
        public string GoldString => goldLabel != null ? goldLabel.text : string.Empty;
        public string TotalString => totalLabel != null ? totalLabel.text : string.Empty;
        public string NextString => nextLabel != null ? nextLabel.text : string.Empty;
        public Image Picture => picture;
        public Color AccentColor => accentColor;
        public RectTransform Trophy => trophy;

        /// <summary>Raised when the panel opened (the area id).</summary>
        public event Action<string> Opened;

        /// <summary>Raised when the panel closed for STAY HERE or the Back key.</summary>
        public event Action Stayed;

        private void Awake()
        {
            progress = progress != null ? progress : FindInScene<ProgressManager>();
            hunt = hunt != null ? hunt : FindInScene<TreasureHuntController>();
            rewardText = rewardText != null ? rewardText : FindInScene<FloatingRewardText>();
            notifications = notifications != null ? notifications : FindInScene<ProgressNotificationPresenter>();
            tutorial = tutorial != null ? tutorial : FindInScene<TutorialController>();
            finals = finals != null ? finals : FindInScene<FinalCelebrationController>();

            if (nextButton != null) { nextButton.onClick.AddListener(OnNextTapped); }
            if (stayButton != null) { stayButton.onClick.AddListener(Stay); }
            if (areaSelectionButton != null) { areaSelectionButton.onClick.AddListener(OpenAreaSelection); }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Start()
        {
            if (progress != null)
            {
                progress.Initialize();
                service = progress.Service;
            }

            if (hunt != null && !subscribed)
            {
                hunt.TreasureCollected += HandleCollected;
                subscribed = true;
            }

            ResumePending();
        }

        /// <summary>
        /// The area was entered again with a celebration still owed (the player left before it opened, or closed the app with the panel open, without answering it): wait for the scene to settle, then show it.
        /// Only a PENDING record starts this, never a merely complete save, so older games that were finished before 17C stay quiet.
        /// </summary>
        private void ResumePending()
        {
            ProgressService s = Service;
            string areaId = hunt != null ? hunt.AreaId : null;
            if (CurrentPhase != Phase.Idle || s == null || string.IsNullOrEmpty(areaId) || !s.IsCompletionPending(areaId)
                || s.IsCompletionCelebrated(areaId) || FollowingArea(s, areaId) == null || !hunt.IsAreaAccessible || !Summarize().IsComplete)
            {
                return;
            }

            lastSpot = null;
            waitFrame = Time.frameCount;
            quiet = 0f;
            waited = 0f;
            earliestOpenAt = Time.unscaledTime + EntrySeconds;
            CurrentPhase = Phase.Waiting;
        }

        private void OnDestroy()
        {
            if (hunt != null && subscribed)
            {
                hunt.TreasureCollected -= HandleCollected;
            }

            subscribed = false;
            if (CurrentPhase == Phase.Open)
            {
                CurrentPhase = Phase.Idle;
                GamePause.SetModalOpen(false); // the Pause Menu's own OnDestroy resumes the game
            }

            if (jingle != null)
            {
                Destroy(jingle);
            }
        }

        private T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ the numbers (by treasure id)

        /// <summary>The treasures of this scene and how many of them are in the save (by id). Cheap: a few dozen spots, called once per collect and once when opening.</summary>
        public AreaSummary Summarize()
        {
            ProgressService s = Service;
            int total = 0, collected = 0, gold = 0, collectedGold = 0;
            foreach (TreasureSpot spot in TreasureSpot.Active)
            {
                if (spot == null || spot.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                int value = spot.Definition != null ? spot.Definition.GoldValue : 0;
                total++;
                gold += value;
                if (s != null && s.IsCollected(spot.SpotId))
                {
                    collected++;
                    collectedGold += value;
                }
            }

            return new AreaSummary(total, collected, gold, collectedGold);
        }

        private ProgressService Service
        {
            get
            {
                if (service == null && progress != null)
                {
                    progress.Initialize();
                    service = progress.Service;
                }

                return service;
            }
        }

        /// <summary>The area that follows <paramref name="areaId"/> in the catalog, or null for the last one (Ancient Mine: stage 17D).</summary>
        public static string FollowingArea(ProgressService s, string areaId)
        {
            if (s == null)
            {
                return null;
            }

            int index = s.Catalog.IndexOf(areaId);
            return index >= 0 && index + 1 < s.Catalog.Areas.Count ? s.Catalog.Areas[index + 1].id : null;
        }

        // ------------------------------------------------------------------ trigger

        private void HandleCollected(TreasureSpot spot, int gold)
        {
            if (CurrentPhase != Phase.Idle)
            {
                return;
            }

            ProgressService s = Service;
            string areaId = hunt != null ? hunt.AreaId : null;
            if (s == null || string.IsNullOrEmpty(areaId) || FollowingArea(s, areaId) == null || s.IsCompletionCelebrated(areaId))
            {
                return; // not an area with a next one (Ancient Mine is 17D), or already celebrated
            }

            if (!Summarize().IsComplete)
            {
                return; // the gold goal (or anything else) is not enough: only every treasure counts
            }

            s.MarkCompletionPending(areaId); // owed from this moment on: leaving the scene or the app before the panel opens cannot lose it
            lastSpot = spot;
            waitFrame = Time.frameCount; // the reward presenters run on this very event: look at them from the next frame on
            quiet = 0f;
            waited = 0f;
            earliestOpenAt = 0f;
            CurrentPhase = Phase.Waiting;
        }

        /// <summary>True while something of the last COLLECT is still on screen, the game is paused by the player, or a tutorial is running.</summary>
        public bool IsRewardBusy()
        {
            return GamePause.IsPaused
                   || (lastSpot != null && lastSpot.VisualAnimator != null && lastSpot.VisualAnimator.IsAnimating)
                   || (rewardText != null && rewardText.IsVisible)
                   || (notifications != null && notifications.IsBusy)
                   || (tutorial != null && tutorial.IsRunning)
                   || (hunt != null && hunt.IsDigging);
        }

        private void Update()
        {
            switch (CurrentPhase)
            {
                case Phase.Waiting:
                    UpdateWaiting();
                    break;
                case Phase.Open:
                    UpdateOpen();
                    break;
            }
        }

        private void UpdateWaiting()
        {
            if (Time.frameCount <= waitFrame || GamePause.IsPaused)
            {
                return;
            }

            if (finals != null && finals.IsActive)
            {
                quiet = 0f; // stage 17D: a final celebration (GAME COMPLETE / 100% COLLECTION) of the same moment comes first, however long it waits (its time does not count here); this panel stays owed in the save and follows
                return;
            }

            waited += Time.unscaledDeltaTime;
            if (Time.unscaledTime < earliestOpenAt)
            {
                return;
            }

            if (IsRewardBusy() && waited < MaxWaitSeconds)
            {
                quiet = 0f;
                return;
            }

            quiet += Time.deltaTime;
            if (quiet >= SettleSeconds || waited >= MaxWaitSeconds)
            {
                Open();
            }
        }

        private void LateUpdate()
        {
            // After the Pause Menu's own Update (which ignores the key while a modal panel is open): the Android Back key is STAY HERE.
            if (CurrentPhase == Phase.Open && Time.unscaledTime - openedAt > 0.2f)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    Stay();
                }
            }
        }

        // ------------------------------------------------------------------ open / close

        private void Open()
        {
            ProgressService s = Service;
            string areaId = hunt != null ? hunt.AreaId : null;
            AreaSummary summary = Summarize();
            string next = FollowingArea(s, areaId);
            if (s == null || next == null || !summary.IsComplete || s.IsCompletionCelebrated(areaId))
            {
                CurrentPhase = Phase.Idle;
                return;
            }

            LastSummary = summary;
            NextAreaId = next;
            Fill(s, areaId, next, summary);
            s.MarkCompletionPending(areaId); // (already recorded by the last COLLECT; harmless again)
            openedAreaId = areaId;

            CurrentPhase = Phase.Open;
            openedAt = Time.unscaledTime;
            ShowCount++;
            deniedAt = -1f;
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            GamePause.Pause();
            GamePause.SetModalOpen(true);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            ApplyAnimation(0f);
            PlayJingle();
            Opened?.Invoke(areaId);
        }

        private void Fill(ProgressService s, string areaId, string next, AreaSummary summary)
        {
            string areaName = (s.GetDisplayName(areaId) ?? areaId).ToUpperInvariant();
            string nextName = (s.GetDisplayName(next) ?? next).ToUpperInvariant();
            SetText(titleLabel, TitleText);
            SetText(areaLabel, areaName + " COMPLETED");
            SetText(countLabel, $"{summary.Collected} / {summary.Total} TREASURES COLLECTED");
            SetText(goldLabel, $"{summary.AreaGold} GOLD AVAILABLE IN THIS AREA");
            SetText(nextLabel, "GO TO " + nextName);

            bool unlocked = s.IsAreaUnlocked(next);
            AreaEntry nextEntry = s.Catalog.Get(next);
            SetText(totalLabel, unlocked || nextEntry == null
                ? $"YOUR TOTAL GOLD: {s.TotalGold}"
                : $"{nextName} OPENS AT {nextEntry.unlockTotalGold} GOLD  -  YOUR TOTAL: {s.TotalGold}");
            if (nextButton != null)
            {
                nextButton.interactable = unlocked && !string.IsNullOrEmpty(SceneNavigator.SceneForArea(next));
            }

            AreaStyle style = StyleFor(areaId);
            accentColor = style != null ? style.accent : Color.white;
            if (picture != null)
            {
                picture.sprite = style != null ? style.picture : null;
                picture.enabled = picture.sprite != null;
            }

            if (areaLabel != null && style != null) { areaLabel.color = style.accentLight; }
            if (frame != null && style != null) { frame.color = style.accent; }
            if (divider != null && style != null) { divider.color = style.accentLight; }
            if (halo != null && style != null)
            {
                Color glow = style.accentLight;
                glow.a = halo.color.a;
                halo.color = glow;
            }
        }

        private AreaStyle StyleFor(string areaId)
        {
            foreach (AreaStyle style in styles)
            {
                if (style != null && style.areaId == areaId)
                {
                    return style;
                }
            }

            return null;
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>The player answered the celebration: it is recorded as celebrated (and no longer pending) in one save, so it never opens again until NEW GAME.</summary>
        private void Answer()
        {
            ProgressService s = Service;
            if (s != null && !string.IsNullOrEmpty(openedAreaId))
            {
                s.MarkCompletionCelebrated(openedAreaId);
            }
        }

        private void Close()
        {
            CurrentPhase = Phase.Idle;
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (source != null)
            {
                source.Stop();
            }

            GamePause.Resume(); // first: the Pause Menu shows its PAUSE button again only when the game is running
            GamePause.SetModalOpen(false);
        }

        /// <summary>STAY HERE (and the Android Back key): the panel closes and the game goes on exactly where it was. Only the celebration record changes in the save (answered, never shown again).</summary>
        public void Stay()
        {
            if (CurrentPhase != Phase.Open)
            {
                return;
            }

            Answer();
            Close();
            Stayed?.Invoke();
        }

        private void OnNextTapped()
        {
            GoToNextArea();
        }

        /// <summary>
        /// GO TO the next area (Rocky Canyon after Green Mine, Ancient Mine after Rocky Canyon). The Gold lock is checked again right now, exactly like the Area Selection does; a locked area is refused and the
        /// panel stays open. The miner, the gold, the collected treasure and the settings are read from the same stores as always by the next scene.
        /// </summary>
        public GoResult GoToNextArea()
        {
            if (CurrentPhase != Phase.Open)
            {
                return GoResult.NotOpen;
            }

            ProgressService s = Service;
            string scene = SceneNavigator.SceneForArea(NextAreaId);
            if (s == null || !s.IsAreaUnlocked(NextAreaId))
            {
                deniedAt = Time.unscaledTime;
                return GoResult.Locked;
            }

            if (string.IsNullOrEmpty(scene))
            {
                deniedAt = Time.unscaledTime;
                return GoResult.NoMap;
            }

            Answer();
            Close();
            SceneNavigator.Load(scene);
            return GoResult.Entered;
        }

        /// <summary>AREA SELECTION: back to the Main Menu with the Area Selection open (it reads the open areas, the gold and the miner from the save and the settings).</summary>
        public void OpenAreaSelection()
        {
            if (CurrentPhase != Phase.Open)
            {
                return;
            }

            Answer();
            Close();
            SceneNavigator.LoadAreaSelection();
        }

        // ------------------------------------------------------------------ sound

        private void PlayJingle()
        {
            float volume = JingleVolume * GameSettings.SfxScale; // Settings > SFX Volume: 0 is silent
            LastJingleVolume = volume;
            if (volume <= 0.001f)
            {
                return;
            }

            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true; // the game is paused while the panel is open
            }

            if (jingle == null)
            {
                jingle = SoundSynth.LevelComplete(); // built on the first celebration, never at scene start
            }

            source.Stop();
            source.clip = jingle;
            source.volume = volume;
            source.Play();
            JinglePlayCount++;
        }

        // ------------------------------------------------------------------ animation (unscaled time, a few rect updates)

        private void UpdateOpen()
        {
            ApplyAnimation(Time.unscaledTime - openedAt);
        }

        private void ApplyAnimation(float t)
        {
            if (dimGroup != null)
            {
                dimGroup.alpha = Mathf.Clamp01(t / 0.25f);
            }

            float pop = Mathf.Clamp01(t / EntranceSeconds);
            if (window != null)
            {
                window.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, EaseOutBack(pop));
            }

            if (windowGroup != null)
            {
                windowGroup.alpha = Mathf.Clamp01(t / 0.2f);
            }

            float trophyT = Mathf.Clamp01((t - 0.18f) / 0.5f);
            if (trophy != null)
            {
                float breathe = 1f + 0.035f * Mathf.Sin(t * 4.2f);
                trophy.localScale = Vector3.one * (EaseOutBack(trophyT, 2.4f) * breathe);
                trophy.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.1f) * 3f * trophyT);
            }

            if (halo != null)
            {
                Color c = halo.color;
                c.a = trophyT * (0.55f + 0.2f * Mathf.Sin(t * 3f));
                halo.color = c;
                halo.rectTransform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * 2.2f));
            }

            // one-shot star burst out of the trophy
            float burstT = Mathf.Clamp01((t - 0.3f) / 1.1f);
            for (int i = 0; i < burst.Length; i++)
            {
                RectTransform star = burst[i];
                if (star == null)
                {
                    continue;
                }

                float angle = (i + 0.5f) / burst.Length * Mathf.PI * 2f + 0.4f;
                float reach = 150f + 70f * ((i * 37) % 5) / 4f;
                float eased = 1f - (1f - burstT) * (1f - burstT);
                star.anchoredPosition = trophy != null
                    ? trophy.anchoredPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (reach * eased)
                    : Vector2.zero;
                star.localRotation = Quaternion.Euler(0f, 0f, burstT * (i % 2 == 0 ? 140f : -140f));
                float fade = burstT <= 0f ? 0f : Mathf.Clamp01(1f - Mathf.Pow(burstT, 1.6f));
                star.localScale = Vector3.one * (0.5f + 0.7f * fade);
                Image image = star.GetComponent<Image>();
                if (image != null)
                {
                    Color c = image.color;
                    c.a = fade;
                    image.color = c;
                }
            }

            // a few stars that keep twinkling around the banner
            for (int i = 0; i < twinkles.Length; i++)
            {
                if (twinkles[i] == null)
                {
                    continue;
                }

                float phase = Mathf.Sin(t * 3.4f + i * 1.7f) * 0.5f + 0.5f;
                twinkles[i].localScale = Vector3.one * (Mathf.Clamp01((t - 0.4f) / 0.3f) * (0.55f + 0.5f * phase));
            }

            // the refused GO TO button shakes
            if (nextButton != null)
            {
                var rect = (RectTransform)nextButton.transform;
                if (!nextRestCaptured)
                {
                    nextRest = rect.anchoredPosition;
                    nextRestCaptured = true;
                }

                float since = deniedAt >= 0f ? Time.unscaledTime - deniedAt : -1f;
                rect.anchoredPosition = since >= 0f && since < 0.35f
                    ? nextRest + new Vector2(Mathf.Sin(since * 60f) * 14f * (1f - since / 0.35f), 0f)
                    : nextRest;
            }
        }

        private static float EaseOutBack(float x, float overshoot = 1.70158f)
        {
            float c3 = overshoot + 1f;
            float f = x - 1f;
            return 1f + c3 * f * f * f + overshoot * f * f;
        }
    }
}
