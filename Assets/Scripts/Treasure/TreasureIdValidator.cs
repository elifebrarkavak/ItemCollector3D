using System.Collections.Generic;

namespace ItemCollector3D.Treasure
{
    public sealed class TreasureIdReport
    {
        public int SpotCount;
        public int FallbackIdCount;
        public readonly List<string> DuplicateIds = new();

        /// <summary>True when no two spots share an id.</summary>
        public bool IsValid => DuplicateIds.Count == 0;
    }

    /// <summary>
    /// Checks that every placed treasure has a unique id (saved progress is keyed by it: a duplicate id would make two
    /// treasures share one "collected" flag and pay gold only once). Spots without an explicit id use a position based
    /// fallback and are counted so they can be given a stable id.
    /// </summary>
    public static class TreasureIdValidator
    {
        public static TreasureIdReport Validate(IEnumerable<TreasureSpot> spots)
        {
            var report = new TreasureIdReport();
            var seen = new HashSet<string>();
            var reported = new HashSet<string>();
            foreach (TreasureSpot spot in spots)
            {
                if (spot == null)
                {
                    continue;
                }

                report.SpotCount++;
                if (!spot.HasExplicitId)
                {
                    report.FallbackIdCount++;
                }

                string id = spot.SpotId;
                if (!seen.Add(id) && reported.Add(id))
                {
                    report.DuplicateIds.Add(id);
                }
            }

            return report;
        }
    }
}
