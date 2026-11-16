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

        private bool digVisible;
        private bool collectVisible;
        private bool subscribed;

        public int TotalGold { get; private set; }

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
            digVisible = true;
            collectVisible = true;
            RefreshButtons();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
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
            TreasureSpot target = FindInRange(TreasureState.Hidden);
            if (target != null)
            {
                target.Reveal();
            }

            RefreshButtons();
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

            bool canDig = FindInRange(TreasureState.Hidden) != null;
            bool canCollect = FindInRange(TreasureState.Revealed) != null;

            if (canDig != digVisible)
            {
                digVisible = canDig;
                hud.SetDigState(canDig);
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
