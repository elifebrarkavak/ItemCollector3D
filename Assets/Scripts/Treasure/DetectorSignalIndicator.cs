using ItemCollector3D.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Feedback for detector pulses: a flashing dot, a status label (NO SIGNAL / SLOW / FAST / RAPID) with a three-step level meter, and a
    /// procedural beep (no audio assets needed). It only displays what <see cref="TreasureDetector"/> reports: there is no second signal system.
    /// Lives in the gameplay scene, not in the shared HUD prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DetectorSignalIndicator : MonoBehaviour
    {
        [SerializeField] private TreasureDetector detector;
        [SerializeField] private Graphic flashGraphic;
        [SerializeField] private TMP_Text label;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Min(0.02f)] private float flashDuration = 0.12f;
        [SerializeField] private Color idleColor = new(0.35f, 0.35f, 0.35f, 0.8f);
        [SerializeField] private Color flashColor = new(1f, 0.85f, 0.1f, 1f);

        [Header("Level display (NO SIGNAL / SLOW / FAST / RAPID)")]
        [Tooltip("Level meter: the first bar is lit from SLOW, the second from FAST, the third at RAPID.")]
        [SerializeField] private Graphic[] levelBars;
        [SerializeField] private Color unlitBarColor = new(0.38f, 0.47f, 0.62f, 0.85f);
        [SerializeField] private Color noSignalColor = new(1f, 0.94f, 0.83f, 0.55f);
        [SerializeField] private Color slowColor = new(0.42f, 0.86f, 0.66f, 1f);
        [SerializeField] private Color fastColor = new(1f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color rapidColor = new(1f, 0.5f, 0.28f, 1f);

        [Header("Signal strength by proximity (Concept Document 2.7: weak far, stronger mid, strongest close)")]
        [Tooltip("Pulse strength at the edge of detection range (proximity 0). Strength grows to 1 when very close.")]
        [SerializeField, Range(0.05f, 1f)] private float weakestStrength = 0.35f;
        [Tooltip("Dot scale at full strength while it flashes (1 = no scale pulse).")]
        [SerializeField, Min(1f)] private float maxPulseScale = 1.6f;
        [SerializeField, Range(0f, 1f)] private float weakestBeepVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float strongestBeepVolume = 0.7f;

        public enum SignalLevel
        {
            None,
            Slow,
            Fast,
            Rapid,
        }

        /// <summary>From this proximity (0..1, see <see cref="TreasureDetector.Proximity"/>) the pulses count as FAST (about one a second).</summary>
        public const float FastFromProximity = 0.4f;

        /// <summary>From this proximity the pulses count as RAPID (about two a second or more).</summary>
        public const float RapidFromProximity = 0.75f;

        private float flashTimer;
        private float flashStrength;
        private AudioClip beep;
        private int appliedLevel = -1;

        public int PulseCount { get; private set; }

        /// <summary>Strength (0..1) of the most recent pulse: weak far away, strong when close.</summary>
        public float LastPulseStrength { get; private set; }

        /// <summary>Volume scale of the most recent beep (proximity strength x Settings SFX volume).</summary>
        public float LastBeepVolume { get; private set; }

        /// <summary>The level the HUD shows for the detector's current reading.</summary>
        public SignalLevel Level { get; private set; }

        /// <summary>How many level-meter bars are lit: 0 (NO SIGNAL), 1 (SLOW), 2 (FAST), 3 (RAPID).</summary>
        public int LitBarCount => levelBars == null ? 0 : Mathf.Min((int)Level, levelBars.Length);

        public static float StrengthFor(float proximity, float weakest)
        {
            return Mathf.Lerp(Mathf.Clamp01(weakest), 1f, Mathf.Clamp01(proximity));
        }

        public static SignalLevel LevelFor(bool hasSignal, float proximity)
        {
            if (!hasSignal)
            {
                return SignalLevel.None;
            }

            if (proximity >= RapidFromProximity)
            {
                return SignalLevel.Rapid;
            }

            return proximity >= FastFromProximity ? SignalLevel.Fast : SignalLevel.Slow;
        }

        public static string LabelFor(SignalLevel level)
        {
            return level switch
            {
                SignalLevel.Slow => "SLOW",
                SignalLevel.Fast => "FAST",
                SignalLevel.Rapid => "RAPID",
                _ => "NO SIGNAL",
            };
        }

        private void Awake()
        {
            beep = CreateBeep();
            if (flashGraphic != null)
            {
                flashGraphic.color = idleColor;
            }

            ApplyLevel(SignalLevel.None);
        }

        private void OnEnable()
        {
            if (detector != null)
            {
                detector.Pulsed += HandlePulse;
            }
        }

        private void OnDisable()
        {
            if (detector != null)
            {
                detector.Pulsed -= HandlePulse;
            }
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
            }

            if (flashGraphic != null)
            {
                float t = flashDuration > 0f ? Mathf.Clamp01(flashTimer / flashDuration) : 0f;
                float amount = t * flashStrength;
                flashGraphic.color = Color.Lerp(idleColor, flashColor, amount);
                flashGraphic.rectTransform.localScale = Vector3.one * (1f + (maxPulseScale - 1f) * amount);
            }

            if (detector != null)
            {
                ApplyLevel(LevelFor(detector.HasSignal, detector.Proximity));
            }
        }

        private void ApplyLevel(SignalLevel level)
        {
            Level = level;
            if (appliedLevel == (int)level)
            {
                return;
            }

            appliedLevel = (int)level;
            Color levelColor = level switch
            {
                SignalLevel.Slow => slowColor,
                SignalLevel.Fast => fastColor,
                SignalLevel.Rapid => rapidColor,
                _ => noSignalColor,
            };

            if (label != null)
            {
                label.text = LabelFor(level);
                label.color = levelColor;
            }

            if (levelBars != null)
            {
                for (int i = 0; i < levelBars.Length; i++)
                {
                    if (levelBars[i] != null)
                    {
                        levelBars[i].color = (int)level > i ? levelColor : unlitBarColor;
                    }
                }
            }
        }

        private void HandlePulse(float proximity)
        {
            PulseCount++;
            flashTimer = flashDuration;
            flashStrength = StrengthFor(proximity, weakestStrength);
            LastPulseStrength = flashStrength;

            if (audioSource != null && beep != null)
            {
                audioSource.pitch = Mathf.Lerp(0.8f, 1.6f, proximity);
                LastBeepVolume = Mathf.Lerp(weakestBeepVolume, strongestBeepVolume, flashStrength) * GameSettings.SfxScale; // Settings > SFX Volume
                audioSource.PlayOneShot(beep, LastBeepVolume);
            }
        }

        private static AudioClip CreateBeep()
        {
            const int sampleRate = 44100;
            const float seconds = 0.07f;
            const float frequency = 880f;
            int sampleCount = Mathf.CeilToInt(sampleRate * seconds);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / sampleCount;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }

            AudioClip clip = AudioClip.Create("DetectorBeep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
