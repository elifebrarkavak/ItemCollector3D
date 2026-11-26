using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace ItemCollector3D.UI
{
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        [Header("Gold")]
        [SerializeField] private TMP_Text levelTitleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private UnityEngine.UI.Image goldFillImage;
        [SerializeField, Min(1)] private int maximumGold = 500;
        [SerializeField, Min(0)] private int currentGold;

        [Header("Actions")]
        [SerializeField] private UnityEngine.UI.Button digButton;
        [SerializeField] private UnityEngine.UI.Button collectButton;
        [SerializeField] private UnityEvent digRequested = new();
        [SerializeField] private UnityEvent collectRequested = new();

        public event Action DigRequested;
        public event Action CollectRequested;

        public int CurrentGold => currentGold;

        /// <summary>The DIG / COLLECT buttons' rects (the tutorial highlights them; null when the HUD has none).</summary>
        public RectTransform DigButtonRect => digButton != null ? (RectTransform)digButton.transform : null;

        public RectTransform CollectButtonRect => collectButton != null ? (RectTransform)collectButton.transform : null;
        public int MaximumGold => maximumGold;

        private void Awake()
        {
            if (digButton != null)
            {
                digButton.onClick.AddListener(HandleDigClicked);
            }

            if (collectButton != null)
            {
                collectButton.onClick.AddListener(HandleCollectClicked);
            }

            RefreshGoldDisplay();
        }

        private void OnDestroy()
        {
            if (digButton != null)
            {
                digButton.onClick.RemoveListener(HandleDigClicked);
            }

            if (collectButton != null)
            {
                collectButton.onClick.RemoveListener(HandleCollectClicked);
            }
        }

        public void SetLevelTitle(string title)
        {
            if (levelTitleText != null)
            {
                levelTitleText.text = title;
            }
        }

        public void SetGold(int value)
        {
            currentGold = Mathf.Clamp(value, 0, maximumGold);
            RefreshGoldDisplay();
        }

        public void SetMaximumGold(int value)
        {
            maximumGold = Mathf.Max(1, value);
            currentGold = Mathf.Clamp(currentGold, 0, maximumGold);
            RefreshGoldDisplay();
        }

        public void SetDigState(bool visible, bool interactable = true)
        {
            SetButtonState(digButton, visible, interactable);
        }

        public void SetCollectState(bool visible, bool interactable = true)
        {
            SetButtonState(collectButton, visible, interactable);
        }

        private void RefreshGoldDisplay()
        {
            if (goldText != null)
            {
                goldText.text = $"{currentGold} / {maximumGold} GOLD";
            }

            if (goldFillImage != null)
            {
                goldFillImage.fillAmount = maximumGold > 0 ? (float)currentGold / maximumGold : 0f;
            }
        }

        private static void SetButtonState(UnityEngine.UI.Button button, bool visible, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
            button.interactable = interactable;
        }

        private void HandleDigClicked()
        {
            digRequested.Invoke();
            DigRequested?.Invoke();
        }

        private void HandleCollectClicked()
        {
            collectRequested.Invoke();
            CollectRequested?.Invoke();
        }
    }
}
