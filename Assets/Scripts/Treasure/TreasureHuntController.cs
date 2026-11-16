using System;
using ItemCollector3D.UI;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Glue between the existing HUD and the treasure spots:
    /// shows DIG / COLLECT when in range, applies them, and pushes gold to the HUD.
    /// It owns no UI of its own - it only drives the existing <see cref="HudController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreasureHuntController : MonoBehaviour
    {
        [SerializeField] private HudController hud;
        [SerializeField] private Transform player;
        [SerializeField] private string levelTitle = "GOLD COIN TEST";
        [SerializeField, Min(1)] private int goldTarget = 10;
        [Tooltip("Seconds between pressing DIG and the treasure being revealed (Concept Document: short interactions). 0 = reveal instantly.")]
        [SerializeField, Min(0f)] private float digDuration = 0.45f;

        // DIG button: 0 = hidden, 1 = ready, 2 = busy (visible but disabled while digging).
        private int digButtonMode = -1;
        private bool collectVisible;
        private bool subscribed;
        private TreasureSpot digTarget;
        private float digElapsed;

        public int TotalGold { get; private set; }
        public float DigDuration => digDuration;

        /// <summary>True from DIG pressed until the treasure is revealed. A second DIG is ignored meanwhile.</summary>
        public bool IsDigging => digTarget != null;

        /// <summary>Raised when a dig starts: the spot being dug and the dig duration in seconds.</summary>
        public event Action<TreasureSpot, float> DigStarted;

        /// <summary>Raised right after the treasure was revealed by a finished dig.</summary>
        public event Action<TreasureSpot> DigCompleted;

        /// <summary>Raised when the player left the dig range during a dig: the treasure stays Hidden and DIG can be used again.</summary>
        public event Action<TreasureSpot> DigCancelled;

        /// <summary>Raised after a treasure was collected and gold was added.</summary>
        public event Action<TreasureSpot, int> TreasureCollected;

        public void Configure(HudController newHud, Transform newPlayer)
        {
            Unsubscribe();
            hud = newHud;
            player = newPlayer;
            Subscribe();
        }

        private void Start()
        {
            Subscribe();

            if (hud != null)
            {
                hud.SetLevelTitle(levelTitle);
                hud.SetMaximumGold(goldTarget);
                hud.SetGold(TotalGold);
            }

            // Force the first button refresh to apply.
            digButtonMode = -1;
            collectVisible = true;
            RefreshButtons();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (IsDigging)
            {
                // The target is locked when DIG starts. The player may move while digging, but must stay inside the
                // dig range; leaving it cancels the dig safely (the treasure stays Hidden, no reveal, no gold).
                if (!IsInsideDigRange(digTarget))
                {
                    CancelDig();
                }
                else
                {
                    digElapsed += Time.deltaTime;
                    if (digElapsed >= digDuration)
                    {
                        CompleteDig();
                    }
                }
            }

            RefreshButtons();
        }

        private void Subscribe()
        {
            if (subscribed || hud == null)
            {
                return;
            }

            hud.DigRequested += HandleDigRequested;
            hud.CollectRequested += HandleCollectRequested;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || hud == null)
            {
                return;
            }

            hud.DigRequested -= HandleDigRequested;
            hud.CollectRequested -= HandleCollectRequested;
            subscribed = false;
        }

        private void HandleDigRequested()
        {
            if (IsDigging)
            {
                return; // one dig at a time; the button is disabled but Invoke()/double taps must be safe too
            }

            TreasureSpot target = FindInRange(TreasureState.Hidden);
            if (target == null)
            {
                return;
            }

            if (digDuration <= 0f)
            {
                if (target.Reveal())
                {
                    DigCompleted?.Invoke(target);
                }
            }
            else
            {
                digTarget = target;
                digElapsed = 0f;
                DigStarted?.Invoke(target, digDuration);
            }

            RefreshButtons();
        }

        private bool IsInsideDigRange(TreasureSpot spot)
        {
            if (player == null || spot.Definition == null)
            {
                return true; // nothing to measure: do not cancel
            }

            return spot.HorizontalDistanceTo(player.position) <= spot.Definition.DigRadius;
        }

        private void CancelDig()
        {
            TreasureSpot target = digTarget;
            digTarget = null;
            digElapsed = 0f;
            if (target != null)
            {
                DigCancelled?.Invoke(target);
            }
        }

        private void CompleteDig()
        {
            TreasureSpot target = digTarget;
            digTarget = null;
            if (target != null && target.Reveal())
            {
                DigCompleted?.Invoke(target);
            }
        }

        private void HandleCollectRequested()
        {
            TreasureSpot target = FindInRange(TreasureState.Revealed);
            if (target != null && target.Collect())
            {
                int reward = target.Definition != null ? target.Definition.GoldValue : 0;
                TotalGold += reward;
                hud.SetGold(TotalGold);
                TreasureCollected?.Invoke(target, reward);
            }

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (hud == null)
            {
                return;
            }

            int digMode = IsDigging ? 2 : FindInRange(TreasureState.Hidden) != null ? 1 : 0;
            bool canCollect = FindInRange(TreasureState.Revealed) != null;

            if (digMode != digButtonMode)
            {
                digButtonMode = digMode;
                hud.SetDigState(digMode != 0, digMode == 1);
            }

            if (canCollect != collectVisible)
            {
                collectVisible = canCollect;
                hud.SetCollectState(canCollect);
            }
        }

        /// <summary>Nearest spot in the given state that is inside its dig (Hidden) or collect (Revealed) radius.</summary>
        private TreasureSpot FindInRange(TreasureState state)
        {
            if (player == null)
            {
                return null;
            }

            TreasureSpot best = null;
            float bestDistance = float.MaxValue;
            foreach (TreasureSpot spot in TreasureSpot.Active)
            {
                if (spot.State != state || spot.Definition == null)
                {
                    continue;
                }

                float radius = state == TreasureState.Hidden
                    ? spot.Definition.DigRadius
                    : spot.Definition.CollectRadius;
                float distance = spot.HorizontalDistanceTo(player.position);
                if (distance <= radius && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = spot;
                }
            }

            return best;
        }
    }
}
