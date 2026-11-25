using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// Calm background music so the Music Volume setting controls something real. No audio files: it synthesises a short, seamless,
    /// quiet pad loop. A real track can be assigned to <c>musicClip</c> later. Keeps playing while the game is paused.
    /// Whether it sounds pleasant / audible on a device is NOT verified by code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientMusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [Tooltip("Optional real music. Empty = built-in placeholder pad.")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 0.35f;

        public float CurrentVolume => source != null ? source.volume : 0f;
        public bool IsPlaying => source != null && source.isPlaying;
        public AudioSource Source => source;

        private void Awake()
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.clip = musicClip != null ? musicClip : CreatePad();
        }

        private void OnEnable()
        {
            GameSettings.Current.Changed += ApplyVolume;
            ApplyVolume();
            if (source != null && source.clip != null && !source.isPlaying)
            {
                source.Play();
            }
        }

        private void OnDisable()
        {
            GameSettings.Current.Changed -= ApplyVolume;
        }

        private void ApplyVolume()
        {
            if (source != null)
            {
                source.volume = baseVolume * GameSettings.Current.MusicVolume;
            }
        }

        // 8 s loop at 22.05 kHz; every partial completes whole cycles in the loop, so it repeats without a click.
        // Stage 17B sound fix: the first pad was four pure sines at 110 / 165 / 220 / 277 Hz, all below 300 Hz, with 110 Hz the strongest: a steady low tone with its octave on top is the signature of an
        // electrical (mains) hum, and a phone speaker cannot play 110 Hz but buzzes on its harmonics. This pad sits higher (G3 B3 D4 G4, nothing on a 55 / 110 Hz harmonic), every note is a slightly
        // detuned PAIR (a slow 0.5 Hz chorus instead of a fixed tone), each note swells on its own cycle, and the whole thing is about 4 dB quieter. Still synthesised, still seamless.
        private static AudioClip CreatePad()
        {
            const int sampleRate = 22050;
            const float seconds = 8f;
            int count = Mathf.RoundToInt(sampleRate * seconds);
            var samples = new float[count];
            float[] notes = { 196f, 246.94f, 293.66f, 392f };   // G3, B3, D4, G4: an open, calm G major
            float[] gain = { 0.30f, 0.24f, 0.20f, 0.12f };
            float[] phase = { 0.4f, 2.0f, 3.6f, 5.2f };
            const float detune = 0.25f;                         // Hz either side: the pair beats at 0.5 Hz
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float value = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float low = Mathf.Round((notes[n] - detune) * seconds) / seconds; // whole cycles per loop
                    float high = Mathf.Round((notes[n] + detune) * seconds) / seconds;
                    float swell = 0.62f + 0.38f * Mathf.Sin(2f * Mathf.PI * (n + 1) * t / seconds + phase[n]);
                    float pair = Mathf.Sin(2f * Mathf.PI * low * t + phase[n]) + Mathf.Sin(2f * Mathf.PI * high * t);
                    value += pair * 0.5f * swell * gain[n];
                }

                samples[i] = value * 0.55f;
            }

            AudioClip clip = AudioClip.Create("AmbientPad", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
