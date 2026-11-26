using System;
using System.Collections.Generic;
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
    /// The final celebrations of the game (stage 17D), one controller in each playable map (Green Mine, Rocky Canyon, Ancient Mine):
    /// <list type="bullet">
    /// <item><description>GAME COMPLETE: the total gold reached 2500 (<see cref="FinalAchievements.GameComplete"/>). The big one.</description></item>
    /// <item><description>100% COLLECTION COMPLETE: all 60 treasures of the three maps are collected (<see cref="FinalAchievements.Collection"/>). A plainer one. No gold.</description></item>
    /// </list>
    /// Both end with the same three choices: CONTINUE EXPLORING (the panel closes, the game goes on; the Android Back key does the same), AREA SELECTION, MAIN MENU. Nothing here ends the game, resets anything or pays anything.
    /// <para>
    /// Whether a goal is reached, whether its celebration is OWED and whether it was ANSWERED are three separate facts (see <see cref="FinalAchievements"/>). The service marks the celebration as owed in the very save
    /// of the COLLECT that crossed the goal, in whichever map that happens (a player who finished Ancient Mine and later collects the rest of Green Mine can cross 2500 there). This controller only SHOWS what is owed:
    /// after a COLLECT it waits (like <see cref="LevelCompleteController"/>) until the collect animation, the floating reward text, the AREA COMPLETE / NEW AREA UNLOCKED banners and a running tutorial are quiet, then
    /// pauses the game through <see cref="GamePause"/> (the HUD hides, joystick, camera and DIG / COLLECT get nothing, the full-screen dim takes every touch) and shows ONE panel. When two celebrations are owed GAME
    /// COMPLETE comes first and CONTINUE EXPLORING leads straight into 100% COLLECTION (the game stays paused, there is never a second panel at the same time). Only the player's answer (CONTINUE EXPLORING, AREA SELECTION,
    /// MAIN MENU, Back) records it as celebrated; leaving the scene or closing the app before that keeps it owed, and the next gameplay scene shows it once, a moment after it is entered. It never opens on loading a save by
    /// itself: a save that was already past a goal before this version has nothing owed. When the LEVEL COMPLETE panel of the same collect is also waiting it yields: the final celebration is shown first and LEVEL COMPLETE
    /// (still owed in the save) follows.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FinalCelebrationController : MonoBehaviour
    {
        public enum Phase { Idle, Waiting, Open }

        /// <summary>Quiet time (no animation, text or banner running) required before the panel opens.</summary>
        public const float SettleSeconds = 0.4f;

        /// <summary>The wait is given up after this long (real time, not counting pause) so a stuck banner can never swallow the celebration.</summary>
        public const float MaxWaitSeconds = 10f;

        /// <summary>An owed celebration of a scene that is entered waits this long first (the scene fades in and the player sees the place before the panel covers it).</summary>
        public const float EntrySeconds = 1f;

        public const float EntranceSeconds = 0.6f;
        public const float JingleVolume = 0.55f;

        public const string GameCompleteTitle = "GAME COMPLETE!";
        public const string GameCompleteSubtitle = "YOU ARE A MASTER TREASURE HUNTER!";
        public const string CollectionTitle = "100% COLLECTION COMPLETE!";
        public const string CollectionSubtitle = "YOU FOUND EVERY HIDDEN TREASURE!";
        public const string ContinueText = "CONTINUE EXPLORING";
        public const string AreaSelectionText = "AREA SELECTION";
        public const string MainMenuText = "MAIN MENU";

        /// <summary>One of the two layouts of the panel (GAME COMPLETE is the grand one, COLLECTION the plainer one). Built by FinalCelebrationUiBuilder.</summary>
        [Serializable]
        public sealed class Skin
        {
            public RectTransform window;
            public CanvasGroup group;
            public TMP_Text title;
            public TMP_Text subtitle;
            [Tooltip("GAME COMPLETE: TOTAL GOLD. COLLECTION: TOTAL GOLD.")]
            public TMP_Text goldLabel;
            [Tooltip("GAME COMPLETE only: 2500 GOLD GOAL REACHED.")]
            public TMP_Text goalLabel;
            public TMP_Text treasuresLabel;
            [Tooltip("GAME COMPLETE only: ALL 3 AREAS UNLOCKED.")]
            public TMP_Text areasLabel;
            public TMP_Text messageLabel;
            [Tooltip("COLLECTION only: one row per area in catalog order (GREEN MINE 15 / 15, ...).")]
            public TMP_Text[] areaRows = Array.Empty<TMP_Text>();
            public Image picture;
            public Button continueButton;
            public Button areaSelectionButton;
            public Button mainMenuButton;
            public RectTransform trophy;
            public Image halo;
            public RectTransform[] burst = Array.Empty<RectTransform>();
            public RectTransform[] twinkles = Array.Empty<RectTransform>();
        }

        [Header("Scene (found in the scene when empty)")]
        [SerializeField] private ProgressManager progress;
        [SerializeField] private TreasureHuntController hunt;
        [SerializeField] private FloatingRewardText rewardText;
        [SerializeField] private ProgressNotificationPresenter notifications;
        [SerializeField] private TutorialController tutorial;

        [Header("Panel (built by FinalCelebrationUiBuilder)")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private Skin gameSkin = new();
        [SerializeField] private Skin collectionSkin = new();

        private ProgressService service;
        private TreasureSpot lastSpot;
        private bool subscribed;
        private int waitFrame;
        private float quiet;
        private float waited;
        private float earliestOpenAt;
        private float openedAt;
        private Skin active;
        private AudioSource source;
        private AudioClip gameJingle;
        private AudioClip collectionJingle;
        private readonly List<string> shown = new();

        public Phase CurrentPhase { get; private set; }
        public bool IsOpen => CurrentPhase == Phase.Open;
        public bool IsWaiting => CurrentPhase == Phase.Waiting;

        /// <summary>True while a final celebration is waiting for its moment or on screen (the LEVEL COMPLETE panel of the same scene yields meanwhile).</summary>
        public bool IsActive => CurrentPhase != Phase.Idle;

        /// <summary>The achievement the panel shows right now (FinalAchievements id), null when it is not open.</summary>
        public string CurrentAchievement { get; private set; }

        /// <summary>How many panels opened in this scene (GAME COMPLETE and 100% COLLECTION count one each).</summary>
        public int ShowCount { get; private set; }

        /// <summary>The achievements that were shown in this scene, in order.</summary>
        public IReadOnlyList<string> ShownAchievements => shown;

        public int JinglePlayCount { get; private set; }
        public float LastJingleVolume { get; private set; }

        public Skin Game => gameSkin;
        public Skin Collection => collectionSkin;
        public Skin ActiveSkin => active;
        public GameObject PanelRoot => panelRoot;
        public Image Dim => dimGroup != null ? dimGroup.GetComponent<Image>() : null;

        /// <summary>Raised when a panel opened (the achievement id).</summary>
        public event Action<string> Opened;

        /// <summary>Raised when the panel closed for CONTINUE EXPLORING or the Back key and the game goes on (the achievement id that was answered).</summary>
        public event Action<string> Continued;

        private void Awake()
        {
            progress = progress != null ? progress : FindInScene<ProgressManager>();
            hunt = hunt != null ? hunt : FindInScene<TreasureHuntController>();
            rewardText = rewardText != null ? rewardText : FindInScene<FloatingRewardText>();
            notifications = notifications != null ? notifications : FindInScene<ProgressNotificationPresenter>();
            tutorial = tutorial != null ? tutorial : FindInScene<TutorialController>();

            foreach (Skin skin in new[] { gameSkin, collectionSkin })
            {
                if (skin == null)
                {
                    continue;
                }

                if (skin.continueButton != null) { skin.continueButton.onClick.AddListener(ContinueExploring); }
                if (skin.areaSelectionButton != null) { skin.areaSelectionButton.onClick.AddListener(OpenAreaSelection); }
                if (skin.mainMenuButton != null) { skin.mainMenuButton.onClick.AddListener(OpenMainMenu); }
            }

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

            if (gameJingle != null) { Destroy(gameJingle); }
            if (collectionJingle != null) { Destroy(collectionJingle); }
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

        // ------------------------------------------------------------------ trigger

        /// <summary>
        /// A celebration is still owed (the player left before it opened, or closed the app with the panel open, without answering it): wait for the scene to settle, then show it. Only an OWED record starts this, never a
        /// merely reached goal, so older games that were past 2500 gold or 60 treasures before 17D stay quiet.
        /// </summary>
        private void ResumePending()
        {
            ProgressService s = Service;
            if (CurrentPhase != Phase.Idle || s == null || s.NextPendingAchievement() == null || hunt == null || !hunt.IsAreaAccessible)
            {
                return;
            }

            BeginWaiting(null, Time.unscaledTime + EntrySeconds);
        }

        private void HandleCollected(TreasureSpot spot, int gold)
        {
            ProgressService s = Service;
            if (CurrentPhase != Phase.Idle || s == null || s.NextPendingAchievement() == null)
            {
                return; // nothing is owed: the service marks a celebration as owed in the save of the COLLECT that crossed a goal
            }

            BeginWaiting(spot, 0f);
        }

        private void BeginWaiting(TreasureSpot spot, float notBefore)
        {
            lastSpot = spot;
            waitFrame = Time.frameCount; // the reward presenters run on this very event: look at them from the next frame on
            quiet = 0f;
            waited = 0f;
            earliestOpenAt = notBefore;
            CurrentPhase = Phase.Waiting;
        }

        /// <summary>True while something of the last COLLECT is still on screen, the game is paused by the player (or another panel), or a tutorial is running.</summary>
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
                    ApplyAnimation(active, Time.unscaledTime - openedAt);
                    break;
            }
        }

        private void UpdateWaiting()
        {
            if (Time.frameCount <= waitFrame || GamePause.IsPaused || GamePause.ModalOpen)
            {
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
            // After the Pause Menu's own Update (which ignores the key while a modal panel is open): the Android Back key is CONTINUE EXPLORING.
            if (CurrentPhase == Phase.Open && Time.unscaledTime - openedAt > 0.2f)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    ContinueExploring();
                }
            }
        }

        // ------------------------------------------------------------------ open / close

        private void Open()
        {
            ProgressService s = Service;
            string next = s != null ? s.NextPendingAchievement() : null;
            if (next == null)
            {
                CurrentPhase = Phase.Idle;
                return;
            }

            CurrentPhase = Phase.Open;
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

            Present(s, next);
        }

        /// <summary>Shows the panel of one achievement (only one window is ever visible) and plays its sound.</summary>
        private void Present(ProgressService s, string achievementId)
        {
            bool isGame = achievementId == FinalAchievements.GameComplete;
            Skin skin = isGame ? gameSkin : collectionSkin;
            Skin other = isGame ? collectionSkin : gameSkin;
            if (other != null && other.window != null) { other.window.gameObject.SetActive(false); }
            if (skin != null && skin.window != null) { skin.window.gameObject.SetActive(true); }

            active = skin;
            CurrentAchievement = achievementId;
            if (isGame)
            {
                FillGameComplete(s, skin);
            }
            else
            {
                FillCollection(s, skin);
            }

            openedAt = Time.unscaledTime;
            ShowCount++;
            shown.Add(achievementId);
            ApplyAnimation(skin, 0f);
            PlayJingle(isGame);
            Opened?.Invoke(achievementId);
        }

        private static void FillGameComplete(ProgressService s, Skin skin)
        {
            int areaCount = s.Catalog.Areas.Count;
            int open = 0;
            foreach (AreaEntry area in s.Catalog.Areas)
            {
                if (s.IsAreaUnlocked(area.id))
                {
                    open++;
                }
            }

            int found = s.CollectedTreasureCount;
            SetText(skin.title, GameCompleteTitle);
            SetText(skin.subtitle, GameCompleteSubtitle);
            SetText(skin.goldLabel, "TOTAL GOLD: " + s.TotalGold);
            SetText(skin.goalLabel, FinalAchievements.Goal + " GOLD GOAL REACHED");
            SetText(skin.treasuresLabel, $"{found} / {TreasureCatalog.TotalCount} TREASURES COLLECTED");
            SetText(skin.areasLabel, open >= areaCount ? $"ALL {areaCount} AREAS UNLOCKED" : $"{open} / {areaCount} AREAS UNLOCKED");
            SetText(skin.messageLabel, found >= TreasureCatalog.TotalCount
                ? "EVERY TREASURE IS YOURS!"
                : "SOME TREASURES ARE STILL HIDDEN - KEEP DIGGING!");
            if (skin.picture != null)
            {
                skin.picture.enabled = skin.picture.sprite != null;
            }
        }

        private static void FillCollection(ProgressService s, Skin skin)
        {
            SetText(skin.title, CollectionTitle);
            SetText(skin.subtitle, CollectionSubtitle);
            SetText(skin.treasuresLabel, $"{s.CollectedTreasureCount} / {TreasureCatalog.TotalCount}");
            SetText(skin.goldLabel, "TOTAL GOLD: " + s.TotalGold);
            SetText(skin.messageLabel, "A PERMANENT ACHIEVEMENT");
            IReadOnlyList<AreaEntry> areas = s.Catalog.Areas;
            for (int i = 0; i < skin.areaRows.Length; i++)
            {
                if (i >= areas.Count)
                {
                    SetText(skin.areaRows[i], string.Empty);
                    continue;
                }

                string name = (areas[i].displayName ?? areas[i].id).ToUpperInvariant();
                int total = TreasureCatalog.IdsOf(areas[i].id).Count;
                SetText(skin.areaRows[i], $"{name}   {s.CollectedTreasureCountIn(areas[i].id)} / {total}");
            }
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>The player answered the celebration that is on screen: it is recorded as celebrated (and no longer owed) in one save, so it never opens again until NEW GAME.</summary>
        private void Answer()
        {
            ProgressService s = Service;
            if (s != null && !string.IsNullOrEmpty(CurrentAchievement))
            {
                s.MarkAchievementCelebrated(CurrentAchievement);
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
            active = null;
        }

        /// <summary>
        /// CONTINUE EXPLORING (and the Android Back key): the answer is saved, then either the other owed celebration is shown (the game stays paused, the panel just changes) or, when nothing else is owed, the panel
        /// closes and the game goes on exactly where it was.
        /// </summary>
        public void ContinueExploring()
        {
            if (CurrentPhase != Phase.Open)
            {
                return;
            }

            string answered = CurrentAchievement;
            Answer();
            ProgressService s = Service;
            string next = s != null ? s.NextPendingAchievement() : null;
            if (next != null)
            {
                Present(s, next);
                return;
            }

            Close();
            CurrentAchievement = null;
            Continued?.Invoke(answered);
        }

        /// <summary>AREA SELECTION: back to the Main Menu with the Area Selection open (open areas, total gold and miner come from the save). A second owed celebration, if any, stays owed and is shown in the next map.</summary>
        public void OpenAreaSelection()
        {
            if (CurrentPhase != Phase.Open)
            {
                return;
            }

            Answer();
            Close();
            CurrentAchievement = null;
            SceneNavigator.LoadAreaSelection();
        }

        /// <summary>MAIN MENU: back to the Main Menu (the progress stays saved). A second owed celebration, if any, stays owed and is shown in the next map.</summary>
        public void OpenMainMenu()
        {
            if (CurrentPhase != Phase.Open)
            {
                return;
            }

            Answer();
            Close();
            CurrentAchievement = null;
            SceneNavigator.Load(SceneNavigator.MainMenuScene);
        }

        // ------------------------------------------------------------------ sound

        private void PlayJingle(bool isGame)
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

            AudioClip clip;
            if (isGame)
            {
                gameJingle = gameJingle != null ? gameJingle : SoundSynth.GameComplete(); // built on the first celebration, never at scene start
                clip = gameJingle;
            }
            else
            {
                collectionJingle = collectionJingle != null ? collectionJingle : SoundSynth.LevelComplete();
                clip = collectionJingle;
            }

            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.Play();
            JinglePlayCount++;
        }

        // ------------------------------------------------------------------ animation (unscaled time, a few rect updates)

        private void ApplyAnimation(Skin skin, float t)
        {
            if (skin == null)
            {
                return;
            }

            if (dimGroup != null)
            {
                dimGroup.alpha = Mathf.Clamp01(t / 0.25f);
            }

            float pop = Mathf.Clamp01(t / EntranceSeconds);
            if (skin.window != null)
            {
                skin.window.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, EaseOutBack(pop));
            }

            if (skin.group != null)
            {
                skin.group.alpha = Mathf.Clamp01(t / 0.2f);
            }

            float trophyT = Mathf.Clamp01((t - 0.18f) / 0.5f);
            if (skin.trophy != null)
            {
                float breathe = 1f + 0.035f * Mathf.Sin(t * 4.2f);
                skin.trophy.localScale = Vector3.one * (EaseOutBack(trophyT, 2.4f) * breathe);
                skin.trophy.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.1f) * 3f * trophyT);
            }

            if (skin.halo != null)
            {
                Color c = skin.halo.color;
                c.a = trophyT * (0.55f + 0.2f * Mathf.Sin(t * 3f));
                skin.halo.color = c;
                skin.halo.rectTransform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * 2.2f));
            }

            // one-shot star burst out of the trophy
            float burstT = Mathf.Clamp01((t - 0.3f) / 1.2f);
            for (int i = 0; i < skin.burst.Length; i++)
            {
                RectTransform star = skin.burst[i];
                if (star == null)
                {
                    continue;
                }

                float angle = (i + 0.5f) / skin.burst.Length * Mathf.PI * 2f + 0.4f;
                float reach = 160f + 80f * ((i * 37) % 5) / 4f;
                float eased = 1f - (1f - burstT) * (1f - burstT);
                star.anchoredPosition = skin.trophy != null
                    ? skin.trophy.anchoredPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (reach * eased)
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
            for (int i = 0; i < skin.twinkles.Length; i++)
            {
                if (skin.twinkles[i] == null)
                {
                    continue;
                }

                float phase = Mathf.Sin(t * 3.4f + i * 1.7f) * 0.5f + 0.5f;
                skin.twinkles[i].localScale = Vector3.one * (Mathf.Clamp01((t - 0.4f) / 0.3f) * (0.55f + 0.5f * phase));
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
