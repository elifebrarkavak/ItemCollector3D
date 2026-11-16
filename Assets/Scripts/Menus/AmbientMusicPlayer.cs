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
        private static AudioClip CreatePad()
        {
            const int sampleRate = 22050;
            const float seconds = 8f;
            int count = Mathf.RoundToInt(sampleRate * seconds);
            var samples = new float[count];
            float[] notes = { 110f, 164.5f, 220f, 277.25f }; // A2, E3, A3, C#4-ish: a soft open chord
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float value = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float frequency = Mathf.Round(notes[n] * seconds) / seconds; // whole cycles per loop
                    float breathe = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * (n + 1) * t / seconds);
                    value += Mathf.Sin(2f * Mathf.PI * frequency * t) * breathe * (0.3f / (n + 1f) + 0.06f);
                }

                samples[i] = value * 0.5f;
            }

            AudioClip clip = AudioClip.Create("AmbientPad", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
