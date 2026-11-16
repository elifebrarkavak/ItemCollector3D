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

        /// <summary>
        /// Converts a safe area (pixels) into normalised anchors. Returns false - and changes nothing - when the screen
        /// size or safe area is not usable (zero/negative size or NaN/Infinity, e.g. a hidden or collapsed Game View).
        /// Without this guard the division produces NaN anchors, every child RectTransform becomes NaN, the HUD disappears
        /// and TextMeshPro logs "Invalid AABB inAABB".
        /// </summary>
        public static bool TryComputeAnchors(Rect safeArea, Vector2 screenSize, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            anchorMin = Vector2.zero;
            anchorMax = Vector2.one;

            if (!(screenSize.x > 0f) || !(screenSize.y > 0f)
                || !float.IsFinite(screenSize.x) || !float.IsFinite(screenSize.y)
                || !float.IsFinite(safeArea.x) || !float.IsFinite(safeArea.y)
                || !float.IsFinite(safeArea.width) || !float.IsFinite(safeArea.height)
                || !(safeArea.width > 0f) || !(safeArea.height > 0f))
            {
                return false;
            }

            anchorMin = safeArea.position / screenSize;
            anchorMax = (safeArea.position + safeArea.size) / screenSize;
            return true;
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);

            // Invalid screen: keep the current anchors and try again next frame (the last* values stay unchanged).
            if (!TryComputeAnchors(safeArea, screenSize, out Vector2 anchorMin, out Vector2 anchorMax))
            {
                return;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
