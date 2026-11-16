using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Data-only description of the reward feedback for a treasure (Concept Document 2.6 / 2.7).
    /// This stage only stores the data and builds the collect message; the actual effects
    /// (floating text, particles, sounds) are a later stage and will read these fields.
    /// Profiles are shared assets - normally one per rarity tier.
    /// </summary>
    [CreateAssetMenu(menuName = "ItemCollector3D/Treasure Feedback Profile", fileName = "FeedbackProfile")]
    public sealed class TreasureFeedbackProfile : ScriptableObject
    {
        [Tooltip("Shown before the gold amount, e.g. \"RARE!\". Leave empty for no announcement.")]
        [SerializeField] private string announcement = string.Empty;
        [SerializeField] private Color accentColor = Color.white;
        [Tooltip("0 = subtle, 1 = strongest (mobile-friendly) feedback. Used by later effect stages.")]
        [SerializeField, Range(0f, 1f)] private float effectIntensity = 0.25f;

        [Header("Optional assets (none assigned yet - supplied by the effects stage)")]
        [SerializeField] private AudioClip collectSound;
        [SerializeField] private GameObject collectParticlesPrefab;

        public string Announcement => announcement;
        public Color AccentColor => accentColor;
        public float EffectIntensity => effectIntensity;
        public AudioClip CollectSound => collectSound;
        public GameObject CollectParticlesPrefab => collectParticlesPrefab;

        /// <summary>"+10 Gold" for no announcement, otherwise "RARE! +50 Gold".</summary>
        public string FormatCollectMessage(int gold)
        {
            return string.IsNullOrEmpty(announcement)
                ? $"+{gold} Gold"
                : $"{announcement} +{gold} Gold";
        }
    }
}
