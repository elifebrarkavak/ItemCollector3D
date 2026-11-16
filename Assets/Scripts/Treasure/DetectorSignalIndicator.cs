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

        private float flashTimer;
        private AudioClip beep;

        public int PulseCount { get; private set; }

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
                flashGraphic.color = Color.Lerp(idleColor, flashColor, t);
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

            if (audioSource != null && beep != null)
            {
                audioSource.pitch = Mathf.Lerp(0.8f, 1.6f, proximity);
                audioSource.PlayOneShot(beep, 0.5f);
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
