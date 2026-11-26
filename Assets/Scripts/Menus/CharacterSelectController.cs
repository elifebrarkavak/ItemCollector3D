using System;
using ItemCollector3D.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Character Selection: big 3D miner in the middle (drawn by <see cref="MenuCharacterStage"/>), arrows left and right, the name
    /// below and a SELECT button (SELECTED on the miner that is already chosen) that saves the choice (PlayerPrefs through <see cref="CharacterSelection"/>). BACK leaves without
    /// changing the saved miner. The two miners have identical abilities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSelectController : MonoBehaviour
    {
        [SerializeField] private CharacterCatalog catalog;
        [SerializeField] private MenuCharacterStage stage;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private Button selectButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text taglineLabel;
        [SerializeField] private TMP_Text selectLabel;
        [Tooltip("The check mark on the select button: shown only on the miner that is already selected.")]
        [SerializeField] private GameObject selectedMark;
        [Header("SELECT / SELECTED look (stage 18B): SELECT is the gold action, SELECTED is the green, ticked state")]
        [SerializeField] private Image selectFace;
        [SerializeField] private Sprite selectSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Material selectLabelMaterial;
        [SerializeField] private Material selectedLabelMaterial;
        [SerializeField] private Image[] dots = Array.Empty<Image>();
        [SerializeField] private Color dotActive = new Color(0.949f, 0.722f, 0.247f, 1f);
        [SerializeField] private Color dotInactive = new Color(0.44f, 0.27f, 0.19f, 1f);

        private int viewing;
        private bool listenersAdded;

        public int ViewingIndex => viewing;
        public string ViewingId => catalog != null && catalog.Count > 0 ? catalog[viewing].id : null;
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>True when the screen was last closed with SELECT (the miner was saved), false for BACK. The owner reads it in its <see cref="Closed"/> handler.</summary>
        public bool ClosedByConfirm { get; private set; }

        /// <summary>True while the miner on show is the saved one (the button says SELECTED, is green and ticked).</summary>
        public bool ViewingIsSelected { get; private set; }

        public string SelectLabelText => selectLabel != null ? selectLabel.text : string.Empty;
        public bool SelectedMarkVisible => selectedMark != null && selectedMark.activeSelf;
        public Button SelectButton => selectButton;
        public bool SelectedLookShown => selectFace != null && selectedSprite != null && selectFace.sprite == selectedSprite;

        /// <summary>Raised when the screen closes (SELECT or BACK); the owner decides what to show next.</summary>
        public event Action Closed;

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
            if (leftButton != null) { leftButton.onClick.AddListener(() => Step(-1)); }
            if (rightButton != null) { rightButton.onClick.AddListener(() => Step(1)); }
            if (selectButton != null) { selectButton.onClick.AddListener(Confirm); }
            if (backButton != null) { backButton.onClick.AddListener(Cancel); }
        }

        public void Open()
        {
            EnsureListeners();
            gameObject.SetActive(true);
            viewing = catalog != null ? Mathf.Max(0, catalog.IndexOf(CharacterSelection.ResolveId(catalog))) : 0;
            if (stage != null)
            {
                stage.SetMode(MenuCharacterStage.Mode.Select);
                stage.ShowIndex(viewing, 0);
            }

            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Moves to the next / previous miner (wraps around).</summary>
        public void Step(int direction)
        {
            if (catalog == null || catalog.Count == 0 || direction == 0)
            {
                return;
            }

            viewing = ((viewing + direction) % catalog.Count + catalog.Count) % catalog.Count;
            if (stage != null)
            {
                stage.ShowIndex(viewing, direction > 0 ? 1 : -1);
            }

            Refresh();
        }

        /// <summary>SELECT: saves the viewed miner and returns to the menu.</summary>
        public void Confirm()
        {
            if (catalog != null && catalog.Count > 0)
            {
                CharacterSelection.Select(catalog[viewing].id);
            }

            ClosedByConfirm = true;
            CloseScreen();
        }

        /// <summary>BACK: returns to the menu; the saved miner is shown again.</summary>
        public void Cancel()
        {
            if (stage != null)
            {
                stage.ShowSelected();
            }

            ClosedByConfirm = false;
            CloseScreen();
        }

        private void CloseScreen()
        {
            if (stage != null)
            {
                stage.SetMode(MenuCharacterStage.Mode.Menu);
            }

            Hide();
            Closed?.Invoke();
        }

        private void Refresh()
        {
            if (catalog == null || catalog.Count == 0)
            {
                return;
            }

            CharacterCatalog.Entry entry = catalog[viewing];
            bool equipped = entry.id == CharacterSelection.ResolveId(catalog);
            if (nameLabel != null) { nameLabel.text = entry.displayName; }
            if (taglineLabel != null) { taglineLabel.text = entry.tagline; }
            ViewingIsSelected = equipped;
            if (selectLabel != null) { selectLabel.text = equipped ? "SELECTED" : "SELECT"; }
            if (selectedMark != null) { selectedMark.SetActive(equipped); }
            if (selectFace != null && selectSprite != null && selectedSprite != null) { selectFace.sprite = equipped ? selectedSprite : selectSprite; }
            if (selectLabel != null && selectLabelMaterial != null && selectedLabelMaterial != null) { selectLabel.fontSharedMaterial = equipped ? selectedLabelMaterial : selectLabelMaterial; }
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] != null)
                {
                    dots[i].color = i == viewing ? dotActive : dotInactive;
                    dots[i].rectTransform.localScale = Vector3.one * (i == viewing ? 1.25f : 1f);
                }
            }
        }
    }
}
