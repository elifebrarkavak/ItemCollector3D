using System;
using UnityEngine;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// Fits a RectTransform to <see cref="Screen.safeArea"/> (display cut-outs, camera holes, system bars) and, optionally, keeps
    /// tappable content clear of the Android system gesture zones: Back swipes from the left and right edges and the Home swipe at
    /// the bottom are NOT part of the safe area, so a margin of about 22 dp (5.5 % of a phone's landscape height, the same on every
    /// phone) is taken off the left, right and bottom edges. Anything that is only decoration (dim panels, vignettes) lives outside
    /// this root and keeps bleeding to the screen edge.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>Test hook: replaces <see cref="Screen.safeArea"/> (a simulated notch or gesture bar). Null in the game.</summary>
        public static Func<Rect> SafeAreaOverride;

        [Tooltip("Share of the screen height kept free at the left, right and bottom edges for the Android system gestures (0 = off).")]
        [SerializeField, Range(0f, 0.12f)] private float gestureMarginFraction;

        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private static Rect CurrentSafeArea => SafeAreaOverride != null ? SafeAreaOverride() : Screen.safeArea;

        public float GestureMarginFraction => gestureMarginFraction;

        private void Update()
        {
            if (lastSafeArea != CurrentSafeArea
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

        /// <summary>Takes <paramref name="margin"/> pixels off the left, right and bottom of a safe area (never more than a quarter of its size).</summary>
        public static Rect ShrinkForGestures(Rect safeArea, float margin)
        {
            if (!(margin > 0f) || !float.IsFinite(margin))
            {
                return safeArea;
            }

            float horizontal = Mathf.Min(margin, safeArea.width * 0.25f);
            float bottom = Mathf.Min(margin, safeArea.height * 0.25f);
            return Rect.MinMaxRect(safeArea.xMin + horizontal, safeArea.yMin + bottom, safeArea.xMax - horizontal, safeArea.yMax);
        }

        private void ApplySafeArea()
        {
            var screenSize = new Vector2(Screen.width, Screen.height);
            Rect safeArea = ShrinkForGestures(CurrentSafeArea, gestureMarginFraction * Screen.height);

            // Invalid screen: keep the current anchors and try again next frame (the last* values stay unchanged).
            if (!TryComputeAnchors(safeArea, screenSize, out Vector2 anchorMin, out Vector2 anchorMax))
            {
                return;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = CurrentSafeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
