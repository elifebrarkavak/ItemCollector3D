using System;
using System.Collections.Generic;

namespace ItemCollector3D.Progress
{
    /// <summary>
    /// The saved game state (serialised with JsonUtility, so public fields). Gold is CUMULATIVE across areas.
    /// </summary>
    [Serializable]
    public sealed class GameProgressData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int totalGold;
        public List<string> collectedTreasureIds = new();
        public List<string> unlockedAreaIds = new();
        public List<string> completedAreaIds = new();
    }
}
