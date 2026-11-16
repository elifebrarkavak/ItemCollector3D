using UnityEngine;

namespace ItemCollector3D.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea
                || lastScreenSize.x != Screen.width
                || lastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            Vector2 screenSize = new(Screen.width, Screen.height);

            rectTransform.anchorMin = safeArea.position / screenSize;
            rectTransform.anchorMax = (safeArea.position + safeArea.size) / screenSize;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
