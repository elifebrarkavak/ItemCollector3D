using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Turns the existing <see cref="TreasureHuntController"/> dig phase into feedback: the player dig motion,
    /// dirt at the dig spot and a dig sound on each stroke, plus a final dirt puff and a soft discovery sound when the treasure is revealed.
    /// It only listens to <see cref="TreasureHuntController.DigStarted"/> / <see cref="TreasureHuntController.DigCompleted"/>
    /// and owns no treasure state, so TreasureSpot / reveal / collect / rarity feedback keep working unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DigFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private TreasureHuntController hunt;
        [SerializeField] private PlayerDigAnimator playerAnimator;
        [SerializeField] private DigDirtEffect dirt;
        [SerializeField] private DigSoundPlayer sound;
        [Tooltip("Used when no PlayerDigAnimator is assigned.")]
        [SerializeField, Min(1)] private int fallbackStrokes = 2;

        private TreasureSpot activeSpot;
        private float elapsed;
        private float duration;
        private int strokesTotal;
        private int nextStroke;

        public int DigsPresented { get; private set; }
        public int StrokesTriggered { get; private set; }
        public int RevealPuffs { get; private set; }
        public int CancelledDigs { get; private set; }

        private void OnEnable()
        {
            if (hunt != null)
            {
                hunt.DigStarted += HandleDigStarted;
                hunt.DigCompleted += HandleDigCompleted;
                hunt.DigCancelled += HandleDigCancelled;
            }
        }

        private void OnDisable()
        {
            if (hunt != null)
            {
                hunt.DigStarted -= HandleDigStarted;
                hunt.DigCompleted -= HandleDigCompleted;
                hunt.DigCancelled -= HandleDigCancelled;
            }

            activeSpot = null;
        }

        private void HandleDigStarted(TreasureSpot spot, float seconds)
        {
            if (spot == null || activeSpot != null)
            {
                return; // never present two digs at once
            }

            activeSpot = spot;
            elapsed = 0f;
            duration = seconds;
            nextStroke = 0;
            strokesTotal = playerAnimator != null ? playerAnimator.Strokes : fallbackStrokes;
            DigsPresented++;

            if (playerAnimator != null)
            {
                playerAnimator.Play(seconds);
            }
        }

        private void Update()
        {
            if (activeSpot == null)
            {
                return;
            }

            elapsed += Time.deltaTime;
            // Stroke k hits the ground at (k + 0.5) / strokes of the dig, the same moment as the visual's peak.
            while (nextStroke < strokesTotal && elapsed >= (nextStroke + 0.5f) / strokesTotal * duration)
            {
                TriggerStroke(nextStroke++);
            }
        }

        private void TriggerStroke(int index)
        {
            StrokesTriggered++;
            if (dirt != null && activeSpot != null)
            {
                dirt.PlayStroke(activeSpot.transform.position);
            }

            if (sound != null)
            {
                sound.PlayStroke(index);
            }
        }

        private void HandleDigCancelled(TreasureSpot spot)
        {
            // The player walked out of range: stop the motion (visual back to rest), no reveal puff, no further strokes.
            activeSpot = null;
            CancelledDigs++;
            if (playerAnimator != null)
            {
                playerAnimator.Cancel();
            }
        }

        private void HandleDigCompleted(TreasureSpot spot)
        {
            // Flush any stroke that did not get its turn (very short digs) so strokes and sounds stay paired.
            while (activeSpot != null && nextStroke < strokesTotal)
            {
                TriggerStroke(nextStroke++);
            }

            if (dirt != null && spot != null)
            {
                dirt.PlayReveal(spot.transform.position);
            }

            if (sound != null)
            {
                sound.PlayReveal(); // the short discovery sound, once per revealed treasure (never on a cancelled dig)
            }

            RevealPuffs++;
            activeSpot = null;
        }
    }
}
