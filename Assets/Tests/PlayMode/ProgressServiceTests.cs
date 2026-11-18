using System.Collections.Generic;
using System.IO;
using ItemCollector3D.Progress;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>Persistent progress: save/load, cumulative gold, area thresholds and locks, once-only events, corrupt data.</summary>
    public sealed class ProgressServiceTests
    {
        private readonly List<Object> created = new();

        [SetUp]
        public void SetUp()
        {
            ProgressTestIsolation.Warnings.Clear();
            expectedWarning = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (expectedWarning != null)
            {
                Assert.That(ProgressTestIsolation.Warnings.Exists(w => Regex.IsMatch(w, expectedWarning)), Is.True,
                    "Expected a warning matching '" + expectedWarning + "' but got: " + string.Join(" | ", ProgressTestIsolation.Warnings));
            }
            else
            {
                Assert.That(ProgressTestIsolation.Warnings, Is.Empty, "Unexpected progress warning(s).");
            }

            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            created.Clear();
        }

        private string expectedWarning;

        /// <summary>This test feeds bad data on purpose: a matching warning MUST be emitted (checked in TearDown); any other warning fails.</summary>
        private void ExpectWarning(string pattern)
        {
            ProgressTestIsolation.Warnings.Clear();
            expectedWarning = pattern;
        }

        private void ExpectRecoveryWarning()
        {
            ExpectWarning("Progress was (reset|repaired)");
        }

        private AreaCatalog Catalog()
        {
            AreaCatalog catalog = AreaCatalog.CreateDefault();
            created.Add(catalog);
            return catalog;
        }

        private ProgressService NewService(IProgressStore store, List<string> log = null)
        {
            ProgressService service = ProgressService.Create(Catalog(), store);
            if (log != null)
            {
                service.AreaCompleted += id => log.Add("complete:" + id);
                service.AreaUnlocked += id => log.Add("unlock:" + id);
            }

            return service;
        }

        // ------------------------------------------------------------ catalog

        [Test]
        public void DefaultCatalog_MatchesConceptDocumentThresholds_AndIsValid()
        {
            AreaCatalog catalog = Catalog();
            Assert.That(catalog.Validate(out string error), Is.True, error);
            Assert.That(catalog.Areas.Count, Is.EqualTo(3));
            Assert.That((catalog.Areas[0].id, catalog.Areas[0].unlockTotalGold, catalog.Areas[0].goalTotalGold), Is.EqualTo(("green_mine", 0, 500)));
            Assert.That((catalog.Areas[1].id, catalog.Areas[1].unlockTotalGold, catalog.Areas[1].goalTotalGold), Is.EqualTo(("rocky_canyon", 500, 1200)));
            Assert.That((catalog.Areas[2].id, catalog.Areas[2].unlockTotalGold, catalog.Areas[2].goalTotalGold), Is.EqualTo(("ancient_mine", 1200, 2500)));
        }

        [Test]
        public void Catalog_Validation_RejectsBrokenData()
        {
            AreaCatalog dup = Catalog();
            dup.Configure(new[] { new AreaEntry("a", "A", 0, 100), new AreaEntry("a", "A2", 100, 200) });
            Assert.That(dup.Validate(out _), Is.False, "duplicate ids");

            AreaCatalog flat = Catalog();
            flat.Configure(new[] { new AreaEntry("a", "A", 0, 100), new AreaEntry("b", "B", 100, 100) });
            Assert.That(flat.Validate(out _), Is.False, "goal must exceed unlock");

            AreaCatalog firstClosed = Catalog();
            firstClosed.Configure(new[] { new AreaEntry("a", "A", 50, 100) });
            Assert.That(firstClosed.Validate(out _), Is.False, "first area must be open");

            AreaCatalog empty = Catalog();
            empty.Configure(new AreaEntry[0]);
            Assert.That(empty.Validate(out _), Is.False);
        }

        [Test]
        public void InvalidCatalog_FallsBackToDefaultAreas()
        {
            ExpectWarning("AreaCatalog is invalid");
            AreaCatalog broken = Catalog();
            broken.Configure(new AreaEntry[0]);
            ProgressService service = ProgressService.Create(broken, new MemoryProgressStore());
            created.Add(service.Catalog);
            Assert.That(service.Catalog.Areas.Count, Is.EqualTo(3));
            Assert.That(service.IsAreaUnlocked("green_mine"), Is.True);
        }

        // ------------------------------------------------------------ new game + thresholds

        [Test]
        public void NewGame_StartsAtZero_OnlyGreenMineUnlocked()
        {
            ProgressService service = NewService(new MemoryProgressStore());
            Assert.That(service.LoadStatus, Is.EqualTo(ProgressLoadStatus.NewGame));
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.IsAreaUnlocked("green_mine"), Is.True, "Green Mine is open from the start.");
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False);
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False);
            Assert.That(service.IsAreaCompleted("green_mine"), Is.False);
            Assert.That(service.GetGoal("green_mine"), Is.EqualTo(500));
            Assert.That(service.GetGoal("rocky_canyon"), Is.EqualTo(1200));
            Assert.That(service.GetGoal("ancient_mine"), Is.EqualTo(2500));
            Assert.That(service.GetGoal("nope"), Is.EqualTo(-1));
        }

        [Test]
        public void Thresholds_UnlockAndComplete_OncePerArea_WithCumulativeGold()
        {
            var log = new List<string>();
            ProgressService service = NewService(new MemoryProgressStore(), log);

            Assert.That(service.RegisterCollected("t1", 499), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(log, Is.Empty, "499 < 500: nothing yet.");
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False);

            service.RegisterCollected("t2", 1); // exactly 500
            Assert.That(service.TotalGold, Is.EqualTo(500));
            Assert.That(log, Is.EqualTo(new[] { "complete:green_mine", "unlock:rocky_canyon" }),
                "AREA COMPLETE then NEW AREA UNLOCKED.");
            Assert.That(service.IsAreaCompleted("green_mine"), Is.True);
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False);

            log.Clear();
            service.RegisterCollected("t3", 100);
            service.RegisterCollected("t4", 50);
            Assert.That(service.TotalGold, Is.EqualTo(650), "Gold is cumulative: nothing resets between areas.");
            Assert.That(log, Is.Empty, "The same notification must not fire again.");

            service.RegisterCollected("t5", 550); // 1200
            Assert.That(log, Is.EqualTo(new[] { "complete:rocky_canyon", "unlock:ancient_mine" }));
            log.Clear();

            service.RegisterCollected("t6", 1299);
            Assert.That(service.TotalGold, Is.EqualTo(2499));
            Assert.That(log, Is.Empty);
            service.RegisterCollected("t7", 1); // 2500
            Assert.That(log, Is.EqualTo(new[] { "complete:ancient_mine" }), "The last area has no next area to unlock.");
            log.Clear();

            service.RegisterCollected("t8", 100);
            Assert.That(log, Is.Empty);
            Assert.That(service.TotalGold, Is.EqualTo(2600));
        }

        [Test]
        public void OneBigCollection_CrossingSeveralThresholds_RaisesEveryEventOnce_InOrder()
        {
            var log = new List<string>();
            ProgressService service = NewService(new MemoryProgressStore(), log);
            service.RegisterCollected("a", 400);
            service.RegisterCollected("b", 900); // 1300: crosses 500 and 1200 together
            Assert.That(log, Is.EqualTo(new[]
            {
                "complete:green_mine", "unlock:rocky_canyon", "complete:rocky_canyon", "unlock:ancient_mine"
            }));
        }

        // ------------------------------------------------------------ once per treasure

        [Test]
        public void SameTreasureId_PaysGoldOnlyOnce()
        {
            ProgressService service = NewService(new MemoryProgressStore());
            Assert.That(service.RegisterCollected("coin", 10), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(service.RegisterCollected("coin", 10), Is.EqualTo(RegisterResult.AlreadyCollected));
            Assert.That(service.RegisterCollected("  coin  ", 10), Is.EqualTo(RegisterResult.AlreadyCollected), "ids are trimmed");
            Assert.That(service.TotalGold, Is.EqualTo(10));
            Assert.That(service.IsCollected("coin"), Is.True);
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidRegistrations_AreRejected_WithoutChangingProgress()
        {
            var store = new MemoryProgressStore();
            ProgressService service = NewService(store);
            Assert.That(service.RegisterCollected(null, 10), Is.EqualTo(RegisterResult.InvalidId));
            Assert.That(service.RegisterCollected("", 10), Is.EqualTo(RegisterResult.InvalidId));
            Assert.That(service.RegisterCollected("   ", 10), Is.EqualTo(RegisterResult.InvalidId));
            Assert.That(service.RegisterCollected("x", -5), Is.EqualTo(RegisterResult.InvalidGold));
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.CollectedTreasureIds, Is.Empty);
            Assert.That(store.WriteCount, Is.EqualTo(0), "Nothing to save.");
        }

        // ------------------------------------------------------------ save / load

        [Test]
        public void SaveLoad_RoundTrip_RestoresGoldIdsAndAreas_WithoutRaisingEventsAgain()
        {
            var store = new MemoryProgressStore();
            ProgressService first = NewService(store);
            first.RegisterCollected("green_mine.coin_1", 10);
            first.RegisterCollected("green_mine.bar_1", 490); // 500: green complete, rocky unlocked
            first.RegisterCollected("rocky.nugget_1", 25);
            Assert.That(store.WriteCount, Is.EqualTo(3), "Saved after every collected treasure.");

            var log = new List<string>();
            ProgressService second = NewService(store, log); // "the game was restarted"
            Assert.That(second.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(second.TotalGold, Is.EqualTo(525));
            Assert.That(second.CollectedTreasureIds, Is.EqualTo(new[] { "green_mine.coin_1", "green_mine.bar_1", "rocky.nugget_1" }));
            Assert.That(second.IsAreaCompleted("green_mine"), Is.True);
            Assert.That(second.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(second.IsAreaUnlocked("ancient_mine"), Is.False);
            Assert.That(log, Is.Empty, "Loading must not repeat AREA COMPLETE / NEW AREA UNLOCKED.");

            Assert.That(second.RegisterCollected("green_mine.coin_1", 10), Is.EqualTo(RegisterResult.AlreadyCollected),
                "A treasure collected before the restart pays nothing again.");
            Assert.That(second.TotalGold, Is.EqualTo(525));
            second.RegisterCollected("rocky.bar_1", 50);
            Assert.That(log, Is.Empty);
            Assert.That(second.TotalGold, Is.EqualTo(575));
        }

        [Test]
        public void ResetAll_ClearsProgress_WithoutRaisingEvents()
        {
            var store = new MemoryProgressStore();
            var log = new List<string>();
            ProgressService service = NewService(store, log);
            service.RegisterCollected("a", 600);
            log.Clear();
            service.ResetAll();
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False);
            Assert.That(service.CollectedTreasureIds, Is.Empty);
            Assert.That(log, Is.Empty);
            Assert.That(store.Json, Is.Null);
        }

        // ------------------------------------------------------------ corrupt / missing data

        [TestCase("not json {{")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{\"version\":99,\"totalGold\":5}")]
        [TestCase("{\"version\":-3}")]
        [TestCase("[1,2,3]")]
        [TestCase("\0\0\0garbage")]
        public void CorruptSave_IsRecovered_BackedUp_AndReplacedWithCleanData(string bad)
        {
            ExpectRecoveryWarning();
            var store = new MemoryProgressStore(bad);
            ProgressService service = null;
            Assert.DoesNotThrow(() => service = NewService(store));
            Assert.That(service.LoadStatus, Is.EqualTo(ProgressLoadStatus.Recovered));
            Assert.That(service.RecoveryNote, Is.Not.Empty);
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.IsAreaUnlocked("green_mine"), Is.True);
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False);
            Assert.That(store.BackupCount, Is.EqualTo(1), "The unreadable data is kept as a backup.");
            Assert.That(store.LastBackup, Is.EqualTo(bad));

            // the bad file was replaced: loading again is clean
            ProgressService again = NewService(store);
            Assert.That(again.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(again.TotalGold, Is.EqualTo(0));
        }

        [Test]
        public void MissingFields_AreFilledWithSafeDefaults()
        {
            ExpectRecoveryWarning(); // an incomplete save (no area list) is repaired, backed up and reported
            ProgressService service = NewService(new MemoryProgressStore("{\"version\":1}"));
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.IsAreaUnlocked("green_mine"), Is.True);
            Assert.That(service.CollectedTreasureIds, Is.Empty);
            Assert.That(service.RegisterCollected("x", 5), Is.EqualTo(RegisterResult.Accepted));
        }

        [Test]
        public void InconsistentButReadableData_IsRepaired()
        {
            ExpectRecoveryWarning();
            string json = "{\"version\":1,\"totalGold\":-40,\"collectedTreasureIds\":[\"a\",\"\",\"a\",\"b\",\"  \"],"
                + "\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\",\"narnia\"],\"completedAreaIds\":[\"rocky_canyon\"]}";
            var store = new MemoryProgressStore(json);
            ProgressService service = NewService(store);
            Assert.That(service.LoadStatus, Is.EqualTo(ProgressLoadStatus.Recovered));
            Assert.That(service.TotalGold, Is.EqualTo(0), "negative gold is clamped");
            Assert.That(service.CollectedTreasureIds, Is.EqualTo(new[] { "a", "b" }), "empty/duplicate ids removed");
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False, "an unlock without the gold for it is dropped");
            Assert.That(service.IsAreaCompleted("rocky_canyon"), Is.False);
            Assert.That(service.UnlockedAreaIds, Is.EqualTo(new[] { "green_mine" }), "unknown area ids are dropped");
            Assert.That(store.BackupCount, Is.EqualTo(1));
        }

        [Test]
        public void GoldAboveThreshold_WithMissingFlags_IsRepairedSilently()
        {
            ExpectRecoveryWarning();
            var store = new MemoryProgressStore("{\"version\":1,\"totalGold\":600,\"collectedTreasureIds\":[\"x\"]}");
            var log = new List<string>();
            ProgressService service = NewService(store, log);
            Assert.That(service.IsAreaCompleted("green_mine"), Is.True);
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(log, Is.Empty, "A repair on load must not replay notifications.");
        }

        [Test]
        public void AbsurdGold_IsClamped()
        {
            ExpectRecoveryWarning();
            ProgressService service = NewService(new MemoryProgressStore("{\"version\":1,\"totalGold\":2147483647}"));
            Assert.That(service.TotalGold, Is.EqualTo(ProgressService.MaxTotalGold));
            service.RegisterCollected("big", int.MaxValue);
            Assert.That(service.TotalGold, Is.EqualTo(ProgressService.MaxTotalGold), "no int overflow");
        }

        [Test]
        public void NullStore_DoesNotCrash()
        {
            ProgressService service = ProgressService.Create(Catalog(), null);
            Assert.That(service.RegisterCollected("a", 5), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(service.TotalGold, Is.EqualTo(5));
        }

        // ------------------------------------------------------------ real file store

        [Test]
        public void FileStore_RoundTrips_RecoversFromCorruptFile_AndNeverThrows()
        {
            ExpectRecoveryWarning();
            string name = "test_progress_" + System.Guid.NewGuid().ToString("N") + ".json";
            var store = new FileProgressStore(name);
            string path = store.FilePath;
            try
            {
                Assert.That(store.TryRead(out _), Is.False, "no file yet");
                ProgressService first = NewService(store);
                Assert.That(first.LoadStatus, Is.EqualTo(ProgressLoadStatus.NewGame));
                first.RegisterCollected("file.coin", 10);
                Assert.That(File.Exists(path), Is.True);

                ProgressService second = NewService(new FileProgressStore(name));
                Assert.That(second.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
                Assert.That(second.TotalGold, Is.EqualTo(10));
                Assert.That(second.IsCollected("file.coin"), Is.True);

                File.WriteAllText(path, "}{ definitely not json");
                ProgressService third = NewService(new FileProgressStore(name));
                Assert.That(third.LoadStatus, Is.EqualTo(ProgressLoadStatus.Recovered));
                Assert.That(third.TotalGold, Is.EqualTo(0));
                Assert.That(File.Exists(path + ".corrupt"), Is.True, "the bad file is kept as a backup");
            }
            finally
            {
                foreach (string suffix in new[] { "", ".tmp", ".corrupt" })
                {
                    if (File.Exists(path + suffix))
                    {
                        File.Delete(path + suffix);
                    }
                }
            }
        }
    }
}
