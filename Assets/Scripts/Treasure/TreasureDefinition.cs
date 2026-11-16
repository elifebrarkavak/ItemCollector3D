using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Data describing one kind of treasure (Gold Coin today; gems, chests, etc. later).
    /// New treasure types are new assets/prefabs - no new code is required unless the behaviour differs.
    /// </summary>
    [CreateAssetMenu(menuName = "ItemCollector3D/Treasure Definition", fileName = "TreasureDefinition")]
    public sealed class TreasureDefinition : ScriptableObject
    {
        [SerializeField] private string id = "gold_coin";
        [SerializeField] private string displayName = "Gold Coin";
        [SerializeField, Min(0)] private int goldValue = 10;

        [Header("Ranges (metres, horizontal distance)")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 15f;
        [SerializeField, Min(0.1f)] private float digRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float collectRadius = 3f;

        public string Id => id;
        public string DisplayName => displayName;
        public int GoldValue => goldValue;
        public float DetectionRadius => detectionRadius;
        public float DigRadius => digRadius;
        public float CollectRadius => collectRadius;

        private void OnValidate()
        {
            detectionRadius = Mathf.Max(detectionRadius, digRadius);
        }
    }
}
