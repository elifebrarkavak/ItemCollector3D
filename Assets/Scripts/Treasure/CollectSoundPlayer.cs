using ItemCollector3D.Audio;
using ItemCollector3D.Menus;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Plays the collect sound through ONE AudioSource (a new play restarts it instead of stacking).
    /// Without audio files it synthesises a short gold-coin chime (<see cref="SoundSynth.CollectCoin"/>); Epic treasure gets a longer,
    /// richer one (<see cref="SoundSynth.CollectEpic"/>); a profile's own clip replaces both.
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
        private AudioClip epicChime;
        private float lastPlayTime = float.NegativeInfinity;

        public int PlayCount { get; private set; }
        public float LastPitch { get; private set; }
        public float LastVolume { get; private set; }
        public bool LastUsedProfileClip { get; private set; }

        /// <summary>True when the last sound was the special Epic one.</summary>
        public bool LastWasSpecial { get; private set; }
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

        private void OnDestroy()
        {
            if (placeholderChime != null)
            {
                Destroy(placeholderChime);
            }

            if (epicChime != null)
            {
                Destroy(epicChime);
            }
        }

        /// <param name="special">true for an Epic treasure: plays the longer, richer reward sound (unless the profile brings its own clip).</param>
        /// <returns>true if a sound was started.</returns>
        public bool Play(TreasureFeedbackProfile profile, float intensity, bool special = false)
        {
            if (source == null || Time.unscaledTime - lastPlayTime < minRepeatInterval)
            {
                return false;
            }

            AudioClip clip = profile != null ? profile.CollectSound : null;
            LastUsedProfileClip = clip != null;
            LastWasSpecial = false;
            if (clip == null && special)
            {
                if (epicChime == null)
                {
                    epicChime = SoundSynth.CollectEpic();
                }

                clip = epicChime;
                LastWasSpecial = true;
            }

            if (clip == null)
            {
                if (placeholderChime == null)
                {
                    placeholderChime = SoundSynth.CollectCoin();
                }

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
    }
}
