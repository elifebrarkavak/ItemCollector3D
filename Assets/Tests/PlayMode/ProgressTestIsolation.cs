using System.Collections.Generic;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using NUnit.Framework;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Runs once for every PlayMode test in this namespace. GameplayTest contains a ProgressManager, which would otherwise read and
    /// write a REAL save file: tests would leak progress into each other and into the developer's own save. This forces a fresh,
    /// in-memory store each time a ProgressManager initialises, and an in-memory settings store, so tests never touch the disk or PlayerPrefs.
    /// </summary>
    [SetUpFixture]
    public sealed class ProgressTestIsolation
    {
        /// <summary>Progress warnings captured during the tests (instead of being printed to the Console).</summary>
        public static readonly List<string> Warnings = new();

        /// <summary>
        /// An in-memory store that already holds a saved game (0 gold, only Green Mine open). Since stage 18B the Main Menu tells a real first launch (no save at all: START ADVENTURE, introduction, tutorial)
        /// from a game in progress (CONTINUE GAME), so menu tests that press PLAY to reach the Area Selection need a save to continue.
        /// </summary>
        public static MemoryProgressStore SavedGameStore()
        {
            var store = new MemoryProgressStore();
            ProgressService.Create(null, store).Save();
            return store;
        }

        /// <summary>Factory form of <see cref="SavedGameStore"/> for <c>ProgressManager.StoreFactoryOverride</c>.</summary>
        public static IProgressStore SavedGame() => SavedGameStore();

        [OneTimeSetUp]
        public void UseInMemoryProgressStore()
        {
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            ProgressLog.WarningSink = message => Warnings.Add(message);

            // Settings: never read or write the real PlayerPrefs during tests either.
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
        }

        [OneTimeTearDown]
        public void RestoreProgressStore()
        {
            ProgressManager.StoreFactoryOverride = null;
            ProgressLog.WarningSink = null;
            GameSettings.StoreFactoryOverride = null;
            GameSettings.ResetCurrent();
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            MainMenuController.ExitHook = null;
        }
    }
}
