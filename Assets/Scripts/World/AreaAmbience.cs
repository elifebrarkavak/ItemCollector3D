using ItemCollector3D.Audio;
using ItemCollector3D.Menus;
using ItemCollector3D.Treasure;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// The background atmosphere of an area: a light nature loop outdoors (wind, leaves, distant birds) and a calmer, echoing cave loop inside
    /// the mine. Both loops run all the time and are cross-faded by <see cref="MineInteriorZone.CameraBlend"/> (the same player-position blend the
    /// camera uses), smoothed again here so the change is gentle; outdoors is also low-passed (muffled) inside, as if heard through the tunnel mouth.
    ///
    /// Mixing: the ambience is the quietest layer. It follows Settings > SFX volume, steps back while a dig runs and briefly after a reveal or a
    /// reward so those sounds stay clear, and it is paused with everything else by <see cref="GamePause"/> (the music keeps playing; it is a separate
    /// system and is not touched here). It only reads state and owns no gameplay.
    /// Without audio files it synthesises its loops (<see cref="SoundSynth"/>); real loops can be assigned to the two clip fields.
    /// Whether it sounds pleasant on a device is NOT verified by code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AreaAmbience : MonoBehaviour
    {
        [SerializeField] private MineInteriorZone zone;
        [SerializeField] private TreasureHuntController hunt;

        [Header("Optional real loops (empty = built-in synthesised loops)")]
        [SerializeField] private AudioClip outdoorClip;
        [SerializeField] private AudioClip caveClip;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float outdoorVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float caveVolume = 0.24f;
        [Tooltip("How much of the outdoor loop is still heard deep inside the mine (through the tunnel mouth).")]
        [SerializeField, Range(0f, 1f)] private float outdoorShareInside = 0.12f;
        [Tooltip("Low-pass cut-off of the outdoor loop deep inside the mine (Hz). Fully open outdoors.")]
        [SerializeField, Range(200f, 8000f)] private float outdoorCutoffInside = 900f;
        [SerializeField, Min(0.1f)] private float crossfadeSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float fadeInSeconds = 1.5f;

        public const float DiggingDuck = 0.6f;
        public const float RewardDuck = 0.5f;
        private const float OpenCutoff = 22000f;

        private AudioSource outdoorSource;
        private AudioSource caveSource;
        private AudioLowPassFilter outdoorFilter;
        private AudioClip madeOutdoor;
        private AudioClip madeCave;
        private float blend;
        private float duck = 1f;
        private float master;
        private float rewardDuckUntil;
        private bool initialised;

        /// <summary>0 = outdoors .. 1 = deep inside the mine (the smoothed value the mix follows).</summary>
        public float InteriorBlend => blend;

        /// <summary>Current gain from ducking: 1 normally, lower while a dig runs or right after a reward.</summary>
        public float Duck => duck;

        public AudioSource OutdoorSource => outdoorSource;
        public AudioSource CaveSource => caveSource;
        public float OutdoorCutoff => outdoorFilter != null ? outdoorFilter.cutoffFrequency : OpenCutoff;

        private void Awake()
        {
            if (zone == null)
            {
                zone = FindAnyObjectByType<MineInteriorZone>();
            }

            if (hunt == null)
            {
                hunt = FindAnyObjectByType<TreasureHuntController>();
            }

            if (outdoorClip == null)
            {
                madeOutdoor = SoundSynth.ForestAmbience();
            }

            if (caveClip == null)
            {
                madeCave = SoundSynth.CaveAmbience();
            }

            outdoorSource = CreateLoop("Ambience_Outdoor", outdoorClip != null ? outdoorClip : madeOutdoor, out GameObject outdoorObject);
            outdoorFilter = outdoorObject.AddComponent<AudioLowPassFilter>();
            outdoorFilter.cutoffFrequency = OpenCutoff;
            outdoorFilter.enabled = false;
            caveSource = CreateLoop("Ambience_Cave", caveClip != null ? caveClip : madeCave, out _);
            ApplyMix();
        }

        private AudioSource CreateLoop(string objectName, AudioClip clip, out GameObject holder)
        {
            holder = new GameObject(objectName);
            holder.transform.SetParent(transform, false);
            AudioSource source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 180; // sound effects (128) win if the device runs out of voices
            return source;
        }

        private void OnEnable()
        {
            if (hunt != null)
            {
                hunt.DigCompleted += HandleReveal;
                hunt.TreasureCollected += HandleCollected;
            }

            if (outdoorSource != null && !outdoorSource.isPlaying)
            {
                outdoorSource.Play();
            }

            if (caveSource != null && !caveSource.isPlaying)
            {
                caveSource.Play();
            }
        }

        private void OnDisable()
        {
            if (hunt != null)
            {
                hunt.DigCompleted -= HandleReveal;
                hunt.TreasureCollected -= HandleCollected;
            }

            if (outdoorSource != null)
            {
                outdoorSource.Stop();
            }

            if (caveSource != null)
            {
                caveSource.Stop();
            }
        }

        private void OnDestroy()
        {
            if (madeOutdoor != null)
            {
                Destroy(madeOutdoor);
            }

            if (madeCave != null)
            {
                Destroy(madeCave);
            }
        }

        private void HandleReveal(TreasureSpot spot)
        {
            rewardDuckUntil = Mathf.Max(rewardDuckUntil, Time.unscaledTime + 0.8f);
        }

        private void HandleCollected(TreasureSpot spot, int gold)
        {
            rewardDuckUntil = Mathf.Max(rewardDuckUntil, Time.unscaledTime + 1.6f);
        }

        private void Update()
        {
            float target = zone != null ? zone.CameraBlend : 0f;
            if (!initialised)
            {
                blend = target; // starting inside the mine (a test, a teleport) must not fade the wrong loop in
                initialised = true;
            }

            float dt = Time.deltaTime;
            blend = Mathf.MoveTowards(blend, target, dt / crossfadeSeconds);
            master = Mathf.MoveTowards(master, 1f, dt / fadeInSeconds);

            float duckTarget = 1f;
            if (hunt != null && hunt.IsDigging)
            {
                duckTarget = DiggingDuck;
            }

            if (Time.unscaledTime < rewardDuckUntil)
            {
                duckTarget = Mathf.Min(duckTarget, RewardDuck);
            }

            duck = Mathf.MoveTowards(duck, duckTarget, dt / 0.3f);
            ApplyMix();
        }

        private void ApplyMix()
        {
            if (outdoorSource == null || caveSource == null)
            {
                return;
            }

            float t = blend * blend * (3f - 2f * blend); // smoothstep
            float gain = master * duck * GameSettings.SfxScale; // Settings > SFX Volume
            outdoorSource.volume = outdoorVolume * Mathf.Lerp(1f, outdoorShareInside, t) * gain;
            caveSource.volume = caveVolume * Mathf.Sin(0.5f * Mathf.PI * t) * gain;

            outdoorFilter.enabled = t > 0.001f;
            if (outdoorFilter.enabled)
            {
                outdoorFilter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoff), Mathf.Log(outdoorCutoffInside), t));
            }
        }
    }
}
