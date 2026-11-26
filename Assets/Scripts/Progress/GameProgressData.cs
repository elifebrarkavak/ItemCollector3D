using System;
using System.Collections.Generic;

namespace ItemCollector3D.Progress
{
    /// <summary>Whether the Green Mine tutorial is waiting, was finished / skipped, or was never offered (a game that existed before the tutorial).</summary>
    public enum TutorialStatus
    {
        NotOffered = 0,
        Pending = 1,
        Finished = 2,
    }

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

        /// <summary>The Green Mine tutorial (stage 17B), a <see cref="TutorialStatus"/> value. Saves written before it existed do not contain the field and read as 0 (not offered): old games never get the tutorial.</summary>
        public int tutorialState;

        /// <summary>
        /// Areas whose LEVEL COMPLETE celebration (stage 17C) was already shown: every treasure of the area collected. NOT the gold goal: reaching a gold threshold never writes here.
        /// Saves written before it existed do not contain the field and read as empty (nothing is celebrated again on load: the panel is opened by the collect event, never by loading).
        /// </summary>
        public List<string> celebratedAreaIds = new();

        /// <summary>
        /// Areas whose last treasure was collected but whose LEVEL COMPLETE celebration the player has not answered yet (set the moment the last treasure is collected, cleared when the player chooses
        /// STAY HERE / GO TO / AREA SELECTION). A scene change or a restart in between cannot lose the celebration: the area shows it once the next time it is entered. Saves from before it read as empty.
        /// </summary>
        public List<string> pendingCelebrationAreaIds = new();

        /// <summary>
        /// Final achievements (stage 17D, <see cref="FinalAchievements"/>) whose celebration is OWED: the goal was crossed by a COLLECT (written in the same save as that gold) and the player has not answered the
        /// panel yet. Whether the goal is reached is NOT stored here (it follows from <see cref="totalGold"/> and <see cref="collectedTreasureIds"/>). Saves from before 17D do not contain the field and read as empty.
        /// </summary>
        public List<string> pendingAchievementIds = new();

        /// <summary>Final achievements whose celebration the player already answered (never shown again until NEW GAME). A save from before 17D has none and is never "celebrated" on loading: the panel only follows a COLLECT.</summary>
        public List<string> celebratedAchievementIds = new();
    }
}
