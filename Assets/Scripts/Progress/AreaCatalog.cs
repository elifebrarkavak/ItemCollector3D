using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.Progress
{
    [Serializable]
    public sealed class AreaEntry
    {
        public string id = "green_mine";
        public string displayName = "GREEN MINE";
        [Tooltip("Cumulative total gold needed to unlock this area (the first area uses 0).")]
        public int unlockTotalGold;
        [Tooltip("Cumulative total gold that completes this area.")]
        public int goalTotalGold = 500;

        public AreaEntry() { }

        public AreaEntry(string id, string displayName, int unlockTotalGold, int goalTotalGold)
        {
            this.id = id;
            this.displayName = displayName;
            this.unlockTotalGold = unlockTotalGold;
            this.goalTotalGold = goalTotalGold;
        }
    }

    /// <summary>
    /// Ordered list of areas with their unlock / goal thresholds (Concept Document v1.0, 2.7): Green Mine is open from the
    /// start (goal 500), Rocky Canyon unlocks at 500 total gold (goal 1200), Ancient Mine at 1200 (goal 2500).
    /// Values are data, tunable in the Inspector after playtesting. Gold is cumulative and never reset between areas.
    /// </summary>
    [CreateAssetMenu(menuName = "ItemCollector3D/Area Catalog", fileName = "AreaCatalog")]
    public sealed class AreaCatalog : ScriptableObject
    {
        [SerializeField] private List<AreaEntry> areas = new();

        public IReadOnlyList<AreaEntry> Areas => areas;

        public static AreaCatalog CreateDefault()
        {
            var catalog = CreateInstance<AreaCatalog>();
            catalog.Configure(new[]
            {
                new AreaEntry("green_mine", "GREEN MINE", 0, 500),
                new AreaEntry("rocky_canyon", "ROCKY CANYON", 500, 1200),
                new AreaEntry("ancient_mine", "ANCIENT MINE", 1200, 2500)
            });
            return catalog;
        }

        public void Configure(IEnumerable<AreaEntry> entries)
        {
            areas = new List<AreaEntry>(entries);
        }

        public int IndexOf(string areaId)
        {
            for (int i = 0; i < areas.Count; i++)
            {
                if (areas[i] != null && areas[i].id == areaId)
                {
                    return i;
                }
            }

            return -1;
        }

        public AreaEntry Get(string areaId)
        {
            int index = IndexOf(areaId);
            return index >= 0 ? areas[index] : null;
        }

        /// <summary>Data integrity check: ids unique and non-empty, first area open, thresholds sensible and ordered.</summary>
        public bool Validate(out string error)
        {
            if (areas == null || areas.Count == 0)
            {
                error = "No areas defined.";
                return false;
            }

            var seen = new HashSet<string>();
            int previousUnlock = -1;
            int previousGoal = -1;
            for (int i = 0; i < areas.Count; i++)
            {
                AreaEntry area = areas[i];
                if (area == null || string.IsNullOrWhiteSpace(area.id))
                {
                    error = $"Area #{i} has no id.";
                    return false;
                }

                if (!seen.Add(area.id))
                {
                    error = $"Duplicate area id '{area.id}'.";
                    return false;
                }

                if (area.goalTotalGold <= area.unlockTotalGold)
                {
                    error = $"Area '{area.id}': goal must be greater than its unlock threshold.";
                    return false;
                }

                if (i == 0 && area.unlockTotalGold != 0)
                {
                    error = "The first area must be open from the start (unlock 0).";
                    return false;
                }

                if (area.unlockTotalGold < previousUnlock || area.goalTotalGold <= previousGoal)
                {
                    error = $"Area '{area.id}': thresholds must increase with the area order.";
                    return false;
                }

                previousUnlock = area.unlockTotalGold;
                previousGoal = area.goalTotalGold;
            }

            error = null;
            return true;
        }
    }
}
