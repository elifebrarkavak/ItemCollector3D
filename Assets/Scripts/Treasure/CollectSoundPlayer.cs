using ItemCollector3D.Menus;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Plays the collect sound through ONE AudioSource (a new play restarts it instead of stacking).
    /// Without audio files it synthesises a short placeholder chime; a profile's own clip replaces it.
    /// Pitch and loudness come from the feedback profile. Whether it is audible on a device is NOT verified by code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CollectSoundPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float minVolumeFactor = 0.6f;
        [Tooltip("A second Play within this many seconds is ignored (prevents stacked duplicates).")]
        [SerializeField, Min(0f)] private float minRepeatInterval = 0.05f;

        private AudioClip placeholderChime;
        private float lastPlayTime = float.NegativeInfinity;

        public int PlayCount { get; private set; }
        public float LastPitch { get; private set; }
        public float LastVolume { get; private set; }
        public bool LastUsedProfileClip { get; private set; }
        public bool IsPlaying => source != null && source.isPlaying;

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
        public bool Play(TreasureFeedbackProfile profile, float intensity)
        {
            if (source == null || Time.unscaledTime - lastPlayTime < minRepeatInterval)
            {
                return false;
            }

            AudioClip clip = profile != null ? profile.CollectSound : null;
            LastUsedProfileClip = clip != null;
            if (clip == null)
            {
                placeholderChime ??= CreateChime();
                clip = placeholderChime;
            }

            lastPlayTime = Time.unscaledTime;
            LastPitch = profile != null ? profile.SoundPitch : 1f;
            LastVolume = baseVolume * Mathf.Lerp(minVolumeFactor, 1f, Mathf.Clamp01(intensity)) * GameSettings.SfxScale; // Settings > SFX Volume

            source.Stop();
            source.clip = clip;
            source.pitch = LastPitch;
            source.volume = LastVolume;
            source.Play();
            PlayCount++;
            return true;
        }

        private static AudioClip CreateChime()
        {
            const int sampleRate = 44100;
            const float seconds = 0.22f;
            const float frequency = 660f;
            int count = Mathf.CeilToInt(sampleRate * seconds);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-14f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t)
                             + 0.5f * Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * t);
                samples[i] = tone * envelope * 0.6f;
            }

            AudioClip clip = AudioClip.Create("CollectChime", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
