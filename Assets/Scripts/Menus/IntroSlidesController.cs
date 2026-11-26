using System;
using UnityEngine;
using UnityEngine.UI;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// The three-page introduction shown right after a successful NEW GAME (stage 17A): WELCOME, FIND HIDDEN TREASURES, UNLOCK NEW WORLDS. Pure presentation: it never touches the save, the
    /// settings or the miner. The panel is a full-screen overlay (its dim catches every touch, so nothing behind it can be pressed) inside the Main Menu, which owns when it opens and what
    /// follows. Buttons: page 1 SKIP / NEXT, middle pages BACK / SKIP / NEXT, last page BACK / LET'S PLAY. SKIP and LET'S PLAY raise <see cref="Finished"/> (with the reason).
    /// Pages cross-fade (0.25 s, unscaled time); the page index changes at once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntroSlidesController : MonoBehaviour
    {
        public const int PageCount = 3;
        public const float FadeSeconds = 0.25f;

        public static readonly string[] Titles = { "WELCOME TO THE ADVENTURE", "FIND HIDDEN TREASURES", "UNLOCK NEW WORLDS" };

        [SerializeField] private CanvasGroup[] pages = Array.Empty<CanvasGroup>();
        [SerializeField] private Image[] dots = Array.Empty<Image>();
        [SerializeField] private Button backButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Color dotOn = new(1f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color dotOff = new(1f, 1f, 1f, 0.35f);

        private bool listenersAdded;

        public int PageIndex { get; private set; }

        public bool IsOpen => gameObject.activeSelf;

        public Button BackButton => backButton;
        public Button SkipButton => skipButton;
        public Button NextButton => nextButton;
        public Button PlayButton => playButton;
        public int PagesBuilt => pages.Length;

        /// <summary>Raised when the player pressed SKIP (true) or LET'S PLAY (false). The owner decides what comes next.</summary>
        public event Action<bool> Finished;

        private void Awake()
        {
            EnsureListeners();
        }

        private void EnsureListeners()
        {
            if (listenersAdded)
            {
                return;
            }

            listenersAdded = true;
            if (backButton != null) { backButton.onClick.AddListener(Back); }
            if (skipButton != null) { skipButton.onClick.AddListener(Skip); }
            if (nextButton != null) { nextButton.onClick.AddListener(Next); }
            if (playButton != null) { playButton.onClick.AddListener(Play); }
        }

        /// <summary>Opens on the first page.</summary>
        public void Open()
        {
            EnsureListeners();
            gameObject.SetActive(true);
            PageIndex = 0;
            Apply(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Next()
        {
            if (PageIndex < PageCount - 1)
            {
                PageIndex++;
                Apply(false);
            }
        }

        public void Back()
        {
            if (PageIndex > 0)
            {
                PageIndex--;
                Apply(false);
            }
        }

        /// <summary>SKIP: ends the introduction.</summary>
        public void Skip()
        {
            Finished?.Invoke(true);
        }

        /// <summary>LET'S PLAY (last page): ends the introduction.</summary>
        public void Play()
        {
            Finished?.Invoke(false);
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / FadeSeconds;
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null)
                {
                    continue;
                }

                float target = i == PageIndex ? 1f : 0f;
                pages[i].alpha = Mathf.MoveTowards(pages[i].alpha, target, step);
                bool visible = i == PageIndex || pages[i].alpha > 0.001f;
                if (pages[i].gameObject.activeSelf != visible)
                {
                    pages[i].gameObject.SetActive(visible);
                }
            }
        }

        private void Apply(bool immediate)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null)
                {
                    continue;
                }

                if (immediate)
                {
                    pages[i].alpha = i == PageIndex ? 1f : 0f;
                }

                pages[i].gameObject.SetActive(i == PageIndex || pages[i].alpha > 0.001f);
                pages[i].blocksRaycasts = false;
                pages[i].interactable = false;
            }

            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] != null)
                {
                    dots[i].color = i == PageIndex ? dotOn : dotOff;
                    dots[i].transform.localScale = Vector3.one * (i == PageIndex ? 1.25f : 1f);
                }
            }

            bool first = PageIndex == 0, last = PageIndex == PageCount - 1;
            SetActive(backButton, !first);
            SetActive(skipButton, !last);
            SetActive(nextButton, !last);
            SetActive(playButton, last);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null && button.gameObject.activeSelf != active)
            {
                button.gameObject.SetActive(active);
            }
        }
    }
}
