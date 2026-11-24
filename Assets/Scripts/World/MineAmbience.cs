using ItemCollector3D.Audio;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Treasure;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// The background sound of the Ancient Mine (stage 16D), the same design as Rocky Canyon's <see cref="CanyonAmbience"/> and with the same rules: the quietest layer, follows Settings > SFX volume,
    /// steps back while a dig runs, after a reveal / reward and while the detector reads RAPID (so beep, DIG and COLLECT stay clear), pauses with <see cref="GamePause"/> (the music is a separate
    /// system, never touched here), owns no gameplay. Seven AudioSources, at most about four sound at once: ONE shared cave bed whose volume / brightness / pitch follow the area (open at the gate,
    /// dull and deep in the Royal Vault), one quiet detail loop per area, and the grotto's water, which only plays near the grotto. The zone weights of <see cref="MineZoneTracker"/> are smoothed
    /// here (2 s), so crossing a border is always a cross-fade, and walking back and forth over one never cuts a sound (a source stops only after its weight has been ~0 for good).
    /// The first time the player enters the Royal Vault the vault layer swells (about 8 s) so the change is felt. The loops are synthesised (<see cref="SoundSynth"/>), one per frame at the start.
    /// Whether it sounds good on a device is NOT verified by code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineAmbience : MonoBehaviour
    {
        [SerializeField] private MineZoneTracker zones;
        [SerializeField] private TreasureHuntController hunt;
        [SerializeField] private TreasureDetector detector;
        [SerializeField] private Transform grotto;

        /// <summary>Base volume of the bed and of every detail loop (Canyon 0.18, Green Mine's forest 0.2, the music pad about 0.28).</summary>
        public const float BaseVolume = 0.17f;

        /// <summary>Per area: how loud the bed is (the Vault carries the deepest, fullest one).</summary>
        public static readonly float[] BedGain = { 0.65f, 0.8f, 0.75f, 0.7f, 1.0f };

        /// <summary>Per area: the bed's low-pass cut-off in Hz (open at the gate, hollow and dark in the vault).</summary>
        public static readonly float[] BedCutoff = { 9000f, 5500f, 12000f, 7000f, 1900f };

        /// <summary>Per area: the bed's playback pitch (a little deeper in the vault, a little brighter in the caverns).</summary>
        public static readonly float[] BedPitch = { 1.0f, 0.9f, 1.1f, 0.95f, 0.8f };

        /// <summary>Per area: how loud its detail loop is.</summary>
        public static readonly float[] DetailGain = { 0.9f, 0.9f, 0.7f, 0.9f, 1.05f };

        /// <summary>The grotto water's gain relative to <see cref="BaseVolume"/>.</summary>
        public const float WaterGain = 0.7f;

        public const float DiggingDuck = AreaAmbience.DiggingDuck;
        public const float RewardDuck = AreaAmbience.RewardDuck;
        public const float RapidDuck = 0.7f;
        public const float CrossfadeSeconds = 2f;
        public const float FadeInSeconds = 1.5f;
        public const float VaultSwellSeconds = 8f;
        public const float VaultSwellBoost = 0.3f;
        public const float GrottoHearingRadius = 18f;
        public const float GrottoFullRadius = 5f;
        private const float OpenCutoff = 22000f;
        private const float AudibleFrom = 0.01f;
        private const float SilentBelow = 0.003f;
        private const int ClipTotal = MineZoneTracker.ZoneCount + 2; // [0] bed, [1..5] details, [6] grotto water

        private AudioSource bed;
        private AudioLowPassFilter bedFilter;
        private AudioSource water;
        private readonly AudioSource[] details = new AudioSource[MineZoneTracker.ZoneCount];
        private readonly AudioClip[] clips = new AudioClip[ClipTotal];
        private readonly float[] weights = new float[MineZoneTracker.ZoneCount];
        private readonly bool[] detailPlaying = new bool[MineZoneTracker.ZoneCount];
        private readonly int[] detailStarts = new int[MineZoneTracker.ZoneCount];
        private int clipsMade;
        private bool bedPlaying, waterPlaying, initialised;
        private float master, duck = 1f, rewardDuckUntil;
        private float waterWeight, swellStart = float.NegativeInfinity;
        private Transform player;

        /// <summary>Current gain from ducking: 1 normally, lower while a dig runs, after a reveal / reward, and while the detector reads RAPID.</summary>
        public float Duck => duck;

        public int ClipsMade => clipsMade;

        public bool IsReady => clipsMade >= clips.Length;

        public AudioSource BedSource => bed;
        public AudioSource WaterSource => water;
        public AudioSource DetailSource(MineZone zone) => details[(int)zone];
        public float CurrentBedCutoff => bedFilter != null && bedFilter.enabled ? bedFilter.cutoffFrequency : OpenCutoff;
        public float SmoothedWeight(MineZone zone) => weights[(int)zone];
        public bool IsDetailPlaying(MineZone zone) => detailPlaying[(int)zone];
        public bool IsWaterPlaying => waterPlaying;

        /// <summary>0 .. 1: how much of the Royal Vault's first-entry swell is left.</summary>
        public float VaultSwell
        {
            get
            {
                float left = Mathf.Clamp01(1f - (Time.time - swellStart) / VaultSwellSeconds);
                return left * left;
            }
        }

        public int PlayingSourceCount
        {
            get
            {
                int count = (bedPlaying ? 1 : 0) + (waterPlaying ? 1 : 0);
                foreach (bool playing in detailPlaying)
                {
                    count += playing ? 1 : 0;
                }

                return count;
            }
        }

        /// <summary>The loudest the ambience can ever be at once (every source at its own full gain, no ducking), for tests and tuning.</summary>
        public static float LoudestPossible => BaseVolume * (Max(BedGain) + Max(DetailGain) * (1f + VaultSwellBoost) + WaterGain);

        private static float Max(float[] values)
        {
            float max = 0f;
            foreach (float v in values)
            {
                max = Mathf.Max(max, v);
            }

            return max;
        }

        private void Awake()
        {
            if (zones == null)
            {
                zones = FindAnyObjectByType<MineZoneTracker>();
            }

            if (hunt == null)
            {
                hunt = FindAnyObjectByType<TreasureHuntController>();
            }

            if (detector == null)
            {
                detector = FindAnyObjectByType<TreasureDetector>();
            }

            bed = CreateLoop("Ambience_Bed", out GameObject bedObject);
            bedFilter = bedObject.AddComponent<AudioLowPassFilter>();
            bedFilter.cutoffFrequency = OpenCutoff;
            bedFilter.enabled = false;
            for (int z = 0; z < details.Length; z++)
            {
                details[z] = CreateLoop("Ambience_" + (MineZone)z, out _);
            }

            water = CreateLoop("Ambience_GrottoWater", out _);
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

            if (zones != null)
            {
                zones.ZoneEntered += HandleEntered;
            }
        }

        private void OnDisable()
        {
            if (hunt != null)
            {
                hunt.DigCompleted -= HandleReveal;
                hunt.TreasureCollected -= HandleCollected;
            }

            if (zones != null)
            {
                zones.ZoneEntered -= HandleEntered;
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
            if (bed != null)
            {
                bed.Stop();
            }

            if (water != null)
            {
                water.Stop();
            }

            bedPlaying = waterPlaying = false;
            for (int z = 0; z < details.Length; z++)
            {
                if (details[z] != null)
                {
                    details[z].Stop();
                }

                detailPlaying[z] = false;
            }
        }

        private void HandleReveal(TreasureSpot spot) => rewardDuckUntil = Mathf.Max(rewardDuckUntil, Time.unscaledTime + 0.8f);

        private void HandleCollected(TreasureSpot spot, int gold) => rewardDuckUntil = Mathf.Max(rewardDuckUntil, Time.unscaledTime + 1.6f);

        private void HandleEntered(MineZone zone)
        {
            if (zone == MineZone.Vault && zones.Visits(MineZone.Vault) == 1)
            {
                swellStart = Time.time; // the very first time: the vault announces itself
            }
        }

        private void Update()
        {
            MakeNextClip();
            float dt = Time.deltaTime;
            for (int z = 0; z < weights.Length; z++)
            {
                float target = zones != null ? zones.Weight((MineZone)z) : 0f;
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
            UpdateWater(dt);
            ApplyMix();
        }

        private void UpdateWater(float dt)
        {
            float target = 0f;
            if (grotto != null && zones != null && zones.HasZone)
            {
                if (player == null)
                {
                    var controller = FindAnyObjectByType<ThirdPersonCameraController>();
                    player = controller != null ? controller.Target : null;
                }

                if (player != null)
                {
                    Vector3 d = player.position - grotto.position;
                    float distance = Mathf.Sqrt(d.x * d.x + d.z * d.z) + Mathf.Abs(d.y) * 2f; // another floor above or below does not hear it
                    target = Mathf.Clamp01((GrottoHearingRadius - distance) / (GrottoHearingRadius - GrottoFullRadius));
                }
            }

            waterWeight = initialised ? Mathf.MoveTowards(waterWeight, target, dt / CrossfadeSeconds) : target;
        }

        /// <summary>Synthesises one loop per frame (bed first, then the five details, then the water), so loading the scene never hitches.</summary>
        private void MakeNextClip()
        {
            if (clipsMade >= clips.Length)
            {
                return;
            }

            AudioClip clip = clipsMade switch
            {
                0 => SoundSynth.MineBed(),
                1 => SoundSynth.GateDetail(),
                2 => SoundSynth.HallDetail(),
                3 => SoundSynth.CavernsDetail(),
                4 => SoundSynth.WorkshopDetail(),
                5 => SoundSynth.VaultDetail(),
                _ => SoundSynth.GrottoWater(),
            };
            clips[clipsMade] = clip;
            if (clipsMade == 0)
            {
                bed.clip = clip;
            }
            else if (clipsMade <= details.Length)
            {
                details[clipsMade - 1].clip = clip;
            }
            else
            {
                water.clip = clip;
            }

            clipsMade++;
        }

        private void ApplyMix()
        {
            float gain = master * duck * GameSettings.SfxScale; // Settings > SFX Volume
            float swell = VaultSwell;

            // the bed: one source, loudness / brightness / pitch blended from the areas' values (normalised, so a cross-fade never dips)
            float total = 0f;
            foreach (float w in weights)
            {
                total += w;
            }

            if (bed.clip != null && total > 0.001f)
            {
                float volume = 0f, pitch = 0f, logCutoff = 0f;
                for (int z = 0; z < weights.Length; z++)
                {
                    volume += weights[z] * BedGain[z];
                    pitch += weights[z] * BedPitch[z];
                    logCutoff += weights[z] * Mathf.Log(BedCutoff[z]);
                }

                float cutoff = Mathf.Exp(logCutoff / total);
                bed.volume = BaseVolume * (volume / total) * gain;
                bed.pitch = pitch / total;
                bedFilter.enabled = cutoff < 15000f;
                if (bedFilter.enabled)
                {
                    bedFilter.cutoffFrequency = cutoff;
                }

                if (!bedPlaying && enabled && !GamePause.IsPaused)
                {
                    bed.Play();
                    bedPlaying = true;
                }
            }

            // the details: each plays only while its area is audible, mixed by its smoothed weight (smoothstep, so a cross-fade has no corner)
            for (int z = 0; z < details.Length; z++)
            {
                AudioSource source = details[z];
                if (source.clip == null)
                {
                    continue;
                }

                float w = weights[z];
                float t = w * w * (3f - 2f * w);
                float boost = z == (int)MineZone.Vault ? 1f + VaultSwellBoost * swell : 1f;
                source.volume = BaseVolume * DetailGain[z] * boost * t * gain;
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

            if (water.clip != null)
            {
                float t = waterWeight * waterWeight * (3f - 2f * waterWeight);
                water.volume = BaseVolume * WaterGain * t * gain;
                if (!waterPlaying && waterWeight > AudibleFrom && !GamePause.IsPaused)
                {
                    water.Play();
                    waterPlaying = true;
                }
                else if (waterPlaying && waterWeight < SilentBelow && !GamePause.IsPaused)
                {
                    water.Stop();
                    waterPlaying = false;
                }
            }
        }
    }
}
