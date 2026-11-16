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
