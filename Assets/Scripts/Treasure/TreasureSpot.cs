using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    public enum TreasureState
    {
        Hidden,
        Revealed,
        Collected
    }

    /// <summary>
    /// A single buried treasure. State only moves forward: Hidden -> Revealed -> Collected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreasureSpot : MonoBehaviour
    {
        private static readonly List<TreasureSpot> ActiveSpots = new();

        [SerializeField] private TreasureDefinition definition;
        [Tooltip("Visual shown after DIG. Kept inactive while the treasure is hidden.")]
        [SerializeField] private GameObject visualRoot;
        [Tooltip("Unique id of THIS placed treasure (saved when it is collected). Must be unique across the game. " +
                 "Empty = a fallback id built from scene, type and position (changes if the spot is moved).")]
        [SerializeField] private string spotId;

        public static IReadOnlyList<TreasureSpot> Active => ActiveSpots;

        /// <summary>
        /// Set by the hunt controller while a save is loaded: tells a spot whether its id was already collected, so a
        /// reloaded scene (or a late-spawned spot) starts as Collected instead of paying gold again.
        /// </summary>
        public static Func<string, bool> AlreadyCollectedProvider;

        /// <summary>The explicit id if set, otherwise a deterministic fallback (scene / type / position).</summary>
        public string SpotId => HasExplicitId ? spotId.Trim() : BuildFallbackId();

        public bool HasExplicitId => !string.IsNullOrWhiteSpace(spotId);

        public TreasureDefinition Definition => definition;
        public TreasureState State { get; private set; } = TreasureState.Hidden;

        public event Action<TreasureSpot> Revealed;
        public event Action<TreasureSpot> Collected;

        private void OnEnable()
        {
            ActiveSpots.Add(this);

            if (State == TreasureState.Hidden && AlreadyCollectedProvider != null && AlreadyCollectedProvider(SpotId))
            {
                RestoreCollected();
            }
        }

        private void OnDisable()
        {
            ActiveSpots.Remove(this);
        }

        private void Awake()
        {
            EnsureVisual();
            ApplyVisual();
        }

        public void SetSpotId(string id)
        {
            spotId = id;
        }

        /// <summary>
        /// Puts a treasure that was already collected in a previous session / scene load straight into the Collected state.
        /// Silent: raises no event and gives no gold.
        /// </summary>
        public void RestoreCollected()
        {
            if (State == TreasureState.Collected)
            {
                return;
            }

            State = TreasureState.Collected;
            ApplyVisual();
        }

        private string BuildFallbackId()
        {
            Vector3 p = transform.position;
            string type = definition != null ? definition.Id : "treasure";
            return string.Format(
                CultureInfo.InvariantCulture, "{0}/{1}@{2:F1},{3:F1},{4:F1}", gameObject.scene.name, type, p.x, p.y, p.z);
        }

        public void Configure(TreasureDefinition newDefinition, GameObject newVisualRoot)
        {
            definition = newDefinition;
            visualRoot = newVisualRoot;
            EnsureVisual();
            ApplyVisual();
        }

        /// <summary>
        /// A spot without its own visual builds one from the definition's model prefab, so a treasure type
        /// can be placed from data alone. Spots with a visual of their own (e.g. the Gold Coin prefab) are untouched.
        /// </summary>
        private void EnsureVisual()
        {
            if (visualRoot != null || definition == null || definition.ModelPrefab == null)
            {
                return;
            }

            GameObject model = Instantiate(definition.ModelPrefab, transform);
            model.name = definition.ModelPrefab.name;
            visualRoot = model;
        }

        /// <summary>Horizontal (XZ) distance, so the player's height does not matter.</summary>
        public float HorizontalDistanceTo(Vector3 worldPosition)
        {
            Vector3 delta = transform.position - worldPosition;
            delta.y = 0f;
            return delta.magnitude;
        }

        /// <returns>true only on the Hidden -> Revealed transition.</returns>
        public bool Reveal()
        {
            if (State != TreasureState.Hidden)
            {
                return false;
            }

            State = TreasureState.Revealed;
            ApplyVisual();
            Revealed?.Invoke(this);
            return true;
        }

        /// <returns>true only on the Revealed -> Collected transition (a treasure can be collected once).</returns>
        public bool Collect()
        {
            if (State != TreasureState.Revealed)
            {
                return false;
            }

            State = TreasureState.Collected;
            ApplyVisual();
            Collected?.Invoke(this);
            return true;
        }

        private void ApplyVisual()
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(State == TreasureState.Revealed);
            }
        }
    }
}
