using ItemCollector3D.Audio;
using ItemCollector3D.Menus;
using ItemCollector3D.Treasure;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// The background atmosphere of Rocky Canyon (stage 15D), built like Green Mine's <see cref="AreaAmbience"/> and sharing its rules: it is the quietest layer, it follows Settings > SFX volume,
    /// it steps back while a dig runs and briefly after a reveal or a reward (and a little while the detector reads RAPID) so the detector beep and the DIG / COLLECT sounds stay clear, it is
    /// paused with everything else by <see cref="GamePause"/> (the music is a separate system and is never touched here) and it owns no gameplay. Green Mine's forest and cave loops are not
    /// involved at all.
    ///
    /// Six AudioSources in total, of which at most about four sound at once: ONE shared dry-wind bed whose volume, brightness (a low-pass: muffled in the ravine) and pitch follow the area, and one
    /// quiet "detail" loop per area (far birds and a creaking sign at the outpost, rustling scrub and sand in the riverbed, creaking timber and a ringing rail on the railway, whistling gusts at the
    /// overlook, a hollow echo in the ravine). The soft area weights of <see cref="CanyonZoneTracker"/> are smoothed again here (2 s), so crossing an area boundary is always a cross-fade and a sound
    /// is never cut. A detail source is only playing while its area is audible. The loops are synthesised (<see cref="SoundSynth"/>), one per frame after the start so no frame hitches.
    /// Whether it sounds pleasant on a device is NOT verified by code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CanyonAmbience : MonoBehaviour
    {
        [SerializeField] private CanyonZoneTracker zones;
        [SerializeField] private TreasureHuntController hunt;
        [SerializeField] private TreasureDetector detector;

        // The mix. Constants on purpose: a value serialized into the scene would win over a retuned default (see the stage 12A notes).
        /// <summary>Base volume of the wind bed and of every detail loop (Green Mine's forest is 0.2, the music pad about 0.28).</summary>
        public const float BaseVolume = 0.18f;

        /// <summary>Per area: how loud the wind is (light at the outpost, stronger in the open riverbed and at the overlook, soft in the ravine).</summary>
        public static readonly float[] WindGain = { 0.55f, 0.85f, 0.6f, 1.0f, 0.5f };

        /// <summary>Per area: the wind's low-pass cut-off in Hz (muffled and hollow in the ravine, open elsewhere).</summary>
        public static readonly float[] WindCutoff = { 12000f, 12000f, 9000f, 16000f, 700f };

        /// <summary>Per area: the wind's playback pitch (a little deeper in the ravine, a little thinner up high).</summary>
        public static readonly float[] WindPitch = { 1.0f, 1.0f, 0.96f, 1.06f, 0.85f };

        /// <summary>Per area: how loud its detail loop is.</summary>
        public static readonly float[] DetailGain = { 0.9f, 0.85f, 1.0f, 0.9f, 1.0f };

        public const float DiggingDuck = AreaAmbience.DiggingDuck;
        public const float RewardDuck = AreaAmbience.RewardDuck;
        public const float RapidDuck = 0.7f;
        public const float CrossfadeSeconds = 2f;
        public const float FadeInSeconds = 1.5f;
        private const float OpenCutoff = 22000f;
        private const float AudibleFrom = 0.01f;
        private const float SilentBelow = 0.003f;

        private AudioSource wind;
        private AudioLowPassFilter windFilter;
        private readonly AudioSource[] details = new AudioSource[CanyonZoneTracker.ZoneCount];
        private readonly AudioClip[] clips = new AudioClip[CanyonZoneTracker.ZoneCount + 1]; // [0] = wind, [1..5] = details
        private readonly float[] weights = new float[CanyonZoneTracker.ZoneCount];
        private readonly bool[] detailPlaying = new bool[CanyonZoneTracker.ZoneCount];
        private readonly int[] detailStarts = new int[CanyonZoneTracker.ZoneCount];
        private int clipsMade;
        private bool windPlaying;
        private bool initialised;
        private float master;
        private float duck = 1f;
        private float rewardDuckUntil;

        /// <summary>Current gain from ducking: 1 normally, lower while a dig runs, after a reveal / reward, and while the detector reads RAPID.</summary>
        public float Duck => duck;

        /// <summary>How many of the six loops have been synthesised so far (one per frame; all of them after the sixth frame).</summary>
        public int ClipsMade => clipsMade;

        public bool IsReady => clipsMade >= clips.Length;

        public AudioSource WindSource => wind;
        public AudioSource DetailSource(CanyonZone zone) => details[(int)zone];
        public float CurrentWindCutoff => windFilter != null && windFilter.enabled ? windFilter.cutoffFrequency : OpenCutoff;

        /// <summary>The smoothed weight (0 .. 1) this component mixes an area's detail loop with.</summary>
        public float SmoothedWeight(CanyonZone zone) => weights[(int)zone];

        public bool IsDetailPlaying(CanyonZone zone) => detailPlaying[(int)zone];

        public int PlayingSourceCount
        {
            get
            {
                int count = windPlaying ? 1 : 0;
                foreach (bool playing in detailPlaying)
                {
                    count += playing ? 1 : 0;
                }

                return count;
            }
        }

        private void Awake()
        {
            if (zones == null)
            {
                zones = FindAnyObjectByType<CanyonZoneTracker>();
            }

            if (hunt == null)
            {
                hunt = FindAnyObjectByType<TreasureHuntController>();
            }

            if (detector == null)
            {
                detector = FindAnyObjectByType<TreasureDetector>();
            }

            wind = CreateLoop("Ambience_Wind", out GameObject windObject);
            windFilter = windObject.AddComponent<AudioLowPassFilter>();
            windFilter.cutoffFrequency = OpenCutoff;
            windFilter.enabled = false;
            for (int z = 0; z < details.Length; z++)
            {
                details[z] = CreateLoop("Ambience_" + (CanyonZone)z, out _);
            }
        }

        private AudioSource CreateLoop(string objectName, out GameObject holder)
        {
            holder = new GameObject(objectName);
            holder.transform.SetParent(transform, false);
            AudioSource source = holder.AddComponent<AudioSource>();
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
        }

        private void OnDisable()
        {
            if (hunt != null)
            {
                hunt.DigCompleted -= HandleReveal;
                hunt.TreasureCollected -= HandleCollected;
            }

            StopAll();
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in clips)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
            }
        }

        private void StopAll()
        {
            if (wind != null)
            {
                wind.Stop();
            }

            windPlaying = false;
            for (int z = 0; z < details.Length; z++)
            {
                if (details[z] != null)
                {
                    details[z].Stop();
                }

                detailPlaying[z] = false;
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
            MakeNextClip();
            float dt = Time.deltaTime;
            for (int z = 0; z < weights.Length; z++)
            {
                float target = zones != null ? zones.Weight((CanyonZone)z) : 0f;
                weights[z] = initialised ? Mathf.MoveTowards(weights[z], target, dt / CrossfadeSeconds) : target; // starting anywhere (a teleport, a test) must not fade the wrong area in
            }

            initialised |= zones != null && zones.HasZone;
            master = Mathf.MoveTowards(master, 1f, dt / FadeInSeconds);

            float duckTarget = 1f;
            if (hunt != null && hunt.IsDigging)
            {
                duckTarget = DiggingDuck;
            }

            if (Time.unscaledTime < rewardDuckUntil)
            {
                duckTarget = Mathf.Min(duckTarget, RewardDuck);
            }

            if (detector != null && detector.HasSignal && detector.Proximity >= DetectorSignalIndicator.RapidFromProximity)
            {
                duckTarget = Mathf.Min(duckTarget, RapidDuck);
            }

            duck = Mathf.MoveTowards(duck, duckTarget, dt / 0.3f);
            ApplyMix();
        }

        /// <summary>Synthesises one loop per frame (wind first, then the five details), so loading the scene never hitches.</summary>
        private void MakeNextClip()
        {
            if (clipsMade >= clips.Length)
            {
                return;
            }

            AudioClip clip = clipsMade switch
            {
                0 => SoundSynth.CanyonWind(),
                1 => SoundSynth.OutpostDetail(),
                2 => SoundSynth.RiverbedDetail(),
                3 => SoundSynth.RailwayDetail(),
                4 => SoundSynth.OverlookDetail(),
                _ => SoundSynth.RavineDetail(),
            };
            clips[clipsMade] = clip;
            if (clipsMade == 0)
            {
                wind.clip = clip;
            }
            else
            {
                details[clipsMade - 1].clip = clip;
            }

            clipsMade++;
        }

        private void ApplyMix()
        {
            float gain = master * duck * GameSettings.SfxScale; // Settings > SFX Volume

            // the wind bed: one source, its loudness / brightness / pitch blended from the areas' values (normalised, so a cross-fade never dips)
            float total = Sum(weights);
            if (wind.clip != null && total > 0.001f)
            {
                float windVolume = 0f, pitch = 0f, logCutoff = 0f;
                for (int z = 0; z < weights.Length; z++)
                {
                    windVolume += weights[z] * WindGain[z];
                    pitch += weights[z] * WindPitch[z];
                    logCutoff += weights[z] * Mathf.Log(WindCutoff[z]);
                }

                float cutoff = Mathf.Exp(logCutoff / total);
                wind.volume = BaseVolume * (windVolume / total) * gain;
                wind.pitch = pitch / total;
                windFilter.enabled = cutoff < 15000f;
                if (windFilter.enabled)
                {
                    windFilter.cutoffFrequency = cutoff;
                }

                if (!windPlaying && enabled && !GamePause.IsPaused)
                {
                    wind.Play();
                    windPlaying = true;
                }
            }

            // the details: each plays only while its area is audible, and is mixed by its smoothed weight (smoothstep, so a cross-fade has no corner)
            for (int z = 0; z < details.Length; z++)
            {
                AudioSource source = details[z];
                if (source.clip == null)
                {
                    continue;
                }

                float w = weights[z];
                float t = w * w * (3f - 2f * w);
                source.volume = BaseVolume * DetailGain[z] * t * gain;
                if (!detailPlaying[z] && w > AudibleFrom && !GamePause.IsPaused)
                {
                    source.time = (detailStarts[z]++ * 0.37f % 1f) * source.clip.length * 0.9f; // a different part of the loop each time it comes back
                    source.Play();
                    detailPlaying[z] = true;
                }
                else if (detailPlaying[z] && w < SilentBelow && !GamePause.IsPaused)
                {
                    source.Stop();
                    detailPlaying[z] = false;
                }
            }
        }

        private static float Sum(float[] values)
        {
            float sum = 0f;
            foreach (float v in values)
            {
                sum += v;
            }

            return sum;
        }
    }
}
