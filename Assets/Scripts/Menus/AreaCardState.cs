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

    /// <summary>
    /// Everything a card needs to show, derived from the saved progress (<see cref="ProgressService"/>) and the <see cref="AreaCatalog"/>.
    /// Since stage 18B every card shows the same three things in the same order, none of them mistaken for the map's own gold: its STATUS (UNLOCKED / LOCKED), its TREASURES (x / n, by id) and
    /// WHEN it opens, always in TOTAL gold (the gold of the whole game). The player's total gold is shown once on the screen, labelled as such.
    /// </summary>
    public readonly struct AreaCardState
    {
        public readonly string AreaId;
        public readonly string DisplayName;
        public readonly AreaAvailability Availability;
        public readonly int TotalGold;
        public readonly int UnlockGold;
        public readonly int GoalGold;

        /// <summary>Every treasure of the map is collected (not the gold goal: see <see cref="ProgressService.IsAreaCompleted"/>).</summary>
        public readonly bool Completed;

        public readonly int TreasuresCollected;
        public readonly int TreasuresTotal;

        public AreaCardState(string areaId, string displayName, AreaAvailability availability, int totalGold, int unlockGold, int goalGold, bool completed, int treasuresCollected = 0, int treasuresTotal = 0)
        {
            AreaId = areaId;
            DisplayName = displayName;
            Availability = availability;
            TotalGold = Mathf.Max(0, totalGold);
            UnlockGold = unlockGold;
            GoalGold = goalGold;
            Completed = completed;
            TreasuresTotal = Mathf.Max(0, treasuresTotal);
            TreasuresCollected = Mathf.Clamp(treasuresCollected, 0, TreasuresTotal);
        }

        public bool IsLocked => Availability == AreaAvailability.Locked;

        /// <summary>The fill of the bar: the share of the map's treasures that are collected (0 for a closed map). Never the gold.</summary>
        public float Progress01 => TreasuresTotal <= 0 ? 0f : Mathf.Clamp01(TreasuresCollected / (float)TreasuresTotal);

        /// <summary>Gold still missing (in TOTAL gold) until the map opens; 0 once it is open.</summary>
        public int RemainingGold => IsLocked ? Mathf.Max(0, UnlockGold - TotalGold) : 0;

        /// <summary>A map is "complete" on its card once every one of its treasures is collected and it can be played.</summary>
        public bool ShowsCompleted => Completed && Availability == AreaAvailability.Playable;

        /// <summary>The big line: UNLOCKED or LOCKED.</summary>
        public string StatusLine => IsLocked ? "LOCKED" : "UNLOCKED";

        /// <summary>The line under the bar: "TREASURES: 3 / 15" (a closed map shows 0 / n).</summary>
        public string TreasureText => $"TREASURES: {TreasuresCollected} / {TreasuresTotal}";

        /// <summary>When the map opens, in total gold: the threshold for a closed map, a reminder for an open one ("OPEN FROM THE START" for the first map).</summary>
        public string UnlockText => UnlockGold <= 0
            ? "OPEN FROM THE START"
            : IsLocked ? $"UNLOCKS AT {UnlockGold} TOTAL GOLD" : $"UNLOCKED AT {UnlockGold} TOTAL GOLD";

        public string StatusText => Availability switch
        {
            AreaAvailability.Locked => "LOCKED",
            AreaAvailability.ComingSoon => "COMING SOON",
            _ => "PLAY"
        };
    }
}
