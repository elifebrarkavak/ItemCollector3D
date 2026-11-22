using System;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
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

        [Header("Progression (optional)")]
        [Tooltip("When set, total gold, collected treasure ids and area unlocks are persistent and cumulative across areas. " +
                 "Without it the controller keeps a scene-local gold counter exactly as before.")]
        [SerializeField] private ProgressManager progress;
        [Tooltip("The area this scene belongs to (id from the AreaCatalog). Its goal is the HUD maximum; a locked area is not playable.")]
        [SerializeField] private string areaId = "green_mine";
        [Tooltip("Seconds between pressing DIG and the treasure being revealed (Concept Document: short interactions). 0 = reveal instantly.")]
        [SerializeField, Min(0f)] private float digDuration = 0.45f;
        [Tooltip("DIG only starts inside the dig radius, but a dig already under way is cancelled only when the player is this much further away, so a single step " +
                 "or a nudge from the character controller at the edge of the radius does not silently throw the dig away.")]
        [SerializeField, Min(0f)] private float digLeaveMargin = 0.6f;

        // DIG button: 0 = hidden, 1 = ready, 2 = busy (visible but disabled while digging).
        private int digButtonMode = -1;
        private bool collectVisible;
        private bool subscribed;
        private TreasureSpot digTarget;
        private float digElapsed;

        private int localGold;
        private ProgressService connectedService; // kept so cleanup never depends on `progress` still being alive

        /// <summary>Cumulative total gold (from the persistent progress when a ProgressManager is assigned, else scene-local).</summary>
        public int TotalGold => progress != null && progress.Service != null ? progress.Service.TotalGold : localGold;

        public string AreaId => areaId;

        /// <summary>False while this scene's area is still locked (total gold below its unlock threshold).</summary>
        public bool IsAreaAccessible => progress == null || progress.Service == null || progress.Service.IsAreaUnlocked(areaId);

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
            ConnectProgress();
            RefreshHudGold(true);

            // Force the first button refresh to apply.
            digButtonMode = -1;
            collectVisible = true;
            RefreshButtons();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DisconnectProgress();
        }

        public void SetAreaId(string newAreaId)
        {
            areaId = newAreaId;
            RefreshHudGold(true);
        }

        private void ConnectProgress()
        {
            if (progress == null)
            {
                return;
            }

            progress.Initialize();
            connectedService = progress.Service;
            connectedService.Changed += HandleProgressChanged;
            TreasureSpot.AlreadyCollectedProvider = connectedService.IsCollected;

            // Spots that were enabled before this controller started: restore the ones collected in an earlier session.
            foreach (TreasureSpot spot in TreasureSpot.Active)
            {
                if (spot.State == TreasureState.Hidden && progress.Service.IsCollected(spot.SpotId))
                {
                    spot.RestoreCollected();
                }
            }

            TreasureIdReport report = TreasureIdValidator.Validate(TreasureSpot.Active);
            if (!report.IsValid)
            {
                ProgressLog.Warning("Duplicate treasure ids (they would share one collected flag): " + string.Join(", ", report.DuplicateIds));
            }
        }

        private void DisconnectProgress()
        {
            // Unity destroys scene objects in no fixed order, so `progress` may already be gone here: use the cached service.
            if (connectedService == null)
            {
                return;
            }

            connectedService.Changed -= HandleProgressChanged;
            if (TreasureSpot.AlreadyCollectedProvider != null && TreasureSpot.AlreadyCollectedProvider.Target == connectedService)
            {
                TreasureSpot.AlreadyCollectedProvider = null; // never leave a stale provider for the next scene
            }

            connectedService = null;
        }

        private void HandleProgressChanged()
        {
            RefreshHudGold(false);
        }

        private void RefreshHudGold(bool updateTitle)
        {
            if (hud == null)
            {
                return;
            }

            int goal = goldTarget;
            string title = levelTitle;
            if (progress != null && progress.Service != null)
            {
                int areaGoal = progress.Service.GetGoal(areaId);
                if (areaGoal > 0)
                {
                    goal = areaGoal;
                }

                string areaName = progress.Service.GetDisplayName(areaId);
                if (!string.IsNullOrEmpty(areaName))
                {
                    title = areaName;
                }
            }

            if (updateTitle)
            {
                hud.SetLevelTitle(title);
            }

            hud.SetMaximumGold(goal);
            hud.SetGold(TotalGold);
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
            if (IsDigging || !IsAreaAccessible || GamePause.IsPaused)
            {
                return; // one dig at a time (Invoke()/double taps must be safe); a locked area cannot be played
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

            return spot.HorizontalDistanceTo(player.position) <= spot.Definition.DigRadius + digLeaveMargin;
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
            if (GamePause.IsPaused)
            {
                return; // gameplay is frozen while paused
            }

            TreasureSpot target = FindInRange(TreasureState.Revealed);
            if (target != null && target.Collect())
            {
                int reward = target.Definition != null ? target.Definition.GoldValue : 0;
                if (GrantGold(target, reward))
                {
                    hud.SetGold(TotalGold);
                    TreasureCollected?.Invoke(target, reward);
                }
            }

            RefreshButtons();
        }

        /// <returns>false if no gold was granted (the treasure id was already collected, or invalid).</returns>
        private bool GrantGold(TreasureSpot spot, int reward)
        {
            if (progress == null || progress.Service == null)
            {
                localGold += reward;
                return true;
            }

            RegisterResult result = progress.Service.RegisterCollected(spot.SpotId, reward);
            if (result != RegisterResult.Accepted)
            {
                ProgressLog.Warning($"No gold granted for treasure '{spot.SpotId}': {result}.");
            }

            return result == RegisterResult.Accepted;
        }

        private void RefreshButtons()
        {
            if (hud == null)
            {
                return;
            }

            bool accessible = IsAreaAccessible;
            int digMode = !accessible ? 0 : IsDigging ? 2 : FindInRange(TreasureState.Hidden) != null ? 1 : 0;
            bool canCollect = accessible && FindInRange(TreasureState.Revealed) != null;

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
