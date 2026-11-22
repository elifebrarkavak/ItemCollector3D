using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// A small mood change per area (stage 15D), on top of the canyon's sun / ambient / fog: the Hidden Ravine is a little cooler, dimmer and hazier (a closed, mysterious place), the
    /// Canyon Overlook has clearer air (the far mesas stay readable). Everything else keeps the values the scene was built with. Only RenderSettings (ambient colours and fog colour / density)
    /// are touched, the sun and the shadows never, no light is added, and the base values are captured when the component is enabled and written back verbatim when it is disabled.
    /// The area weights come from <see cref="CanyonZoneTracker"/> and are smoothed here (about 1.5 s), so the change is gradual; the render settings are only written while the blend moves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CanyonZoneMood : MonoBehaviour
    {
        [SerializeField] private CanyonZoneTracker zones;

        [Header("Hidden Ravine (a closed, cooler, hazier place)")]
        [SerializeField] private Color ravineSky = new(0.46f, 0.50f, 0.70f);
        [SerializeField] private Color ravineEquator = new(0.74f, 0.55f, 0.50f);
        [SerializeField] private Color ravineGround = new(0.36f, 0.26f, 0.26f);
        [SerializeField] private Color ravineFog = new(0.78f, 0.60f, 0.56f);
        [SerializeField, Min(0.1f)] private float ravineFogDensity = 1.5f;

        [Header("Canyon Overlook (clearer air)")]
        [SerializeField, Min(0.1f)] private float overlookFogDensity = 0.7f;

        [SerializeField, Min(0.1f)] private float smoothSeconds = 1.5f;

        private Color baseSky, baseEquator, baseGround, baseFog;
        private float baseDensity;
        private bool captured;
        private float ravine, overlook;
        private float lastRavine = -1f, lastOverlook = -1f;

        /// <summary>0 .. 1: how much the ravine mood is applied right now.</summary>
        public float RavineBlend => ravine;

        /// <summary>0 .. 1: how much the overlook's clearer air is applied right now.</summary>
        public float OverlookBlend => overlook;

        private void Awake()
        {
            if (zones == null)
            {
                zones = FindAnyObjectByType<CanyonZoneTracker>();
            }
        }

        private void OnEnable()
        {
            baseSky = RenderSettings.ambientSkyColor;
            baseEquator = RenderSettings.ambientEquatorColor;
            baseGround = RenderSettings.ambientGroundColor;
            baseFog = RenderSettings.fogColor;
            baseDensity = RenderSettings.fogDensity;
            captured = true;
            ravine = overlook = 0f;
            lastRavine = lastOverlook = -1f;
        }

        private void OnDisable()
        {
            if (!captured)
            {
                return;
            }

            RenderSettings.ambientSkyColor = baseSky;
            RenderSettings.ambientEquatorColor = baseEquator;
            RenderSettings.ambientGroundColor = baseGround;
            RenderSettings.fogColor = baseFog;
            RenderSettings.fogDensity = baseDensity;
            captured = false;
        }

        private void Update()
        {
            if (zones == null || !zones.HasZone)
            {
                return;
            }

            float step = Time.unscaledDeltaTime / smoothSeconds;
            ravine = Mathf.MoveTowards(ravine, zones.Weight(CanyonZone.Ravine), step);
            overlook = Mathf.MoveTowards(overlook, zones.Weight(CanyonZone.Overlook), step);
            if (Mathf.Approximately(ravine, lastRavine) && Mathf.Approximately(overlook, lastOverlook))
            {
                return;
            }

            lastRavine = ravine;
            lastOverlook = overlook;
            float r = ravine * ravine * (3f - 2f * ravine);
            float o = overlook * overlook * (3f - 2f * overlook);
            RenderSettings.ambientSkyColor = Color.Lerp(baseSky, ravineSky, r);
            RenderSettings.ambientEquatorColor = Color.Lerp(baseEquator, ravineEquator, r);
            RenderSettings.ambientGroundColor = Color.Lerp(baseGround, ravineGround, r);
            RenderSettings.fogColor = Color.Lerp(baseFog, ravineFog, r);
            RenderSettings.fogDensity = baseDensity * Mathf.Lerp(1f, ravineFogDensity, r) * Mathf.Lerp(1f, overlookFogDensity, o);
        }
    }
}
