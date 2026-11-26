using System;
using System.Collections.Generic;

namespace ItemCollector3D.Progress
{
    /// <summary>
    /// The two final achievements of the game (stage 17D) and the rules that make them true.
    /// <list type="bullet">
    /// <item><description><see cref="GameComplete"/>: the cumulative total gold reached <see cref="Goal"/> (2500). Gold is never capped: 2705 is the most the three maps can pay, 2500 is only the goal.</description></item>
    /// <item><description><see cref="Collection"/>: every one of the 60 treasures of the three maps is in the save (by id, see <see cref="TreasureCatalog"/>). It pays no gold.</description></item>
    /// </list>
    /// "Reached" is a fact derived from the saved gold and the saved treasure ids (both only ever grow), "owed" (pending) and "answered" (celebrated) are separate lists in the save
    /// (<see cref="GameProgressData.pendingAchievementIds"/>, <see cref="GameProgressData.celebratedAchievementIds"/>): reaching a goal, owing its celebration and having shown it are three different questions.
    /// </summary>
    public static class FinalAchievements
    {
        public const string GameComplete = "game_complete";
        public const string Collection = "collection_complete";

        /// <summary>The total gold that completes the game. The same number as the goal of the last area in the AreaCatalog (a test keeps the two equal).</summary>
        public const int Goal = 2500;

        private static readonly string[] OrderedIds = { GameComplete, Collection };

        /// <summary>The order the celebrations are shown in when more than one is owed: GAME COMPLETE first, then 100% COLLECTION.</summary>
        public static IReadOnlyList<string> Order => OrderedIds;

        public static bool IsKnown(string achievementId)
        {
            return Array.IndexOf(OrderedIds, achievementId) >= 0;
        }
    }
}
