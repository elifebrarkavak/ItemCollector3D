using System.Collections.Generic;
using ItemCollector3D.Progress;
using NUnit.Framework;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Runs once for every PlayMode test in this namespace. GameplayTest contains a ProgressManager, which would otherwise read and
    /// write a REAL save file: tests would leak progress into each other and into the developer's own save. This forces a fresh,
    /// in-memory store each time a ProgressManager initialises, so tests never touch the disk.
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
        }

        [OneTimeTearDown]
        public void RestoreProgressStore()
        {
            ProgressManager.StoreFactoryOverride = null;
            ProgressLog.WarningSink = null;
        }
    }
}
