using ItemCollector3D.Audio;
using ItemCollector3D.Menus;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Plays the short dig sound (one per stroke) through ONE AudioSource: a new play restarts the source instead of
    /// stacking, and a minimum repeat interval ignores accidental double triggers. Without an audio file it synthesises
    /// a short, soft earthy thud + scrape (<see cref="SoundSynth.DigStroke"/>); assign <c>digClip</c> later to use a real sound.
    /// The "treasure revealed" cue (<see cref="PlayReveal"/>) has its own AudioSource, so it never cuts the last stroke short.
    /// Whether it is audible on a device is NOT verified by code - only that it is triggered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DigSoundPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [Tooltip("Optional real clip. Empty = built-in placeholder thud.")]
        [SerializeField] private AudioClip digClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.75f;
        [SerializeField, Min(0f)] private float minRepeatInterval = 0.05f;
        [Tooltip("Optional real clip for the treasure reveal. Empty = built-in soft two-note discovery sound.")]
        [SerializeField] private AudioClip revealClip;
        [SerializeField, Range(0f, 1f)] private float revealVolume = 0.55f;

        private AudioSource revealSource;
        private AudioClip placeholder;
        private AudioClip revealPlaceholder;
        private float lastPlayTime = float.NegativeInfinity;
        private float lastRevealTime = float.NegativeInfinity;

        public int PlayCount { get; private set; }
        public float LastPitch { get; private set; }
        public float LastVolume { get; private set; }
        public bool LastUsedAssignedClip { get; private set; }
        public bool IsPlaying => source != null && source.isPlaying;
        public AudioSource Source => source;

        /// <summary>How many reveal cues were started (one per revealed treasure).</summary>
        public int RevealCount { get; private set; }
        public float LastRevealVolume { get; private set; }
        public bool IsRevealPlaying => revealSource != null && revealSource.isPlaying;

        private void Awake()
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            revealSource = gameObject.AddComponent<AudioSource>();
            revealSource.playOnAwake = false;
            revealSource.loop = false;
            revealSource.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (placeholder != null)
            {
                Destroy(placeholder);
            }

            if (revealPlaceholder != null)
            {
                Destroy(revealPlaceholder);
            }
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
                if (placeholder == null)
                {
                    placeholder = SoundSynth.DigStroke();
                }

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

        /// <summary>The treasure has just come out of the ground: one short, soft discovery sound.</summary>
        /// <returns>true if a sound was started.</returns>
        public bool PlayReveal()
        {
            if (revealSource == null || Time.unscaledTime - lastRevealTime < 0.2f)
            {
                return false;
            }

            AudioClip clip = revealClip;
            if (clip == null)
            {
                if (revealPlaceholder == null)
                {
                    revealPlaceholder = SoundSynth.Reveal();
                }

                clip = revealPlaceholder;
            }

            lastRevealTime = Time.unscaledTime;
            LastRevealVolume = revealVolume * GameSettings.SfxScale; // Settings > SFX Volume
            revealSource.Stop();
            revealSource.clip = clip;
            revealSource.pitch = 1f;
            revealSource.volume = LastRevealVolume;
            revealSource.Play();
            RevealCount++;
            return true;
        }
    }
}
