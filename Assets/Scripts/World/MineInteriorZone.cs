using ItemCollector3D.Player;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// Gameplay side of the Green Mine interior (the art lives under GreenMineEnvironment/MineInterior and holds no scripts). While the player is in or at
    /// the mouth of the tunnel / treasure room the third-person camera orbits closer (and cannot be squeezed into the player's head), and while the camera is
    /// inside the sky (ambient) light fades to a warm, dim underground tone; the lanterns and the sun shadowed by the tunnel shell do the rest. The sun and
    /// the fog are never touched, so the daylight seen through the mouth stays daylight. The ambient is restored exactly on the way out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineInteriorZone : MonoBehaviour
    {
        [Header("Volumes (world space: tunnel, treasure room)")]
        [SerializeField] private Bounds[] interiorBounds = System.Array.Empty<Bounds>();
        [SerializeField, Min(0f)] private float approachMargin = 1.0f;

        [Header("Camera")]
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField, Range(0.3f, 1f)] private float cameraDistanceScale = 0.7f;
        [SerializeField, Min(0f)] private float cameraMinimumDistance = 1.3f;

        [Header("Mood (ambient only: the sun and the fog stay outdoor values)")]
        [SerializeField] private Color ambientSky = new(0.56f, 0.44f, 0.34f);
        [SerializeField] private Color ambientEquator = new(0.46f, 0.35f, 0.26f);
        [SerializeField] private Color ambientGround = new(0.24f, 0.17f, 0.11f);
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.8f;

        private float cameraBlend;
        private float moodBlend;
        private Color baseSky, baseEquator, baseGround;
        private bool captured;

        /// <summary>0 outdoors .. 1 fully underground (the mood fade, driven by the camera position).</summary>
        public float MoodBlend => moodBlend;

        /// <summary>0 .. 1: how far the camera orbit is shortened for the mine (driven by the player position).</summary>
        public float CameraBlend => cameraBlend;

        public bool IsPlayerInside => PlayerInside(0f);

        private void Awake()
        {
            if (cameraController == null)
            {
                cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            }
        }

        private void Update()
        {
            if (cameraController == null)
            {
                return;
            }

            float step = Time.deltaTime / fadeSeconds;
            cameraBlend = Mathf.MoveTowards(cameraBlend, PlayerInside(approachMargin) ? 1f : 0f, step);
            cameraController.SetEnvironmentLimits(Mathf.Lerp(1f, cameraDistanceScale, cameraBlend), Mathf.Lerp(0f, cameraMinimumDistance, cameraBlend));

            Camera view = Camera.main;
            bool cameraInside = view != null && Contains(view.transform.position, 0f);
            float previous = moodBlend;
            moodBlend = Mathf.MoveTowards(moodBlend, cameraInside ? 1f : 0f, step);
            if (!Mathf.Approximately(previous, moodBlend) || (moodBlend > 0f && !captured))
            {
                ApplyMood();
            }
        }

        private void OnDisable()
        {
            if (cameraController != null)
            {
                cameraController.SetEnvironmentLimits(1f, 0f);
            }

            moodBlend = 0f;
            cameraBlend = 0f;
            if (captured)
            {
                ApplyMood();
            }
        }

        private bool PlayerInside(float margin)
        {
            Transform target = cameraController != null ? cameraController.Target : null;
            return target != null && Contains(target.position + Vector3.up, margin);
        }

        private bool Contains(Vector3 point, float margin)
        {
            foreach (Bounds b in interiorBounds)
            {
                b.Expand(margin * 2f);
                if (b.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyMood()
        {
            if (!captured)
            {
                baseSky = RenderSettings.ambientSkyColor;
                baseEquator = RenderSettings.ambientEquatorColor;
                baseGround = RenderSettings.ambientGroundColor;
                captured = true;
            }

            if (moodBlend <= 0f)
            {
                // fully outdoors again: write the captured values back verbatim and forget them, so the next visit captures whatever the outdoors is then
                RenderSettings.ambientSkyColor = baseSky;
                RenderSettings.ambientEquatorColor = baseEquator;
                RenderSettings.ambientGroundColor = baseGround;
                captured = false;
                return;
            }

            float t = moodBlend * moodBlend * (3f - 2f * moodBlend);
            RenderSettings.ambientSkyColor = Color.Lerp(baseSky, ambientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(baseEquator, ambientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(baseGround, ambientGround, t);
        }
    }
}
