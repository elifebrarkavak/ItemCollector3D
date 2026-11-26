using System;
using ItemCollector3D.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Area Selection (Main Menu -> PLAY): three big area cards. It adds NO progress or lock logic of its own: whether an area is open comes from the
    /// saved total gold through <see cref="ProgressService"/> (thresholds from the <see cref="AreaCatalog"/>), re-read every time the screen opens.
    /// An open area with a scene is entered with <see cref="SceneNavigator"/>; a locked area, or an open one that has no map yet (COMING SOON), is refused.
    /// The chosen miner is not touched: gameplay reads it from <c>CharacterSelection</c> as before.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AreaSelectController : MonoBehaviour
    {
        [SerializeField] private AreaCatalog catalog;
        [Tooltip("The scene's progress. When empty one is searched in the scene, then a service on the save file is created.")]
        [SerializeField] private ProgressManager progress;
        [SerializeField] private AreaCardView[] cards = Array.Empty<AreaCardView>();
        [SerializeField] private Button backButton;
        [Tooltip("TOTAL GOLD: 655, once for the whole screen (never a per map number).")]
        [SerializeField] private TMP_Text totalGoldLabel;
        [SerializeField] private MenuCharacterStage stage;

        private ProgressService fallbackService;
        private int focusedIndex = -1;
        private bool listenersAdded;

        public int CardCount => cards.Length;
        public AreaCardView GetCard(int index) => cards[index];
        public int FocusedIndex => focusedIndex;
        public bool IsOpen => gameObject.activeSelf;
        public Button BackButton => backButton;
        public TMP_Text TotalGoldLabel => totalGoldLabel;

        /// <summary>The label of the player's total gold (the gold of the whole game, not of one map).</summary>
        public static string TotalGoldText(int totalGold) => "TOTAL GOLD: " + Mathf.Max(0, totalGold);

        /// <summary>Raised when BACK is pressed (the owner decides what to show next).</summary>
        public event Action Closed;

        /// <summary>Raised when an area is about to be entered (the area id).</summary>
        public event Action<string> AreaEntered;

        private void Awake()
        {
            if (stage == null)
            {
                stage = FindAnyObjectByType<MenuCharacterStage>(); // the stage lives in the scene, this panel in a prefab
            }

            EnsureListeners();
        }

        private void EnsureListeners()
        {
            if (listenersAdded)
            {
                return;
            }

            listenersAdded = true;
            if (backButton != null)
            {
                backButton.onClick.AddListener(Cancel);
            }

            foreach (AreaCardView card in cards)
            {
                if (card != null)
                {
                    card.ActionTapped += OnActionTapped;
                    card.BodyTapped += OnBodyTapped;
                }
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Open()
        {
            EnsureListeners();
            gameObject.SetActive(true);
            if (stage != null)
            {
                stage.SetVisible(false); // the 3D miner makes room for the cards
            }

            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>BACK: leaves the screen (the Main Menu is shown again) and the miner returns.</summary>
        public void Cancel()
        {
            if (stage != null)
            {
                stage.SetVisible(true);
            }

            Hide();
            Closed?.Invoke();
        }

        private ProgressService Service
        {
            get
            {
                if (progress == null)
                {
                    progress = FindAnyObjectByType<ProgressManager>();
                }

                if (progress != null)
                {
                    progress.Initialize();
                    return progress.Service;
                }

                fallbackService ??= ProgressService.Create(catalog, new FileProgressStore());
                return fallbackService;
            }
        }

        /// <summary>Re-reads the saved progress and shows it on every card (called whenever the screen opens).</summary>
        public void Refresh()
        {
            EnsureListeners();
            ProgressService service = Service;
            if (totalGoldLabel != null)
            {
                totalGoldLabel.text = TotalGoldText(service.TotalGold); // the whole game's gold, said so, shown once for all three maps
            }

            for (int i = 0; i < cards.Length; i++)
            {
                AreaCardView card = cards[i];
                if (card == null)
                {
                    continue;
                }

                bool known = service.Catalog.Get(card.AreaId) != null;
                Transform slot = card.transform.parent; // the wrapper that owns the entrance animation
                (slot != null ? slot.gameObject : card.gameObject).SetActive(known);
                if (known)
                {
                    card.SetState(Evaluate(service, card));
                }
            }

            if (focusedIndex < 0 || focusedIndex >= cards.Length || cards[focusedIndex] == null || cards[focusedIndex].State.IsLocked)
            {
                Focus(FirstPlayableIndex());
            }
            else
            {
                Focus(focusedIndex);
            }
        }

        /// <summary>The state of one area, from the saved progress. Open comes from <see cref="ProgressService.IsAreaUnlocked"/> (total gold against the catalog threshold).</summary>
        public static AreaCardState Evaluate(ProgressService service, string areaId, string sceneName)
        {
            AreaEntry entry = service.Catalog.Get(areaId);
            if (entry == null)
            {
                return new AreaCardState(areaId, areaId, AreaAvailability.Locked, service.TotalGold, 0, 0, false);
            }

            AreaAvailability availability = !service.IsAreaUnlocked(areaId)
                ? AreaAvailability.Locked
                : string.IsNullOrEmpty(sceneName) ? AreaAvailability.ComingSoon : AreaAvailability.Playable;

            // the treasures of the map by id (15 / 20 / 25), never the gold: "complete" on a card means every treasure is found
            int total = TreasureCatalog.IdsOf(areaId).Count;
            int found = service.CollectedTreasureCountIn(areaId);
            return new AreaCardState(areaId, entry.displayName, availability, service.TotalGold, entry.unlockTotalGold, entry.goalTotalGold, total > 0 && found >= total, found, total);
        }

        private static AreaCardState Evaluate(ProgressService service, AreaCardView card)
        {
            return Evaluate(service, card.AreaId, card.SceneName);
        }

        public AreaCardState GetState(string areaId)
        {
            AreaCardView card = FindCard(areaId);
            return card != null ? Evaluate(Service, card) : default;
        }

        private AreaCardView FindCard(string areaId)
        {
            foreach (AreaCardView card in cards)
            {
                if (card != null && card.AreaId == areaId)
                {
                    return card;
                }
            }

            return null;
        }

        private int FirstPlayableIndex()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null && cards[i].State.Availability == AreaAvailability.Playable)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>Highlights one card (it lifts and glows). Closed cards cannot be focused; tapping them only shakes them.</summary>
        public void Focus(int index)
        {
            if (index < 0 || index >= cards.Length || cards[index] == null)
            {
                return;
            }

            focusedIndex = index;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].SetFocused(i == index);
                }
            }
        }

        /// <summary>
        /// Enters an area. Only an open area that has a scene loads it; a locked area (total gold below its threshold) and an area without a map
        /// (COMING SOON) are refused, and their card shakes.
        /// </summary>
        public AreaEnterResult TryEnter(string areaId)
        {
            AreaCardView card = FindCard(areaId);
            if (card == null)
            {
                return AreaEnterResult.UnknownArea;
            }

            AreaCardState current = Evaluate(Service, card); // re-checked from the progress right now, never from what was shown earlier
            card.SetState(current);
            switch (current.Availability)
            {
                case AreaAvailability.Locked:
                    card.PlayDenied();
                    return AreaEnterResult.Locked;
                case AreaAvailability.ComingSoon:
                    card.PlayDenied();
                    return AreaEnterResult.ComingSoon;
            }

            AreaEntered?.Invoke(areaId);
            SceneNavigator.Load(card.SceneName);
            return AreaEnterResult.Entered;
        }

        private void OnActionTapped(AreaCardView card)
        {
            TryEnter(card.AreaId);
        }

        private void OnBodyTapped(AreaCardView card)
        {
            int index = Array.IndexOf(cards, card);
            if (card.State.Availability == AreaAvailability.Playable)
            {
                Focus(index);
            }
            else
            {
                card.PlayDenied();
            }
        }
    }
}
