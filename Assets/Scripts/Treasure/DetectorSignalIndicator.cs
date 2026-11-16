using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Minimal feedback for detector pulses: a flashing dot, a status label and a procedural beep
    /// (no audio assets needed). Lives in the gameplay scene, not in the shared HUD prefab.
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

        [Header("Signal strength by proximity (Concept Document 2.7: weak far, stronger mid, strongest close)")]
        [Tooltip("Pulse strength at the edge of detection range (proximity 0). Strength grows to 1 when very close.")]
        [SerializeField, Range(0.05f, 1f)] private float weakestStrength = 0.35f;
        [Tooltip("Dot scale at full strength while it flashes (1 = no scale pulse).")]
        [SerializeField, Min(1f)] private float maxPulseScale = 1.6f;
        [SerializeField, Range(0f, 1f)] private float weakestBeepVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float strongestBeepVolume = 0.7f;

        private float flashTimer;
        private float flashStrength;
        private AudioClip beep;

        public int PulseCount { get; private set; }

        /// <summary>Strength (0..1) of the most recent pulse: weak far away, strong when close.</summary>
        public float LastPulseStrength { get; private set; }

        public static float StrengthFor(float proximity, float weakest)
        {
            return Mathf.Lerp(Mathf.Clamp01(weakest), 1f, Mathf.Clamp01(proximity));
        }

        private void Awake()
        {
            beep = CreateBeep();
            if (flashGraphic != null)
            {
                flashGraphic.color = idleColor;
            }
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

            if (label != null && detector != null)
            {
                label.text = detector.HasSignal
                    ? $"SIGNAL {Mathf.RoundToInt(detector.Proximity * 100f)}%"
                    : "NO SIGNAL";
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
                audioSource.PlayOneShot(beep, Mathf.Lerp(weakestBeepVolume, strongestBeepVolume, flashStrength));
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
