using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// Soft scene transition: every scene opens from black (a short fade-in) so Main Menu -> gameplay -> Main Menu never pops.
    /// One tiny persistent overlay is created by code; it never blocks input (no raycast target) and costs one Image.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        public const float FadeSeconds = 0.4f;

        private static ScreenFader instance;
        private Image image;
        private float alpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenFader>();
            instance.Build();
            SceneManager.sceneLoaded += (_, _) => instance.StartFade();
            instance.StartFade();
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;
            var imageObject = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image = imageObject.GetComponent<Image>();
            image.color = new Color(0.03f, 0.05f, 0.09f, 0f);
            image.raycastTarget = false;
        }

        private void StartFade()
        {
            alpha = 1f;
            Apply();
        }

        private void Update()
        {
            if (alpha <= 0f)
            {
                return;
            }

            alpha = Mathf.Max(0f, alpha - Mathf.Min(Time.unscaledDeltaTime, 0.05f) / FadeSeconds);
            Apply();
        }

        private void Apply()
        {
            if (image != null)
            {
                float eased = alpha * alpha;
                image.color = new Color(0.03f, 0.05f, 0.09f, eased);
                image.enabled = eased > 0.002f;
            }
        }
    }
}
