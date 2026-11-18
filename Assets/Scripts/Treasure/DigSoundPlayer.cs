using ItemCollector3D.Menus;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Plays the short dig sound (one per stroke) through ONE AudioSource: a new play restarts the source instead of
    /// stacking, and a minimum repeat interval ignores accidental double triggers. Without an audio file it synthesises
    /// a short earthy thud + scrape; assign <c>digClip</c> later to use a real sound. Whether it is audible on a device
    /// is NOT verified by code - only that it is triggered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DigSoundPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [Tooltip("Optional real clip. Empty = built-in placeholder thud.")]
        [SerializeField] private AudioClip digClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.75f;
        [SerializeField, Min(0f)] private float minRepeatInterval = 0.05f;

        private AudioClip placeholder;
        private float lastPlayTime = float.NegativeInfinity;

        public int PlayCount { get; private set; }
        public float LastPitch { get; private set; }
        public float LastVolume { get; private set; }
        public bool LastUsedAssignedClip { get; private set; }
        public bool IsPlaying => source != null && source.isPlaying;
        public AudioSource Source => source;

        private void Awake()
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        /// <returns>true if a sound was started.</returns>
        public bool PlayStroke(int strokeIndex)
        {
            if (source == null || Time.unscaledTime - lastPlayTime < minRepeatInterval)
            {
                return false;
            }

            AudioClip clip = digClip;
            LastUsedAssignedClip = clip != null;
            if (clip == null)
            {
                placeholder ??= CreateThud();
                clip = placeholder;
            }

            lastPlayTime = Time.unscaledTime;
            LastPitch = strokeIndex % 2 == 0 ? 1f : 0.88f; // alternate strokes sound slightly different
            LastVolume = volume * GameSettings.SfxScale; // Settings > SFX Volume

            source.Stop();
            source.clip = clip;
            source.pitch = LastPitch;
            source.volume = LastVolume;
            source.Play();
            PlayCount++;
            return true;
        }

        private static AudioClip CreateThud()
        {
            const int sampleRate = 44100;
            const float seconds = 0.16f;
            int count = Mathf.CeilToInt(sampleRate * seconds);
            var samples = new float[count];
            var random = new System.Random(1234);
            float lowpass = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float thump = Mathf.Sin(2f * Mathf.PI * (110f - 300f * t) * t) * Mathf.Exp(-24f * t);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                lowpass += 0.25f * (noise - lowpass); // one-pole low-pass: dull scrape
                float scrape = lowpass * Mathf.Exp(-18f * t);
                samples[i] = (thump * 0.8f + scrape * 0.9f) * 0.7f;
            }

            AudioClip clip = AudioClip.Create("DigThud", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
