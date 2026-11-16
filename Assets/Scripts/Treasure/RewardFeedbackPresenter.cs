using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Shows the reward for a collected treasure: floating text, particle burst + glow, and sound.
    /// Hooks the existing <see cref="TreasureHuntController.TreasureCollected"/> event, so it never touches the HUD
    /// gold counter (the controller already updated it) and it adds no second HUD or gold manager.
    /// All look/feel values come from the treasure's <see cref="TreasureFeedbackProfile"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private TreasureHuntController hunt;
        [SerializeField] private FloatingRewardText floatingText;
        [SerializeField] private CollectBurstEffect burst;
        [SerializeField] private CollectSoundPlayer sound;
        [SerializeField, Min(0f)] private float effectHeight = 0.4f;
        [SerializeField, Min(0.5f)] private float profileParticleLifetime = 3f;

        private readonly HashSet<TreasureSpot> presented = new();

        public int RewardsShown { get; private set; }
        public string LastMessage { get; private set; } = string.Empty;
        public int LastGold { get; private set; }

        /// <summary>Raised once per shown reward: spot, message text, gold granted.</summary>
        public event Action<TreasureSpot, string, int> RewardShown;

        private void OnEnable()
        {
            if (hunt != null)
            {
                hunt.TreasureCollected += HandleCollected;
            }
        }

        private void OnDisable()
        {
            if (hunt != null)
            {
                hunt.TreasureCollected -= HandleCollected;
            }
        }

        private void HandleCollected(TreasureSpot spot, int gold)
        {
            // Belt and braces: the controller only raises this for a successful Collect(), but never reward twice.
            if (spot == null || !presented.Add(spot))
            {
                return;
            }

            TreasureFeedbackProfile profile = spot.Definition != null ? spot.Definition.FeedbackProfile : null;
            string message = profile != null ? profile.FormatCollectMessage(gold) : $"+{gold} Gold";
            Color color = profile != null ? profile.AccentColor : Color.white;
            float intensity = profile != null ? profile.EffectIntensity : 0.25f;
            float glow = profile != null ? profile.Glow : 0f;
            Vector3 position = spot.transform.position + Vector3.up * effectHeight;

            if (floatingText != null)
            {
                floatingText.Show(message, color, intensity);
            }

            bool profileHasParticles = profile != null && profile.CollectParticlesPrefab != null;
            if (burst != null)
            {
                burst.Play(position, color, intensity, glow, !profileHasParticles);
            }

            if (profileHasParticles)
            {
                GameObject custom = Instantiate(profile.CollectParticlesPrefab, position, Quaternion.identity);
                Destroy(custom, profileParticleLifetime);
            }

            if (sound != null)
            {
                sound.Play(profile, intensity);
            }

            RewardsShown++;
            LastMessage = message;
            LastGold = gold;
            RewardShown?.Invoke(spot, message, gold);
        }
    }
}
