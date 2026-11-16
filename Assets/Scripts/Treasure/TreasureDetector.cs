using System;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Metal-detector logic. The nearest hidden treasure inside its detection radius produces pulses;
    /// the closer the player, the shorter the interval between pulses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreasureDetector : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Min(0.05f)] private float slowestInterval = 1.6f;
        [SerializeField, Min(0.02f)] private float fastestInterval = 0.15f;

        private float timer;
        private bool armed;

        /// <summary>Raised on every signal pulse with proximity in 0..1.</summary>
        public event Action<float> Pulsed;

        public TreasureSpot NearestHidden { get; private set; }

        /// <summary>0 = no signal / edge of detection radius, 1 = inside dig range.</summary>
        public float Proximity { get; private set; }

        /// <summary>Seconds between pulses; 0 when there is no signal.</summary>
        public float CurrentInterval { get; private set; }

        public bool HasSignal => NearestHidden != null;

        public void SetPlayer(Transform target)
        {
            player = target;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            Evaluate();

            if (!HasSignal)
            {
                armed = false;
                timer = 0f;
                return;
            }

            if (!armed)
            {
                // Pulse immediately when entering detection range.
                armed = true;
                timer = CurrentInterval;
            }
            else
            {
                timer += deltaTime;
            }

            if (timer >= CurrentInterval)
            {
                timer = 0f;
                Pulsed?.Invoke(Proximity);
            }
        }

        public static float ProximityFor(float distance, float digRadius, float detectionRadius)
        {
            if (distance > detectionRadius)
            {
                return 0f;
            }

            if (detectionRadius <= digRadius)
            {
                return 1f;
            }

            return Mathf.Clamp01(Mathf.InverseLerp(detectionRadius, digRadius, distance));
        }

        public static float IntervalFor(float proximity, float slowest, float fastest)
        {
            return Mathf.Lerp(slowest, fastest, Mathf.Clamp01(proximity));
        }

        private void Evaluate()
        {
            NearestHidden = null;
            Proximity = 0f;
            CurrentInterval = 0f;

            if (player == null)
            {
                return;
            }

            float bestDistance = float.MaxValue;
            foreach (TreasureSpot spot in TreasureSpot.Active)
            {
                if (spot.State != TreasureState.Hidden || spot.Definition == null)
                {
                    continue;
                }

                float distance = spot.HorizontalDistanceTo(player.position);
                if (distance <= spot.Definition.DetectionRadius && distance < bestDistance)
                {
                    bestDistance = distance;
                    NearestHidden = spot;
                }
            }

            if (NearestHidden == null)
            {
                return;
            }

            TreasureDefinition definition = NearestHidden.Definition;
            Proximity = ProximityFor(bestDistance, definition.DigRadius, definition.DetectionRadius);
            CurrentInterval = IntervalFor(Proximity, slowestInterval, fastestInterval);
        }
    }
}
