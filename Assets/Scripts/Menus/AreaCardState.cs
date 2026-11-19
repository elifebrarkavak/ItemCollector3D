using ItemCollector3D.Progress;
using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>What a card of the Area Selection can be. Locked and ComingSoon are deliberately separate: Locked is about the gold the player has
    /// (an area that is still closed), ComingSoon is about the game (an area that is open but has no map yet).</summary>
    public enum AreaAvailability
    {
        /// <summary>Open and it has a scene: PLAY enters it.</summary>
        Playable,

        /// <summary>Open (enough total gold) but there is no map for it yet: shown as COMING SOON, never entered.</summary>
        ComingSoon,

        /// <summary>Total gold is still below the unlock threshold: dimmed, with a lock and the gold that is missing.</summary>
        Locked
    }

    public enum AreaEnterResult
    {
        Entered,
        Locked,
        ComingSoon,
        UnknownArea
    }

    /// <summary>Everything a card needs to show, derived from the saved progress (<see cref="ProgressService"/>) and the <see cref="AreaCatalog"/>.</summary>
    public readonly struct AreaCardState
    {
        public readonly string AreaId;
        public readonly string DisplayName;
        public readonly AreaAvailability Availability;
        public readonly int TotalGold;
        public readonly int UnlockGold;
        public readonly int GoalGold;
        public readonly bool Completed;

        public AreaCardState(string areaId, string displayName, AreaAvailability availability, int totalGold, int unlockGold, int goalGold, bool completed)
        {
            AreaId = areaId;
            DisplayName = displayName;
            Availability = availability;
            TotalGold = Mathf.Max(0, totalGold);
            UnlockGold = unlockGold;
            GoalGold = goalGold;
            Completed = completed;
        }

        public bool IsLocked => Availability == AreaAvailability.Locked;

        /// <summary>The gold the bar counts towards: the unlock threshold while the area is locked, its goal once it is open.</summary>
        public int TargetGold => IsLocked ? UnlockGold : GoalGold;

        public int ShownGold => Mathf.Min(TotalGold, TargetGold);

        /// <summary>Gold still missing for the bar's target: for a locked area this is what is left until it unlocks.</summary>
        public int RemainingGold => Mathf.Max(0, TargetGold - TotalGold);

        public float Progress01 => TargetGold <= 0 ? 1f : Mathf.Clamp01(TotalGold / (float)TargetGold);

        /// <summary>Only an area you can actually play is "complete"; the gold goal of a COMING SOON area is just "reached".</summary>
        public bool ShowsCompleted => Completed && Availability == AreaAvailability.Playable;

        /// <summary>
        /// The big line (gold) of the card. A locked area says how much gold is LEFT to unlock it; an open area says its GOAL (the cumulative total gold that
        /// completes it). Both numbers come from the saved progress and the AreaCatalog.
        /// </summary>
        public string GoldText => IsLocked ? $"{RemainingGold} GOLD TO UNLOCK" : $"GOAL: {GoalGold} GOLD";

        /// <summary>The small line under the bar: the total gold the player has against the number the bar counts to.</summary>
        public string CaptionText => IsLocked
            ? $"TOTAL GOLD: {ShownGold} / {UnlockGold}"
            : Completed ? (ShowsCompleted ? "GOAL COMPLETE" : "GOAL REACHED") : $"{ShownGold} / {GoalGold} COLLECTED";

        public string StatusText => Availability switch
        {
            AreaAvailability.Locked => "LOCKED",
            AreaAvailability.ComingSoon => "COMING SOON",
            _ => "PLAY"
        };
    }
}
