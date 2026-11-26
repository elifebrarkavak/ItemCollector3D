using System;
using System.Collections.Generic;

namespace ItemCollector3D.Progress
{
    /// <summary>
    /// The ids of every treasure of the three maps (stage 17D): Green Mine 15, Rocky Canyon 20, Ancient Mine 25 = 60. The 100% COLLECTION achievement is counted against THIS list, by id, so an unknown id (for example the
    /// sandbox coin of the old test scene), a blank or a duplicate in a save never counts as a treasure, and neither does the gold. The scenes remain the source of the treasures themselves (position, gold); a test
    /// compares this list with the TreasureSpots of the three scenes so the two can never drift apart.
    /// </summary>
    public static class TreasureCatalog
    {
        public const int TotalCount = 60;

        private static readonly string[] GreenMine =
        {
            "green_mine.bridge_01", "green_mine.camp_01", "green_mine.forest_01", "green_mine.forest_02", "green_mine.forest_03",
            "green_mine.meadow_01", "green_mine.mine_01", "green_mine.mine_room_01", "green_mine.mine_room_02", "green_mine.mine_tunnel_01",
            "green_mine.riverbank_01", "green_mine.rocky_01", "green_mine.rocky_02", "green_mine.rocky_03", "green_mine.rocky_04",
        };

        private static readonly string[] RockyCanyon =
        {
            "rocky_canyon.outpost_01", "rocky_canyon.outpost_02", "rocky_canyon.outpost_03",
            "rocky_canyon.overlook_01", "rocky_canyon.overlook_02", "rocky_canyon.overlook_03",
            "rocky_canyon.railway_01", "rocky_canyon.railway_02", "rocky_canyon.railway_03", "rocky_canyon.railway_04", "rocky_canyon.railway_05",
            "rocky_canyon.ravine_01", "rocky_canyon.ravine_02", "rocky_canyon.ravine_03", "rocky_canyon.ravine_04",
            "rocky_canyon.riverbed_01", "rocky_canyon.riverbed_02", "rocky_canyon.riverbed_03", "rocky_canyon.riverbed_04", "rocky_canyon.riverbed_05",
        };

        private static readonly string[] AncientMine =
        {
            "ancient_mine.crystal_01", "ancient_mine.crystal_02", "ancient_mine.crystal_03", "ancient_mine.crystal_04", "ancient_mine.crystal_05", "ancient_mine.crystal_06", "ancient_mine.crystal_07",
            "ancient_mine.gate_01", "ancient_mine.gate_02", "ancient_mine.gate_03",
            "ancient_mine.hall_01", "ancient_mine.hall_02", "ancient_mine.hall_03", "ancient_mine.hall_04", "ancient_mine.hall_05",
            "ancient_mine.vault_01", "ancient_mine.vault_02", "ancient_mine.vault_03", "ancient_mine.vault_04", "ancient_mine.vault_05",
            "ancient_mine.workshop_01", "ancient_mine.workshop_02", "ancient_mine.workshop_03", "ancient_mine.workshop_04", "ancient_mine.workshop_05",
        };

        private static readonly string[] All = BuildAll();
        private static readonly HashSet<string> Known = new(All, StringComparer.Ordinal);

        /// <summary>All 60 treasure ids (Green Mine, then Rocky Canyon, then Ancient Mine).</summary>
        public static IReadOnlyList<string> AllIds => All;

        /// <summary>The treasure ids of one area (the ids of the AreaCatalog), or an empty list for an unknown area.</summary>
        public static IReadOnlyList<string> IdsOf(string areaId)
        {
            return areaId switch
            {
                "green_mine" => GreenMine,
                "rocky_canyon" => RockyCanyon,
                "ancient_mine" => AncientMine,
                _ => Array.Empty<string>(),
            };
        }

        /// <summary>True for exactly the 60 ids of the three maps.</summary>
        public static bool IsKnown(string treasureId)
        {
            return !string.IsNullOrWhiteSpace(treasureId) && Known.Contains(treasureId.Trim());
        }

        private static string[] BuildAll()
        {
            var all = new List<string>(TotalCount);
            all.AddRange(GreenMine);
            all.AddRange(RockyCanyon);
            all.AddRange(AncientMine);
            return all.ToArray();
        }
    }
}
