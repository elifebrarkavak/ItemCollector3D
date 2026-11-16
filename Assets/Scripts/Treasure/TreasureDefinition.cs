using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Data describing one kind of treasure (Gold Coin, Gold Nugget, Gold Bar, Ancient Artifact, ...).
    /// New treasure types are new assets - no new code is required unless the behaviour differs.
    /// Starting values follow Concept Document v1.0 (2.7) and stay adjustable in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "ItemCollector3D/Treasure Definition", fileName = "TreasureDefinition")]
    public sealed class TreasureDefinition : ScriptableObject
    {
        [SerializeField] private string id = "gold_coin";
        [SerializeField] private string displayName = "Gold Coin";
        [SerializeField] private TreasureRarity rarity = TreasureRarity.Common;
        [SerializeField, Min(0)] private int goldValue = 10;

        [Header("Presentation")]
        [Tooltip("Visual model shown after DIG. Used by TreasureSpot when the spot has no visual of its own. " +
                 "Placeholder primitives for now; final art is replaced by swapping this reference.")]
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private TreasureFeedbackProfile feedbackProfile;

        [Header("Ranges (metres, horizontal distance)")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 15f;
        [SerializeField, Min(0.1f)] private float digRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float collectRadius = 3f;

        public string Id => id;
        public string DisplayName => displayName;
        public TreasureRarity Rarity => rarity;
        public int GoldValue => goldValue;
        public GameObject ModelPrefab => modelPrefab;
        public TreasureFeedbackProfile FeedbackProfile => feedbackProfile;
        public float DetectionRadius => detectionRadius;
        public float DigRadius => digRadius;
        public float CollectRadius => collectRadius;

        /// <summary>Message for the reward feedback, e.g. "RARE! +50 Gold".</summary>
        public string CollectMessage => feedbackProfile != null
            ? feedbackProfile.FormatCollectMessage(goldValue)
            : $"+{goldValue} Gold";

        private void OnValidate()
        {
            detectionRadius = Mathf.Max(detectionRadius, digRadius);
        }

        /// <summary>Data integrity check used by tests and tools.</summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "Id is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "Display name is empty.";
                return false;
            }

            if (goldValue <= 0)
            {
                error = "Gold value must be greater than 0.";
                return false;
            }

            if (modelPrefab == null)
            {
                error = "Model prefab is missing.";
                return false;
            }

            if (feedbackProfile == null)
            {
                error = "Feedback profile is missing.";
                return false;
            }

            if (digRadius > detectionRadius || digRadius <= 0f || collectRadius <= 0f)
            {
                error = "Radii are inconsistent (dig must be <= detection, all > 0).";
                return false;
            }

            error = null;
            return true;
        }
    }
}
