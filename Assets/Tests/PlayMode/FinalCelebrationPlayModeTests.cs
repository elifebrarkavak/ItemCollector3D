#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ItemCollector3D.Audio;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>The save and the rules of the two final achievements (stage 17D), as plain C# over in-memory stores: nothing here touches a scene or the developer's real save.</summary>
    public sealed class FinalAchievementsServiceTests
    {
        private const string CatalogPath = "Assets/Data/Areas/AreaCatalog.asset";
        private AreaCatalog catalog;

        [SetUp]
        public void Load()
        {
            catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
        }

        internal static string Json(IEnumerable<string> ids, int gold, IEnumerable<string> pending = null, IEnumerable<string> celebrated = null, bool oldFormat = false)
        {
            var data = new GameProgressData { totalGold = gold };
            data.collectedTreasureIds.AddRange(ids);
            data.unlockedAreaIds.Add("green_mine");
            if (gold >= 500) { data.unlockedAreaIds.Add("rocky_canyon"); data.completedAreaIds.Add("green_mine"); }
            if (gold >= 1200) { data.unlockedAreaIds.Add("ancient_mine"); data.completedAreaIds.Add("rocky_canyon"); }
            if (gold >= 2500) { data.completedAreaIds.Add("ancient_mine"); }
            if (pending != null) { data.pendingAchievementIds.AddRange(pending); }
            if (celebrated != null) { data.celebratedAchievementIds.AddRange(celebrated); }
            string json = JsonUtility.ToJson(data);
            if (oldFormat)
            {
                // a save from before 17C / 17D: none of the celebration fields exist
                json = json.Replace(",\"celebratedAreaIds\":[]", string.Empty).Replace(",\"pendingCelebrationAreaIds\":[]", string.Empty)
                    .Replace(",\"pendingAchievementIds\":[]", string.Empty).Replace(",\"celebratedAchievementIds\":[]", string.Empty);
                Assert.That(json, Does.Not.Contain("elebrat"), "an old save has none of the celebration fields");
                Assert.That(json, Does.Not.Contain("pendingAchievementIds"));
            }

            return json;
        }

        private ProgressService Open(MemoryProgressStore store) => ProgressService.Create(catalog, store);

        private static GameProgressData Saved(MemoryProgressStore s) => JsonUtility.FromJson<GameProgressData>(s.Json);

        [Test]
        public void TheTreasureCatalog_HasTheSixtyIds_FifteenTwentyTwentyFive_AndTheGoalIsTheLastAreasGoal()
        {
            Assert.That(TreasureCatalog.TotalCount, Is.EqualTo(60));
            Assert.That(TreasureCatalog.AllIds.Count, Is.EqualTo(60));
            Assert.That(TreasureCatalog.AllIds.Distinct().Count(), Is.EqualTo(60), "every id once");
            Assert.That(TreasureCatalog.IdsOf("green_mine").Count, Is.EqualTo(15));
            Assert.That(TreasureCatalog.IdsOf("rocky_canyon").Count, Is.EqualTo(20));
            Assert.That(TreasureCatalog.IdsOf("ancient_mine").Count, Is.EqualTo(25));
            Assert.That(TreasureCatalog.IdsOf("nowhere"), Is.Empty);
            foreach (string area in new[] { "green_mine", "rocky_canyon", "ancient_mine" })
            {
                Assert.That(TreasureCatalog.IdsOf(area).All(id => id.StartsWith(area + ".")), Is.True, area + ": every id carries the area prefix");
            }

            Assert.That(TreasureCatalog.IsKnown("green_mine.camp_01"), Is.True);
            Assert.That(TreasureCatalog.IsKnown("green_mine.gold_coin_01"), Is.False, "the sandbox coin is not one of the 60");
            Assert.That(TreasureCatalog.IsKnown("GREEN_MINE.CAMP_01"), Is.False, "ids are exact");
            Assert.That(TreasureCatalog.IsKnown(""), Is.False);
            Assert.That(TreasureCatalog.IsKnown(null), Is.False);
            Assert.That(FinalAchievements.Goal, Is.EqualTo(2500));
            Assert.That(catalog.Get("ancient_mine").goalTotalGold, Is.EqualTo(FinalAchievements.Goal), "the game goal is the goal of the last area");
            Assert.That(FinalAchievements.Order, Is.EqualTo(new[] { "game_complete", "collection_complete" }), "GAME COMPLETE first");
        }

        [Test]
        public void Gold_2499_IsNotTheGoal_2500_Is_AndGoldAboveItIsKept_Up_To_2705()
        {
            var store = new MemoryProgressStore();
            ProgressService s = Open(store);
            Assert.That(s.RegisterCollected("green_mine.camp_01", 2499), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(s.TotalGold, Is.EqualTo(2499));
            Assert.That(s.IsGameCompleteReached, Is.False, "2499 is not 2500");
            Assert.That(s.PendingAchievementIds, Is.Empty, "no GAME COMPLETE owed at 2499");
            Assert.That(s.NextPendingAchievement(), Is.Null);

            Assert.That(s.RegisterCollected("green_mine.forest_01", 1), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(s.TotalGold, Is.EqualTo(2500), "2500 exactly: nothing is reset or clamped");
            Assert.That(s.IsGameCompleteReached, Is.True);
            Assert.That(s.IsAchievementReached(FinalAchievements.GameComplete), Is.True);
            Assert.That(s.IsAchievementPending(FinalAchievements.GameComplete), Is.True, "the crossing owes the celebration");
            Assert.That(s.IsAchievementCelebrated(FinalAchievements.GameComplete), Is.False);
            Assert.That(s.NextPendingAchievement(), Is.EqualTo(FinalAchievements.GameComplete));
            Assert.That(s.IsAchievementPending(FinalAchievements.Collection), Is.False, "2 / 60 treasures: no collection");
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "game_complete" }), "owed in the save");

            // the gold goes on above the goal and is saved as it is: 2705 is what the three maps can pay
            Assert.That(s.RegisterCollected("green_mine.mine_01", 205), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(s.TotalGold, Is.EqualTo(2705));
            Assert.That(Saved(store).totalGold, Is.EqualTo(2705), "the real saved gold is 2705, not 2500");
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "game_complete" }), "the goal is crossed once: still just one owed record");
            Assert.That(Open(new MemoryProgressStore(store.Json)).TotalGold, Is.EqualTo(2705), "and it loads back as 2705");
        }

        [Test]
        public void TheGoal_NeedsNoCollection_AndTheCollection_NeedsNoGoal_AndNeitherIsTheAreaGoalFlag()
        {
            // (a) 2500 gold with 54 treasures: GAME COMPLETE, not the collection
            List<string> ids = TreasureCatalog.AllIds.Take(54).ToList();
            var storeA = new MemoryProgressStore(Json(ids, 2499));
            ProgressService a = Open(storeA);
            a.RegisterCollected("rocky_canyon.fake_99", 1); // an unknown id still pays its gold (as before) ...
            Assert.That(a.TotalGold, Is.EqualTo(2500));
            Assert.That(a.PendingAchievementIds, Is.EqualTo(new[] { "game_complete" }));
            Assert.That(a.CollectedTreasureCount, Is.EqualTo(54), "... but is not one of the 60");
            Assert.That(a.IsCollectionComplete, Is.False);
            Assert.That(a.IsAreaCompleted("ancient_mine"), Is.True, "IsAreaCompleted is the GOLD goal of the area");

            // (b) 60 treasures but little gold: the collection, not the game goal, and the gold-based area flag stays false
            var storeB = new MemoryProgressStore(Json(TreasureCatalog.AllIds.Take(59), 100));
            ProgressService b = Open(storeB);
            Assert.That(b.CollectedTreasureCount, Is.EqualTo(59));
            Assert.That(b.IsCollectionComplete, Is.False, "59 / 60");
            Assert.That(b.PendingAchievementIds, Is.Empty, "59 / 60: no collection celebration");
            string last = TreasureCatalog.AllIds[59];
            Assert.That(b.RegisterCollected(last, 10), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(b.IsCollectionComplete, Is.True, "60 / 60");
            Assert.That(b.TotalGold, Is.EqualTo(110), "the collection pays nothing of its own: only the treasure's own gold");
            Assert.That(b.PendingAchievementIds, Is.EqualTo(new[] { "collection_complete" }));
            Assert.That(b.IsGameCompleteReached, Is.False, "110 gold is not the goal");
            Assert.That(b.IsAreaCompleted("ancient_mine"), Is.False, "the gold-based area flag is not the collection");
            Assert.That(b.CollectedTreasureCountIn("green_mine"), Is.EqualTo(15));
            Assert.That(b.CollectedTreasureCountIn("rocky_canyon"), Is.EqualTo(20));
            Assert.That(b.CollectedTreasureCountIn("ancient_mine"), Is.EqualTo(25));
        }

        [Test]
        public void TheCollection_CountsOnlyTheSixtyKnownIds_NeverUnknownBlankOrDuplicateOnes()
        {
            // a save with 55 real ids and rubbish: unknown, blank, a case variant, a prefix that looks right, and duplicates
            List<string> real = TreasureCatalog.AllIds.Take(55).ToList();
            var messy = new List<string>(real) { "green_mine.gold_coin_01", "  ", string.Empty, "GREEN_MINE.CAMP_01", "rocky_canyon.fake_99", real[0], real[1] };
            ProgressService s = Open(new MemoryProgressStore(Json(messy, 1000)));
            Assert.That(s.CollectedTreasureCount, Is.EqualTo(55), "only the real ids count, each once");

            // the 5 missing ones: four real ones, then five unknown ones, then the last real one
            for (int i = 55; i < 59; i++)
            {
                s.RegisterCollected(TreasureCatalog.AllIds[i], 1);
            }

            Assert.That(s.CollectedTreasureCount, Is.EqualTo(59));
            for (int i = 0; i < 5; i++)
            {
                s.RegisterCollected($"ancient_mine.unknown_{i:00}", 1);
            }

            Assert.That(s.RegisterCollected(TreasureCatalog.AllIds[0], 1), Is.EqualTo(RegisterResult.AlreadyCollected), "a duplicate is refused");
            Assert.That(s.RegisterCollected(" " + TreasureCatalog.AllIds[1] + " ", 1), Is.EqualTo(RegisterResult.AlreadyCollected), "also with spaces around it");
            Assert.That(s.CollectedTreasureCount, Is.EqualTo(59), "unknown ids and duplicates never completed it");
            Assert.That(s.IsCollectionComplete, Is.False);
            Assert.That(s.IsAchievementPending(FinalAchievements.Collection), Is.False);
            Assert.That(s.RegisterCollected(TreasureCatalog.AllIds[59], 1), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(s.IsCollectionComplete, Is.True);
            Assert.That(s.IsAchievementPending(FinalAchievements.Collection), Is.True);
        }

        [Test]
        public void BothAtOnce_AreOwedInOneSave_GameCompleteFirst_AndAnsweringOneKeepsTheOther()
        {
            var store = new MemoryProgressStore(Json(TreasureCatalog.AllIds.Take(59), 2490));
            ProgressService s = Open(store);
            int writes = store.WriteCount;
            Assert.That(s.RegisterCollected(TreasureCatalog.AllIds[59], 10), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(store.WriteCount, Is.EqualTo(writes + 1), "the gold, the treasure and BOTH owed celebrations are written in one save");
            Assert.That(s.TotalGold, Is.EqualTo(2500));
            GameProgressData saved = Saved(store);
            Assert.That(saved.pendingAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(saved.celebratedAchievementIds, Is.Empty);
            Assert.That(s.NextPendingAchievement(), Is.EqualTo("game_complete"), "GAME COMPLETE comes first");

            Assert.That(s.MarkAchievementCelebrated("game_complete"), Is.True);
            Assert.That(s.NextPendingAchievement(), Is.EqualTo("collection_complete"), "then 100% COLLECTION");
            Assert.That(Saved(store).celebratedAchievementIds, Is.EqualTo(new[] { "game_complete" }));
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "collection_complete" }));
            Assert.That(s.IsAchievementReached("game_complete"), Is.True, "answering does not change whether the goal is reached");

            // the owed one survives a restart between the two panels
            ProgressService restarted = Open(new MemoryProgressStore(store.Json));
            Assert.That(restarted.NextPendingAchievement(), Is.EqualTo("collection_complete"));
            Assert.That(restarted.IsAchievementCelebrated("game_complete"), Is.True);

            Assert.That(s.MarkAchievementCelebrated("collection_complete"), Is.True);
            Assert.That(s.NextPendingAchievement(), Is.Null);
            Assert.That(s.MarkAchievementCelebrated("collection_complete"), Is.True, "answering twice is harmless");
            GameProgressData end = Saved(store);
            Assert.That(end.celebratedAchievementIds, Is.EqualTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(end.pendingAchievementIds, Is.Empty);
            Assert.That(end.totalGold, Is.EqualTo(2500), "no gold was added by either celebration");
            Assert.That(end.collectedTreasureIds.Count, Is.EqualTo(60));
        }

        [Test]
        public void TheThreeQuestions_AreSeparate_Reached_Owed_Answered_AndSurviveARestart()
        {
            var store = new MemoryProgressStore(Json(TreasureCatalog.AllIds.Take(3), 2499));
            ProgressService s = Open(store);
            string g = FinalAchievements.GameComplete;
            Assert.That((s.IsAchievementReached(g), s.IsAchievementPending(g), s.IsAchievementCelebrated(g)), Is.EqualTo((false, false, false)));
            s.RegisterCollected(TreasureCatalog.AllIds[3], 1);
            Assert.That((s.IsAchievementReached(g), s.IsAchievementPending(g), s.IsAchievementCelebrated(g)), Is.EqualTo((true, true, false)), "reached and owed");
            var restarted = new MemoryProgressStore(store.Json); // the app was closed before the panel was answered
            s = Open(restarted);
            Assert.That((s.IsAchievementReached(g), s.IsAchievementPending(g), s.IsAchievementCelebrated(g)), Is.EqualTo((true, true, false)), "still owed after a restart");
            s.MarkAchievementCelebrated(g);
            Assert.That((s.IsAchievementReached(g), s.IsAchievementPending(g), s.IsAchievementCelebrated(g)), Is.EqualTo((true, false, true)), "answered");
            s = Open(new MemoryProgressStore(restarted.Json));
            Assert.That((s.IsAchievementReached(g), s.IsAchievementPending(g), s.IsAchievementCelebrated(g)), Is.EqualTo((true, false, true)), "answered stays answered");
            s.RegisterCollected(TreasureCatalog.AllIds[4], 50);
            Assert.That(s.IsAchievementPending(g), Is.False, "more gold never owes it again");
            Assert.That(s.MarkAchievementCelebrated(FinalAchievements.Collection), Is.False, "an unreached goal cannot be marked");
            Assert.That(s.MarkAchievementCelebrated("bogus"), Is.False);
            Assert.That(s.MarkAchievementCelebrated(null), Is.False);
        }

        [Test]
        public void OldSaves_LoadAsTheyAre_NothingIsOwedForAGoalPassedBeforeThisVersion_AndNoWriteHappens()
        {
            // (a) 2600 gold, 40 treasures, no 17C / 17D fields at all
            string past = Json(TreasureCatalog.AllIds.Take(40), 2600, oldFormat: true);
            var storeA = new MemoryProgressStore(past);
            ProgressService a = Open(storeA);
            Assert.That(a.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded), "not repaired");
            Assert.That(storeA.WriteCount, Is.EqualTo(0), "loading never rewrites an old save");
            Assert.That(storeA.Json, Is.EqualTo(past));
            Assert.That(a.TotalGold, Is.EqualTo(2600));
            Assert.That(a.IsGameCompleteReached, Is.True, "the goal IS reached (a fact of the gold) ...");
            Assert.That(a.PendingAchievementIds, Is.Empty, "... but nothing is owed: the player is past it");
            Assert.That(a.IsAchievementCelebrated(FinalAchievements.GameComplete), Is.False);
            a.RegisterCollected(TreasureCatalog.AllIds[40], 30);
            Assert.That(a.PendingAchievementIds, Is.Empty, "collecting more never celebrates the old crossing");
            Assert.That(a.TotalGold, Is.EqualTo(2630));

            // (b) a finished old game: 60 treasures and 2705 gold
            var storeB = new MemoryProgressStore(Json(TreasureCatalog.AllIds, 2705, oldFormat: true));
            ProgressService b = Open(storeB);
            Assert.That(b.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(storeB.WriteCount, Is.EqualTo(0));
            Assert.That(b.IsCollectionComplete && b.IsGameCompleteReached, Is.True);
            Assert.That(b.PendingAchievementIds, Is.Empty);
            Assert.That(b.CelebratedAchievementIds, Is.Empty);

            // (c) an old save that crosses the goal AFTER this version: that crossing counts
            var storeC = new MemoryProgressStore(Json(TreasureCatalog.AllIds.Take(40), 2400, oldFormat: true));
            ProgressService c = Open(storeC);
            c.RegisterCollected(TreasureCatalog.AllIds[40], 100);
            Assert.That(c.PendingAchievementIds, Is.EqualTo(new[] { "game_complete" }));

            // (d) an old save that is one treasure short of the collection, past the gold goal: only the collection is owed
            var storeD = new MemoryProgressStore(Json(TreasureCatalog.AllIds.Take(59), 2650, oldFormat: true));
            ProgressService d = Open(storeD);
            d.RegisterCollected(TreasureCatalog.AllIds[59], 40);
            Assert.That(d.PendingAchievementIds, Is.EqualTo(new[] { "collection_complete" }));
            Assert.That(d.TotalGold, Is.EqualTo(2690));
        }

        [Test]
        public void OddSaveData_IsTidiedSilently_NothingIsInvented_AndCelebrationsStayIndependent()
        {
            // owed for goals that are not reached, unknown ids, duplicates, and an owed record that is also answered
            var odd = new GameProgressData { totalGold = 100 };
            odd.collectedTreasureIds.AddRange(TreasureCatalog.AllIds.Take(3));
            odd.unlockedAreaIds.Add("green_mine");
            odd.pendingAchievementIds.AddRange(new[] { "game_complete", "game_complete", "bogus", "collection_complete", null });
            odd.celebratedAchievementIds.AddRange(new[] { "bogus", "collection_complete", "collection_complete", " " });
            string json = JsonUtility.ToJson(odd);
            var store = new MemoryProgressStore(json);
            ProgressService s = Open(store);
            Assert.That(s.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded), "tidied silently: no 'repair'");
            Assert.That(store.WriteCount, Is.EqualTo(0));
            Assert.That(s.PendingAchievementIds, Is.Empty, "nothing is owed for goals that are not reached");
            Assert.That(s.CelebratedAchievementIds, Is.EqualTo(new[] { "collection_complete" }), "unknown ids and duplicates are dropped");
            Assert.That(s.TotalGold, Is.EqualTo(100));
            Assert.That(s.CollectedTreasureCount, Is.EqualTo(3));

            // a reached goal that was also marked answered is not owed
            var both = new GameProgressData { totalGold = 2600 };
            both.unlockedAreaIds.AddRange(new[] { "green_mine", "rocky_canyon", "ancient_mine" });
            both.completedAreaIds.AddRange(new[] { "green_mine", "rocky_canyon", "ancient_mine" });
            both.pendingAchievementIds.Add("game_complete");
            both.celebratedAchievementIds.Add("game_complete");
            ProgressService t = Open(new MemoryProgressStore(JsonUtility.ToJson(both)));
            Assert.That(t.PendingAchievementIds, Is.Empty);
            Assert.That(t.IsAchievementCelebrated("game_complete"), Is.True);

            // the 17C area celebrations and the 17D achievements do not touch each other
            var store3 = new MemoryProgressStore(Json(TreasureCatalog.AllIds.Take(59), 2490));
            ProgressService u = Open(store3);
            Assert.That(u.MarkCompletionPending("green_mine"), Is.True);
            u.RegisterCollected(TreasureCatalog.AllIds[59], 10);
            Assert.That(u.IsCompletionPending("green_mine"), Is.True, "the owed LEVEL COMPLETE of the area is still owed");
            u.MarkAchievementCelebrated("game_complete");
            u.MarkAchievementCelebrated("collection_complete");
            Assert.That(u.IsCompletionPending("green_mine"), Is.True, "answering the final celebrations does not answer LEVEL COMPLETE");
            Assert.That(u.IsCompletionCelebrated("green_mine"), Is.False);
            u.MarkCompletionCelebrated("green_mine");
            Assert.That(u.CelebratedAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }));
        }

        [Test]
        public void NewGame_ClearsEveryFinalState_AndTheNextCrossingOwesItAgain()
        {
            var store = new MemoryProgressStore(Json(TreasureCatalog.AllIds.Take(59), 2490));
            ProgressService s = Open(store);
            s.RegisterCollected(TreasureCatalog.AllIds[59], 10);
            s.MarkAchievementCelebrated("game_complete"); // one answered, one still owed
            Assert.That(s.CelebratedAchievementIds, Is.EqualTo(new[] { "game_complete" }));
            Assert.That(s.PendingAchievementIds, Is.EqualTo(new[] { "collection_complete" }));

            Assert.That(s.StartNewGame(), Is.True);
            Assert.That(s.TotalGold, Is.EqualTo(0));
            Assert.That(s.CollectedTreasureCount, Is.EqualTo(0));
            Assert.That(s.IsGameCompleteReached, Is.False);
            Assert.That(s.PendingAchievementIds, Is.Empty);
            Assert.That(s.CelebratedAchievementIds, Is.Empty);
            GameProgressData fresh = Saved(store);
            Assert.That(fresh.pendingAchievementIds, Is.Empty);
            Assert.That(fresh.celebratedAchievementIds, Is.Empty);
            Assert.That(fresh.totalGold, Is.EqualTo(0));

            // and a new game can earn both again from the start
            s.RegisterCollected(TreasureCatalog.AllIds[0], 2499);
            s.RegisterCollected(TreasureCatalog.AllIds[1], 1);
            Assert.That(s.PendingAchievementIds, Is.EqualTo(new[] { "game_complete" }), "a new game celebrates the goal again");
        }
    }

    /// <summary>
    /// GAME COMPLETE and 100% COLLECTION (stage 17D) in the real maps: the treasures are collected through the real DIG and COLLECT buttons (real mouse taps through the Input System), the panel buttons are tapped
    /// for real too. Saves are in memory only (the developer's real save file is hashed before and after). Layout tests are at the end (they resize the Game View).
    /// </summary>
    public sealed class FinalCelebrationPlayModeTests
    {
        private const string GreenPath = "Assets/Scenes/GreenMine.unity";
        private const string RockyPath = "Assets/Scenes/RockyCanyon.unity";
        private const string AncientPath = "Assets/Scenes/AncientMine.unity";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string CatalogPath = "Assets/Data/Areas/AreaCatalog.asset";

        // treasures left for a test to collect (in this order), on open ground that PlaceAtDistance can stand next to
        private static readonly string[] GreenLeft = { "green_mine.meadow_01", "green_mine.camp_01", "green_mine.forest_01" };
        private static readonly string[] RockyLeft = { "rocky_canyon.outpost_01", "rocky_canyon.overlook_01", "rocky_canyon.riverbed_01" };
        private static readonly string[] AncientLeft = { "ancient_mine.gate_02", "ancient_mine.gate_01", "ancient_mine.gate_03" };

        private static readonly Dictionary<string, List<(string id, int gold)>> Tables = new();
        private static string realSavePath, realSaveHashBefore;

        private Scene scene;
        private float previousTimeScale;
        private Mouse mouse;
        private Touchscreen touchscreen;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode previousBehaviour;
        private InputSettings.BackgroundBehavior previousBackground;
        private MemoryProgressStore store;
        private TreasureHuntController hunt;
        private FinalCelebrationController final;
        private LevelCompleteController level;
        private readonly List<string> requestedScenes = new();
        private readonly List<string> openedOrder = new();
        private AreaCatalog catalog;
        private TestGameView gameView;
        private float screenSide, screenBottom;
        private int tapRetries;

        // ------------------------------------------------------------------ set up / tear down

        private static string HashOfRealSave()
        {
            if (!File.Exists(realSavePath))
            {
                return "(no file)";
            }

            using SHA256 sha = SHA256.Create();
            return System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(realSavePath)));
        }

        [OneTimeSetUp]
        public void HashTheRealSave()
        {
            realSavePath = Path.Combine(Application.persistentDataPath, "item_collector_progress.json");
            realSaveHashBefore = HashOfRealSave();
        }

        [OneTimeTearDown]
        public void RealSaveIsUntouched()
        {
            Assert.That(HashOfRealSave(), Is.EqualTo(realSaveHashBefore), "the developer's real save file must be byte-for-byte unchanged by these tests");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(0f, 0f, Screen.width, Screen.height);
            previousBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; // the UI input module drops pointer events while the Editor is unfocused
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouseFinal");
            touchscreen = InputSystem.AddDevice<Touchscreen>("IC3D_TestTouchscreenFinal");
            keyboard = InputSystem.AddDevice<Keyboard>("IC3D_TestKeyboardFinal");
            requestedScenes.Clear();
            openedOrder.Clear();
            SceneNavigator.LoadHook = name => requestedScenes.Add(name);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (tapRetries > 0)
            {
                Debug.Log($"[Final tests] {tapRetries} tap(s) had to be repeated (the Editor swallowed a synthetic click)");
                tapRetries = 0;
            }

            foreach (InputDevice device in new InputDevice[] { mouse, touchscreen, keyboard })
            {
                if (device != null)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            mouse = null;
            touchscreen = null;
            keyboard = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehaviour;
            InputSystem.settings.backgroundBehavior = previousBackground;
            SafeAreaFitter.SafeAreaOverride = null;
            SceneNavigator.LoadHook = null;
            SceneNavigator.ConsumeAreaSelectionRequest();
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            GamePause.Resume();
            GamePause.SetModalOpen(false);
            if (gameView != null)
            {
                yield return gameView.Restore();
                gameView = null;
                yield return RefocusSimulator(); // resizing the Game View leaves the docked Simulator unfocused: real taps in the next test would miss
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            scene = default;
            final = null;
            level = null;
            hunt = null;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
        }

        private static IEnumerator RefocusSimulator()
        {
            System.Type type = typeof(Editor).Assembly.GetType("UnityEditor.DeviceSimulation.SimulatorWindow");
            if (type != null)
            {
                foreach (Object window in Resources.FindObjectsOfTypeAll(type))
                {
                    var editorWindow = (EditorWindow)window;
                    type.GetMethod("ShowTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(window, null);
                    editorWindow.Focus();
                }
            }

            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------ helpers: saves and scenes

        private IEnumerator EnsureTable(string path)
        {
            if (Tables.ContainsKey(path))
            {
                yield break;
            }

            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            Scene temporary = SceneManager.GetSceneByPath(path);
            var list = new List<(string id, int gold)>();
            foreach (GameObject root in temporary.GetRootGameObjects())
            {
                foreach (TreasureSpot spot in root.GetComponentsInChildren<TreasureSpot>(true))
                {
                    list.Add((spot.SpotId, spot.Definition.GoldValue));
                }
            }

            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            Tables[path] = list;
            yield return SceneManager.UnloadSceneAsync(temporary);
        }

        private IEnumerator EnsureTables()
        {
            yield return EnsureTable(GreenPath);
            yield return EnsureTable(RockyPath);
            yield return EnsureTable(AncientPath);
        }

        private static IEnumerable<string> EveryId => Tables.Values.SelectMany(t => t.Select(x => x.id));

        private static int GoldOf(string id) => Tables.Values.SelectMany(t => t).First(x => x.id == id).gold;

        private static GameProgressData Saved(MemoryProgressStore s) => JsonUtility.FromJson<GameProgressData>(s.Json);

        /// <summary>Loads a map with every treasure of the three maps collected except <paramref name="left"/>.</summary>
        private IEnumerator StartMap(string path, string[] left, int totalGold, string[] pending = null, string[] celebrated = null, bool oldFormat = false)
        {
            yield return EnsureTables();
            IEnumerable<string> ids = EveryId.Where(id => !left.Contains(id));
            store = new MemoryProgressStore(FinalAchievementsServiceTests.Json(ids, totalGold, pending, celebrated, oldFormat));
            yield return LoadScene(path, store);
        }

        private IEnumerator LoadScene(string path, MemoryProgressStore withStore)
        {
            store = withStore;
            ProgressManager.StoreFactoryOverride = () => withStore;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            Physics.SyncTransforms();
            hunt = TryOne<TreasureHuntController>();
            final = TryOne<FinalCelebrationController>();
            level = TryOne<LevelCompleteController>();
            openedOrder.Clear();
            if (path != MainMenuPath)
            {
                Assert.That(final, Is.Not.Null, "every playable map has a FinalCelebration panel: " + path);
                final.Opened += id => openedOrder.Add("final:" + id);
            }

            if (level != null)
            {
                level.Opened += id => openedOrder.Add("level:" + id);
            }
        }

        private T TryOne<T>() where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private T One<T>() where T : Component
        {
            T found = TryOne<T>();
            Assert.That(found, Is.Not.Null, "Not found: " + typeof(T).Name);
            return found;
        }

        private Button FindButton(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button b in root.GetComponentsInChildren<Button>(true))
                {
                    if (b.name == name)
                    {
                        return b;
                    }
                }
            }

            Assert.Fail("Button not found: " + name);
            return null;
        }

        private Transform Player
        {
            get
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Player")
                    {
                        return root.transform;
                    }
                }

                Assert.Fail("Player not found");
                return null;
            }
        }

        private TreasureSpot Spot(string id)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TreasureSpot spot in root.GetComponentsInChildren<TreasureSpot>(true))
                {
                    if (spot.SpotId == id)
                    {
                        return spot;
                    }
                }
            }

            Assert.Fail("Treasure spot not found: " + id);
            return null;
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static Vector3 PlaceAtDistance(TreasureSpot spot, float distance)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                Vector3 p = spot.transform.position + new Vector3(Mathf.Cos(a) * distance, 0f, Mathf.Sin(a) * distance);
                if (Physics.Raycast(new Vector3(p.x, spot.transform.position.y + 3f, p.z), Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
                    && !Physics.CheckCapsule(hit.point + Vector3.up * 0.6f, hit.point + Vector3.up * 1.5f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    return hit.point + Vector3.up * 1.1f;
                }
            }

            Assert.Fail($"No free standing place {distance} m from {spot.SpotId}");
            return default;
        }

        private static Vector2 ScreenCentre(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        private IEnumerator TouchEvent(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = position, pressure = phase == UnityEngine.InputSystem.TouchPhase.Ended ? 0f : 1f });
            yield return null;
            yield return null;
        }

        /// <summary>A real mouse click on a screen position through the Input System (EventSystem, GraphicRaycaster, Button).</summary>
        private IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
        }

        /// <summary>A real tap, repeated (up to three times, after re-focusing the Simulator window) until its effect <paramref name="done"/> is seen: the Editor sometimes swallows a synthetic click.</summary>
        private IEnumerator ClickUntil(RectTransform target, System.Func<bool> done)
        {
            for (int attempt = 0; attempt < 3 && !done(); attempt++)
            {
                if (attempt > 0)
                {
                    tapRetries++;
                    yield return RefocusSimulator();
                    Canvas.ForceUpdateCanvases();
                }

                yield return Click(ScreenCentre(target));
                yield return WaitUntil(done, 1.5f);
            }
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        /// <summary>Like <see cref="WaitUntil"/>, but every frame it checks that never two celebration panels (and never two final windows) are on screen at the same time.</summary>
        private IEnumerator WaitUntilChecking(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                CheckOnePanel();
                yield return null;
            }

            CheckOnePanel();
        }

        private void CheckOnePanel()
        {
            bool finalOn = final != null && final.PanelRoot.activeInHierarchy;
            bool levelOn = level != null && level.PanelRoot.activeInHierarchy;
            Assert.That(finalOn && levelOn, Is.False, "the final panel and LEVEL COMPLETE are open at the same time");
            Assert.That(final != null && final.IsOpen && level != null && level.IsOpen, Is.False, "both controllers are Open at once");
            if (finalOn)
            {
                int windows = (final.Game.window.gameObject.activeSelf ? 1 : 0) + (final.Collection.window.gameObject.activeSelf ? 1 : 0);
                Assert.That(windows, Is.EqualTo(1), "exactly one final window is shown");
            }
        }

        private IEnumerator DragJoystick(float seconds)
        {
            Vector2 centre = ScreenCentre((RectTransform)One<VirtualJoystick>().transform);
            yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Began, centre);
            yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Moved, centre + new Vector2(0f, 90f));
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Moved, centre + new Vector2(0f, 90f));
            }

            yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Ended, centre + new Vector2(0f, 90f));
        }

        private IEnumerator DragCamera(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * 0.7f, Screen.height * 0.5f), delta = new Vector2(40f, 0f) }.WithButton(MouseButton.Right));
                yield return null;
            }

            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * 0.7f, Screen.height * 0.5f) });
            yield return null;
            yield return null;
        }

        private IEnumerator PressBack()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        /// <summary>Collects one treasure with real taps: stand next to it, tap DIG, wait for the reveal, tap COLLECT.</summary>
        private IEnumerator TapDigAndCollect(TreasureSpot spot)
        {
            Button dig = FindButton("DigButton");
            Button collect = FindButton("CollectButton");
            Teleport(Player, PlaceAtDistance(spot, 1.5f));
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.25f);
            Canvas.ForceUpdateCanvases();
            yield return ClickUntil((RectTransform)dig.transform, () => spot.State == TreasureState.Revealed); // real tap
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "the dig revealed " + spot.SpotId);
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.25f);
            Canvas.ForceUpdateCanvases();
            yield return ClickUntil((RectTransform)collect.transform, () => spot.State == TreasureState.Collected); // real tap
            yield return WaitUntil(() => spot.State == TreasureState.Collected, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), "the collect took " + spot.SpotId);
        }

        /// <summary>Collects a treasure and waits for the final panel (and its entrance animation).</summary>
        private IEnumerator CollectAndWaitForFinal(TreasureSpot spot)
        {
            yield return TapDigAndCollect(spot);
            yield return WaitUntilChecking(() => final.IsOpen, 15f);
            Assert.That(final.IsOpen, Is.True, "a final celebration opened after the COLLECT");
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Waits for the final panel to open on its own (a celebration that is owed when the scene is entered).</summary>
        private IEnumerator WaitForFinalToOpen()
        {
            yield return WaitUntilChecking(() => final.IsOpen, 12f);
            Assert.That(final.IsOpen, Is.True, "the owed celebration opened");
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);
            Canvas.ForceUpdateCanvases();
        }

        private static string Text(TMPro.TMP_Text label) => label != null ? label.text : string.Empty;

        // ================================================================ the data: catalog, scenes, economy

        [UnityTest, Order(1)]
        public IEnumerator TheTreasureCatalog_IsExactlyTheTreasureSpotsOfTheThreeScenes_AndTheEconomyIsStill_625_840_1240_2705()
        {
            yield return EnsureTables();
            var expected = new[] { (GreenPath, "green_mine", 15, 625), (RockyPath, "rocky_canyon", 20, 840), (AncientPath, "ancient_mine", 25, 1240) };
            int total = 0;
            foreach ((string path, string area, int count, int gold) in expected)
            {
                List<(string id, int gold)> table = Tables[path];
                Assert.That(table.Count, Is.EqualTo(count), area + " treasures");
                Assert.That(table.Select(t => t.id).Distinct().Count(), Is.EqualTo(count), area + ": unique ids");
                Assert.That(table.Select(t => t.id).OrderBy(x => x, System.StringComparer.Ordinal), Is.EqualTo(TreasureCatalog.IdsOf(area).OrderBy(x => x, System.StringComparer.Ordinal)),
                    area + ": the catalog lists exactly the ids of the scene");
                Assert.That(table.Sum(t => t.gold), Is.EqualTo(gold), area + " gold");
                total += table.Sum(t => t.gold);
            }

            Assert.That(total, Is.EqualTo(2705), "the three maps pay 2705 in all");
            Assert.That(EveryId.Count(), Is.EqualTo(60));
            Assert.That(catalog.Get("rocky_canyon").unlockTotalGold, Is.EqualTo(500));
            Assert.That(catalog.Get("ancient_mine").unlockTotalGold, Is.EqualTo(1200));
            Assert.That(catalog.Get("ancient_mine").goalTotalGold, Is.EqualTo(2500));
            Assert.That(625, Is.GreaterThanOrEqualTo(500), "Green Mine alone opens Rocky Canyon");
            Assert.That(625 + 840, Is.GreaterThanOrEqualTo(1200), "Green Mine and Rocky Canyon open Ancient Mine");
            Assert.That(total, Is.GreaterThan(FinalAchievements.Goal), "the goal 2500 is below the maximum: 2705");
        }

        // ================================================================ real collecting: the goal and the collection

        [UnityTest, Order(1)]
        public IEnumerator AncientMine_2499IsNothing_Crossing2500ByARealCollect_ShowsGameComplete_ContinueKeepsPlaying_ThenTheLastTreasureShowsTheCollection()
        {
            yield return EnsureTables();
            string first = AncientLeft[0], second = AncientLeft[1], third = AncientLeft[2];
            int v1 = GoldOf(first), v2 = GoldOf(second), v3 = GoldOf(third);
            CharacterSelection.Select(CharacterSelection.FemaleId);
            GameSettings.Current.SetMusicVolume(0.35f);
            GameSettings.Current.SetSfxVolume(0.6f);
            yield return StartMap(AncientPath, AncientLeft, 2499 - v1);
            Assert.That(final.CurrentPhase, Is.EqualTo(FinalCelebrationController.Phase.Idle));

            // 2499: the first collect ends exactly one short of the goal: nothing, however long we wait
            yield return TapDigAndCollect(Spot(first));
            Assert.That(hunt.TotalGold, Is.EqualTo(2499));
            yield return new WaitForSecondsRealtime(FinalCelebrationController.SettleSeconds + 2.6f);
            Assert.That(final.CurrentPhase, Is.EqualTo(FinalCelebrationController.Phase.Idle), "2499 gold is no GAME COMPLETE");
            Assert.That(final.ShowCount, Is.EqualTo(0));
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Saved(store).pendingAchievementIds, Is.Empty);
            Assert.That(Saved(store).celebratedAchievementIds, Is.Empty);

            // 2500+: the second collect crosses the goal. The panel waits for the collect animation, the reward text and the AREA COMPLETE banner
            TreasureSpot crossing = Spot(second);
            float collectedAt = -1f, openedAt = -1f;
            crossing.Collected += _ => collectedAt = Time.realtimeSinceStartup;
            final.Opened += _ => { if (openedAt < 0f) { openedAt = Time.realtimeSinceStartup; } };
            FloatingRewardText rewardText = One<FloatingRewardText>();
            ProgressNotificationPresenter banners = One<ProgressNotificationPresenter>();
            int rewardsBefore = rewardText.ShowCount;
            bool bannerSeen = false;
            yield return TapDigAndCollect(crossing);
            Assert.That(final.IsOpen, Is.False, "not open yet ...");
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "game_complete" }), "... but already owed in the save, written with the gold of that COLLECT");
            Assert.That(Saved(store).totalGold, Is.EqualTo(2499 + v2));
            while (!final.IsOpen && Time.realtimeSinceStartup < collectedAt + 15f)
            {
                bannerSeen |= banners.IsBusy;
                CheckOnePanel();
                yield return null;
            }

            Assert.That(final.IsOpen, Is.True, "GAME COMPLETE opens after crossing 2500");
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete));
            Assert.That(openedAt - collectedAt, Is.GreaterThanOrEqualTo(TreasureVisualAnimator.CollectSeconds + FinalCelebrationController.SettleSeconds - 0.2f), "it waited for the collect animation and the settle time");
            Assert.That(crossing.VisualAnimator.IsAnimating, Is.False, "the last COLLECT animation was not cut");
            Assert.That(rewardText.ShowCount, Is.EqualTo(rewardsBefore + 1), "the normal Gold notification was shown");
            Assert.That(rewardText.IsVisible, Is.False, "... and finished before the panel came");
            Assert.That(banners.IsBusy, Is.False, "the AREA COMPLETE banner was not cut either");
            Assert.That(bannerSeen, Is.True, "(the gold goal of Ancient Mine did raise its own AREA COMPLETE banner)");
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);

            int goldNow = v2 + 2499;
            Assert.That(final.Game.window.gameObject.activeSelf, Is.True);
            Assert.That(final.Collection.window.gameObject.activeSelf, Is.False, "only the GAME COMPLETE window");
            Assert.That(Text(final.Game.title), Is.EqualTo("GAME COMPLETE!"));
            Assert.That(Text(final.Game.subtitle), Is.EqualTo("YOU ARE A MASTER TREASURE HUNTER!"));
            Assert.That(Text(final.Game.goldLabel), Is.EqualTo($"TOTAL GOLD: {goldNow}"), "the player's real total gold");
            Assert.That(Text(final.Game.goalLabel), Is.EqualTo("2500 GOLD GOAL REACHED"));
            Assert.That(Text(final.Game.treasuresLabel), Is.EqualTo("59 / 60 TREASURES COLLECTED"), "GAME COMPLETE does not need 60 / 60");
            Assert.That(Text(final.Game.areasLabel), Is.EqualTo("ALL 3 AREAS UNLOCKED"));
            Assert.That(Text(final.Game.messageLabel), Does.Contain("STILL HIDDEN"));
            Assert.That(final.Game.picture.sprite, Is.Not.Null, "the Ancient Mine picture");
            Assert.That(final.Game.trophy.gameObject.activeInHierarchy, Is.True, "the trophy");
            Assert.That(final.Game.window.localScale.x, Is.EqualTo(1f).Within(0.02f), "the entrance animation has settled");
            Assert.That(Text(final.Game.continueButton.GetComponentInChildren<TMPro.TMP_Text>()), Is.EqualTo("CONTINUE EXPLORING"));
            Assert.That(Text(final.Game.areaSelectionButton.GetComponentInChildren<TMPro.TMP_Text>()), Is.EqualTo("AREA SELECTION"));
            Assert.That(Text(final.Game.mainMenuButton.GetComponentInChildren<TMPro.TMP_Text>()), Is.EqualTo("MAIN MENU"));
            Assert.That(GamePause.IsPaused && GamePause.ModalOpen, Is.True, "the game is paused behind it");
            Assert.That(FindButton("PauseButton").gameObject.activeSelf, Is.False, "no PAUSE button under the panel");
            Assert.That(final.JinglePlayCount, Is.EqualTo(1));
            Assert.That(One<AmbientMusicPlayer>().IsPlaying, Is.True, "the music keeps playing");
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "game_complete" }), "still owed until the player answers");
            Assert.That(Saved(store).celebratedAchievementIds, Is.Empty);

            // CONTINUE EXPLORING by a real tap: the panel closes, the game goes on, nothing is paid again
            GameProgressData beforeAnswer = Saved(store);
            yield return ClickUntil((RectTransform)final.Game.continueButton.transform, () => !final.IsOpen); // real tap
            Assert.That(final.IsOpen, Is.False, "CONTINUE EXPLORING closes the panel");
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(GamePause.ModalOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(requestedScenes, Is.Empty, "no scene change");
            Assert.That(FindButton("PauseButton").gameObject.activeSelf, Is.True, "the PAUSE button is back");
            GameProgressData answered = Saved(store);
            Assert.That(answered.totalGold, Is.EqualTo(beforeAnswer.totalGold), "answering pays nothing");
            Assert.That(answered.collectedTreasureIds, Is.EqualTo(beforeAnswer.collectedTreasureIds));
            Assert.That(answered.celebratedAchievementIds, Is.EqualTo(new[] { "game_complete" }));
            Assert.That(answered.pendingAchievementIds, Is.Empty);
            Assert.That(Saved(store).completedAreaIds, Is.EqualTo(beforeAnswer.completedAreaIds));

            // the game goes on: the third treasure is collected above 2500 (the save keeps every coin), and it completes the collection
            TreasureSpot lastOne = Spot(third);
            yield return CollectAndWaitForFinal(lastOne);
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection), "now the collection, not GAME COMPLETE again");
            Assert.That(final.ShownAchievements, Is.EqualTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(hunt.TotalGold, Is.EqualTo(2499 + v2 + v3), "gold keeps growing above 2500, nothing is capped");
            Assert.That(Saved(store).totalGold, Is.EqualTo(2499 + v2 + v3), "and the real save has it");
            Assert.That(Saved(store).collectedTreasureIds.Count, Is.EqualTo(60));
            Assert.That(Text(final.Collection.title), Is.EqualTo("100% COLLECTION COMPLETE!"));
            Assert.That(Text(final.Collection.subtitle), Is.EqualTo("YOU FOUND EVERY HIDDEN TREASURE!"));
            Assert.That(Text(final.Collection.treasuresLabel), Is.EqualTo("60 / 60"));
            Assert.That(Text(final.Collection.areaRows[0]), Does.Contain("GREEN MINE").And.Contain("15 / 15"));
            Assert.That(Text(final.Collection.areaRows[1]), Does.Contain("ROCKY CANYON").And.Contain("20 / 20"));
            Assert.That(Text(final.Collection.areaRows[2]), Does.Contain("ANCIENT MINE").And.Contain("25 / 25"));
            Assert.That(Text(final.Collection.goldLabel), Is.EqualTo($"TOTAL GOLD: {2499 + v2 + v3}"));
            yield return ClickUntil((RectTransform)final.Collection.continueButton.transform, () => !final.IsOpen); // real tap
            Assert.That(final.IsOpen, Is.False);
            Assert.That(GamePause.IsPaused, Is.False);
            GameProgressData end = Saved(store);
            Assert.That(end.celebratedAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(end.pendingAchievementIds, Is.Empty);
            Assert.That(end.totalGold, Is.EqualTo(2499 + v2 + v3), "the collection paid nothing of its own");
            Assert.That(final.ShowCount, Is.EqualTo(2));

            // miner and settings are what they were
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(openedOrder, Is.EqualTo(new[] { "final:game_complete", "final:collection_complete" }));
        }

        [UnityTest, Order(1)]
        public IEnumerator AncientMine_TheLastTreasureOfAll_OwesBothAtOnce_GameCompleteFirst_ThenTheCollection_NeverTwoPanels_AndTheGoldIsPaidOnce()
        {
            yield return EnsureTables();
            string left = AncientLeft[0];
            int v = GoldOf(left);
            yield return StartMap(AncientPath, new[] { left }, 2500 - v);
            Assert.That(Saved(store).collectedTreasureIds.Count, Is.EqualTo(59));

            yield return TapDigAndCollect(Spot(left));
            GameProgressData owed = Saved(store);
            Assert.That(owed.totalGold, Is.EqualTo(2500), "2500 exactly: the goal is reached on the dot");
            Assert.That(owed.pendingAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }), "both are owed, in the same save");
            Assert.That(owed.collectedTreasureIds.Count(id => id == left), Is.EqualTo(1));
            yield return WaitUntilChecking(() => final.IsOpen, 15f);
            Assert.That(final.IsOpen, Is.True);
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete), "GAME COMPLETE comes first");
            Assert.That(Text(final.Game.treasuresLabel), Is.EqualTo("60 / 60 TREASURES COLLECTED"));
            Assert.That(Text(final.Game.messageLabel), Does.Contain("EVERY TREASURE"));
            Assert.That(final.Collection.window.gameObject.activeSelf, Is.False, "the collection window is not shown yet");
            yield return new WaitForSecondsRealtime(1.0f);
            CheckOnePanel();
            Assert.That(final.ShowCount, Is.EqualTo(1), "one panel at a time");

            // the player closes the first: the second follows (the game stays paused: the panel only changes)
            yield return ClickUntil((RectTransform)final.Game.continueButton.transform, () => final.CurrentAchievement == FinalAchievements.Collection); // real tap
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection), "then 100% COLLECTION");
            Assert.That(final.IsOpen, Is.True);
            Assert.That(final.Game.window.gameObject.activeSelf, Is.False);
            Assert.That(final.Collection.window.gameObject.activeSelf, Is.True);
            Assert.That(GamePause.IsPaused && GamePause.ModalOpen, Is.True, "no gap in which the game runs");
            Assert.That(Saved(store).celebratedAchievementIds, Is.EqualTo(new[] { "game_complete" }));
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "collection_complete" }));
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);
            Assert.That(final.JinglePlayCount, Is.EqualTo(2), "each panel has its own sound");
            Assert.That(Text(final.Collection.treasuresLabel), Is.EqualTo("60 / 60"));
            Assert.That(Text(final.Collection.goldLabel), Is.EqualTo("TOTAL GOLD: 2500"));
            yield return ClickUntil((RectTransform)final.Collection.continueButton.transform, () => !final.IsOpen); // real tap
            Assert.That(final.IsOpen, Is.False);
            Assert.That(GamePause.IsPaused, Is.False);
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntrySeconds + 2.0f);
            Assert.That(final.ShowCount, Is.EqualTo(2), "nothing opens again");
            GameProgressData end = Saved(store);
            Assert.That(end.totalGold, Is.EqualTo(2500), "the gold was paid once and the celebrations paid nothing");
            Assert.That(end.collectedTreasureIds.Count, Is.EqualTo(60));
            Assert.That(end.celebratedAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(end.pendingAchievementIds, Is.Empty);
            Assert.That(One<ProgressManager>().Service.RegisterCollected(left, 10), Is.EqualTo(RegisterResult.AlreadyCollected));
            Assert.That(hunt.TotalGold, Is.EqualTo(2500));
        }

        [UnityTest, Order(1)]
        public IEnumerator GreenMine_TheLastTreasureOfEverything_ShowsGameComplete_ThenTheCollection_ThenLevelComplete_InThatOrder_NeverTwoAtOnce()
        {
            yield return EnsureTables();
            string left = GreenLeft[0];
            int v = GoldOf(left);
            CharacterSelection.Select(CharacterSelection.MaleId);
            yield return StartMap(GreenPath, new[] { left }, 2500 - v); // 59 / 60, Green Mine 14 / 15, this one crosses 2500 AND completes the map AND the collection
            Assert.That(level, Is.Not.Null, "Green Mine has its LEVEL COMPLETE panel too");

            yield return TapDigAndCollect(Spot(left));
            GameProgressData owed = Saved(store);
            Assert.That(owed.pendingAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(owed.pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }), "LEVEL COMPLETE is owed as well (17C)");
            yield return WaitUntilChecking(() => final.IsOpen || level.IsOpen, 15f);
            Assert.That(final.IsOpen, Is.True, "the final celebration is first");
            Assert.That(level.IsOpen, Is.False);
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete));
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);
            yield return new WaitForSecondsRealtime(1.5f);
            CheckOnePanel();
            Assert.That(level.IsOpen, Is.False, "LEVEL COMPLETE waits however long the final panel stays");
            Assert.That(level.ShowCount, Is.EqualTo(0));

            yield return ClickUntil((RectTransform)final.Game.continueButton.transform, () => final.CurrentAchievement == FinalAchievements.Collection); // real tap
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection));
            yield return new WaitForSecondsRealtime(1.2f);
            CheckOnePanel();
            Assert.That(level.IsOpen, Is.False, "still the collection panel");
            yield return ClickUntil((RectTransform)final.Collection.continueButton.transform, () => !final.IsOpen); // real tap
            Assert.That(final.IsOpen, Is.False);

            // LEVEL COMPLETE of the map follows and is not lost
            yield return WaitUntilChecking(() => level.IsOpen, 10f);
            Assert.That(level.IsOpen, Is.True, "17C LEVEL COMPLETE shows after the final panels");
            Assert.That(level.AreaString, Is.EqualTo("GREEN MINE COMPLETED"));
            Assert.That(level.CountString, Is.EqualTo("15 / 15 TREASURES COLLECTED"));
            Assert.That(level.TotalString, Is.EqualTo("YOUR TOTAL GOLD: 2500"));
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntranceSeconds + 0.15f);
            Assert.That(final.PanelRoot.activeSelf, Is.False);
            yield return ClickUntil((RectTransform)level.StayButton.transform, () => !level.IsOpen); // real tap
            Assert.That(level.IsOpen, Is.False);
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(openedOrder, Is.EqualTo(new[] { "final:game_complete", "final:collection_complete", "level:green_mine" }), "the order of the panels");
            GameProgressData end = Saved(store);
            Assert.That(end.totalGold, Is.EqualTo(2500));
            Assert.That(end.celebratedAreaIds, Is.EqualTo(new[] { "green_mine" }));
            Assert.That(end.pendingCelebrationAreaIds, Is.Empty);
            Assert.That(end.celebratedAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(end.pendingAchievementIds, Is.Empty);
        }

        [UnityTest, Order(1)]
        public IEnumerator RockyCanyon_TheGoalCrossedAwayFromAncientMine_ShowsGameComplete_AndLevelCompleteOfTheMapStaysOwed()
        {
            yield return EnsureTables();
            string left = RockyLeft[0];
            int v = GoldOf(left);
            // Rocky Canyon 19 / 20 with the goal crossed by this collect, and two more treasures elsewhere uncollected: GAME COMPLETE only (+ LEVEL COMPLETE is NOT due: the map is not complete)
            yield return StartMap(RockyPath, RockyLeft, 2500 - v);
            yield return CollectAndWaitForFinal(Spot(left));
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete), "the goal counts in whichever map it is crossed");
            Assert.That(Text(final.Game.treasuresLabel), Is.EqualTo("58 / 60 TREASURES COLLECTED"));
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "game_complete" }));
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.Empty, "Rocky Canyon is not complete (2 treasures left): no LEVEL COMPLETE");
            yield return ClickUntil((RectTransform)final.Game.continueButton.transform, () => !final.IsOpen); // real tap
            Assert.That(final.IsOpen, Is.False);
            Assert.That(level.ShowCount, Is.EqualTo(0));
            Assert.That(openedOrder, Is.EqualTo(new[] { "final:game_complete" }));
        }

        [UnityTest, Order(1)]
        public IEnumerator GameComplete_AreaSelection_ByARealTap_OpensTheAreaSelection_KeepsGoldAreasAndMiner_AndTheOtherCelebrationsStayOwed()
        {
            yield return EnsureTables();
            string left = GreenLeft[0];
            int v = GoldOf(left);
            CharacterSelection.Select(CharacterSelection.FemaleId);
            GameSettings.Current.SetMusicVolume(0.4f);
            yield return StartMap(GreenPath, new[] { left }, 2500 - v);
            yield return CollectAndWaitForFinal(Spot(left));
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete));
            GameProgressData before = Saved(store);

            yield return ClickUntil((RectTransform)final.Game.areaSelectionButton.transform, () => requestedScenes.Count > 0); // real tap
            Assert.That(requestedScenes, Is.EqualTo(new[] { SceneNavigator.MainMenuScene }), "the Main Menu through the SceneNavigator");
            Assert.That(SceneNavigator.AreaSelectionRequested, Is.True, "... with the Area Selection open");
            Assert.That(final.IsOpen, Is.False);
            Assert.That(GamePause.IsPaused, Is.False);
            GameProgressData after = Saved(store);
            Assert.That(after.totalGold, Is.EqualTo(before.totalGold));
            Assert.That(after.collectedTreasureIds, Is.EqualTo(before.collectedTreasureIds));
            Assert.That(after.unlockedAreaIds, Is.EqualTo(before.unlockedAreaIds));
            Assert.That(after.celebratedAchievementIds, Is.EqualTo(new[] { "game_complete" }), "GAME COMPLETE is answered");
            Assert.That(after.pendingAchievementIds, Is.EqualTo(new[] { "collection_complete" }), "the collection is still owed");
            Assert.That(after.pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }), "and so is LEVEL COMPLETE of Green Mine (17C): nothing is lost");

            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadScene(MainMenuPath, store);
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu.IsAreaSelectOpen, Is.True, "the existing Area Selection is open");
            AreaSelectController areas = Object.FindAnyObjectByType<AreaSelectController>();
            Assert.That(areas.GetState("green_mine").TotalGold, Is.EqualTo(2500), "the saved total gold");
            foreach (string id in new[] { "green_mine", "rocky_canyon", "ancient_mine" })
            {
                Assert.That(areas.GetState(id).Availability, Is.EqualTo(AreaAvailability.Playable), id + " is open");
            }

            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "the miner is kept");
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.4f).Within(0.001f), "the settings are kept");

            // the next time a map is entered: the collection, then LEVEL COMPLETE of Green Mine, one after the other
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return LoadScene(GreenPath, new MemoryProgressStore(store.Json));
            yield return WaitUntilChecking(() => final.IsOpen, 10f);
            Assert.That(final.IsOpen, Is.True);
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection));
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);
            yield return ClickUntil((RectTransform)final.Collection.continueButton.transform, () => !final.IsOpen); // real tap
            yield return WaitUntilChecking(() => level.IsOpen, 10f);
            Assert.That(level.IsOpen, Is.True, "LEVEL COMPLETE of the map follows");
            Assert.That(openedOrder, Is.EqualTo(new[] { "final:collection_complete", "level:green_mine" }));
            level.Stay();
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntrySeconds + 1.5f);
            Assert.That(final.ShowCount, Is.EqualTo(1));
        }

        [UnityTest, Order(1)]
        public IEnumerator GameComplete_MainMenu_ByARealTap_GoesToTheMainMenu_KeepsTheProgress_AndTheCollectionComesBackInTheNextMap()
        {
            yield return EnsureTables();
            string left = AncientLeft[0];
            int v = GoldOf(left);
            CharacterSelection.Select(CharacterSelection.MaleId);
            yield return StartMap(AncientPath, new[] { left }, 2500 - v);
            yield return CollectAndWaitForFinal(Spot(left));
            GameProgressData before = Saved(store);

            yield return ClickUntil((RectTransform)final.Game.mainMenuButton.transform, () => requestedScenes.Count > 0); // real tap
            Assert.That(requestedScenes, Is.EqualTo(new[] { SceneNavigator.MainMenuScene }));
            Assert.That(SceneNavigator.AreaSelectionRequested, Is.False, "MAIN MENU is the plain Main Menu, not the Area Selection");
            Assert.That(final.IsOpen, Is.False);
            Assert.That(GamePause.IsPaused, Is.False);
            GameProgressData after = Saved(store);
            Assert.That(after.totalGold, Is.EqualTo(before.totalGold));
            Assert.That(after.collectedTreasureIds, Is.EqualTo(before.collectedTreasureIds));
            Assert.That(after.celebratedAchievementIds, Is.EqualTo(new[] { "game_complete" }));
            Assert.That(after.pendingAchievementIds, Is.EqualTo(new[] { "collection_complete" }), "left through MAIN MENU: the collection stays owed");

            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadScene(MainMenuPath, store);
            yield return null;
            Assert.That(Object.FindAnyObjectByType<MainMenuController>().IsAreaSelectOpen, Is.False, "the ordinary Main Menu");
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.MaleId));
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;

            yield return LoadScene(AncientPath, new MemoryProgressStore(store.Json));
            yield return WaitForFinalToOpen();
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection), "the collection that was owed");
            Assert.That(final.ShowCount, Is.EqualTo(1));
            yield return ClickUntil((RectTransform)final.Collection.continueButton.transform, () => !final.IsOpen); // real tap
            Assert.That(Saved(store).pendingAchievementIds, Is.Empty);
            Assert.That(Saved(store).celebratedAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(Saved(store).totalGold, Is.EqualTo(2500), "no gold the second time");
        }

        [UnityTest, Order(1)]
        public IEnumerator TheAndroidBackKey_DoesContinueExploring_OnBothPanels_AndNeverOpensThePauseMenu()
        {
            yield return EnsureTables();
            string left = AncientLeft[0];
            yield return StartMap(AncientPath, new[] { left }, 2500 - GoldOf(left));
            yield return CollectAndWaitForFinal(Spot(left));
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete));

            yield return PressBack();
            Assert.That(final.IsOpen, Is.True, "Back on GAME COMPLETE leads on to the collection, like CONTINUE EXPLORING");
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection));
            Assert.That(GamePause.IsPaused, Is.True, "still our panel: not the Pause Menu");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return PressBack();
            Assert.That(final.IsOpen, Is.False, "Back closes the last panel");
            Assert.That(GamePause.IsPaused, Is.False, "and does not open the Pause Menu");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(requestedScenes, Is.Empty);
            Assert.That(FindButton("PauseButton").gameObject.activeSelf, Is.True);
            Assert.That(Saved(store).celebratedAchievementIds, Is.EquivalentTo(new[] { "game_complete", "collection_complete" }));
            Assert.That(Saved(store).pendingAchievementIds, Is.Empty);

            // with the panel closed Back is the Pause Menu's again
            yield return PressBack();
            Assert.That(GamePause.IsPaused, Is.True, "the Pause Menu answers Back again");
            One<PauseMenuController>().Resume();
        }

        // ================================================================ restarts, any map, old saves, NEW GAME

        [UnityTest, Order(1)]
        public IEnumerator ALeftOwedGoal_SurvivesTheScene_AndTheApp_ShowsInAnyMapOnce_AndTheAnswerEndsIt()
        {
            yield return EnsureTables();
            string left = RockyLeft[0];
            int v = GoldOf(left);
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return StartMap(RockyPath, RockyLeft, 2500 - v);
            yield return TapDigAndCollect(Spot(left)); // the goal is crossed in Rocky Canyon ...
            Assert.That(final.IsOpen, Is.False, "... and the player leaves while the panel still waits for the reward");
            Assert.That(final.CurrentPhase, Is.EqualTo(FinalCelebrationController.Phase.Waiting));
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "game_complete" }), "owed in the save already");
            int gold = Saved(store).totalGold;
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;

            // a restarted app (a fresh store over the same text) in ANOTHER map: it is shown once, after the map has settled
            yield return LoadScene(GreenPath, new MemoryProgressStore(store.Json));
            float enteredAt = Time.realtimeSinceStartup;
            float openedAt = -1f;
            final.Opened += _ => openedAt = Time.realtimeSinceStartup;
            Assert.That(final.IsOpen, Is.False, "not at once: the player sees the place first");
            yield return WaitUntilChecking(() => final.IsOpen, 10f);
            Assert.That(final.IsOpen, Is.True, "the owed celebration is shown in the next map");
            Assert.That(openedAt - enteredAt, Is.GreaterThanOrEqualTo(FinalCelebrationController.EntrySeconds - 0.15f));
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete));
            Assert.That(Text(final.Game.goldLabel), Is.EqualTo($"TOTAL GOLD: {gold}"));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(final.ShowCount, Is.EqualTo(1), "it does not trigger twice");
            Assert.That(hunt.TotalGold, Is.EqualTo(gold), "no gold was paid again");
            Assert.That(Saved(store).pendingAchievementIds, Is.EqualTo(new[] { "game_complete" }), "still owed: nobody answered it");
            Assert.That(Saved(store).celebratedAchievementIds, Is.Empty);

            // the app is closed with the panel open: it is owed still and comes back (in Ancient Mine this time)
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            GamePause.Resume();
            GamePause.SetModalOpen(false);
            yield return LoadScene(AncientPath, new MemoryProgressStore(store.Json));
            yield return WaitForFinalToOpen();
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete), "no choice was made, so it comes back once more");

            // the answer (a real tap): from now on it is over, in every map
            yield return ClickUntil((RectTransform)final.Game.continueButton.transform, () => !final.IsOpen); // real tap
            Assert.That(Saved(store).celebratedAchievementIds, Is.EqualTo(new[] { "game_complete" }));
            Assert.That(Saved(store).pendingAchievementIds, Is.Empty);
            Assert.That(Saved(store).totalGold, Is.EqualTo(gold));
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            foreach (string path in new[] { AncientPath, RockyPath })
            {
                yield return LoadScene(path, new MemoryProgressStore(store.Json));
                yield return new WaitForSecondsRealtime(FinalCelebrationController.EntrySeconds + FinalCelebrationController.SettleSeconds + 1.2f);
                Assert.That(final.ShowCount, Is.EqualTo(0), "an answered celebration never comes back: " + path);
                Assert.That(GamePause.IsPaused, Is.False);
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
            }

            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
        }

        [UnityTest, Order(1)]
        public IEnumerator OldSaves_PastTheGoalOrComplete_NeverCelebrateByLoading_NorByCollectingMore_AndAreNeverRewritten()
        {
            yield return EnsureTables();
            // (1) an old finished game: 60 / 60, 2705 gold, no celebration fields at all
            string old = FinalAchievementsServiceTests.Json(EveryId, 2705, oldFormat: true);
            var oldStore = new MemoryProgressStore(old);
            foreach (string path in new[] { AncientPath, GreenPath })
            {
                yield return LoadScene(path, oldStore);
                yield return new WaitForSecondsRealtime(FinalCelebrationController.EntrySeconds + FinalCelebrationController.SettleSeconds + 1.5f);
                Assert.That(final.ShowCount, Is.EqualTo(0), "an old finished save does not celebrate by itself: " + path);
                Assert.That(final.CurrentPhase, Is.EqualTo(FinalCelebrationController.Phase.Idle));
                Assert.That(GamePause.IsPaused, Is.False);
                Assert.That(oldStore.Json, Is.EqualTo(old), "the old save is not touched");
                Assert.That(oldStore.WriteCount, Is.EqualTo(0));
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
            }

            // (2) an old save past the gold goal, one treasure short: the last treasure owes ONLY the collection
            string left = AncientLeft[0];
            int v = GoldOf(left);
            yield return StartMap(AncientPath, new[] { left }, 2705 - v, oldFormat: true);
            yield return CollectAndWaitForFinal(Spot(left));
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection), "the old goal crossing is not celebrated, the collection is");
            Assert.That(openedOrder, Is.EqualTo(new[] { "final:collection_complete" }));
            yield return ClickUntil((RectTransform)final.Collection.continueButton.transform, () => !final.IsOpen); // real tap
            yield return new WaitForSecondsRealtime(FinalCelebrationController.SettleSeconds + 2.0f);
            Assert.That(final.ShowCount, Is.EqualTo(1), "and GAME COMPLETE never comes for an old crossing");
            Assert.That(Saved(store).celebratedAchievementIds, Is.EqualTo(new[] { "collection_complete" }));
            Assert.That(Saved(store).totalGold, Is.EqualTo(2705));
        }

        [UnityTest, Order(1)]
        public IEnumerator NewGame_WhileACelebrationWaits_ClearsIt_ItNeverOpens_AndTheSaveIsFresh()
        {
            yield return EnsureTables();
            yield return StartMap(AncientPath, new string[0], 2705, pending: new[] { "game_complete", "collection_complete" });
            Assert.That(final.CurrentPhase, Is.EqualTo(FinalCelebrationController.Phase.Waiting), "owed from the save: it waits for the scene to settle");
            Assert.That(final.IsOpen, Is.False);
            Assert.That(One<ProgressManager>().Service.StartNewGame(), Is.True);
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntrySeconds + FinalCelebrationController.SettleSeconds + 2.5f);
            Assert.That(final.ShowCount, Is.EqualTo(0), "NEW GAME cleared the owed celebrations: nothing opens");
            Assert.That(final.CurrentPhase, Is.EqualTo(FinalCelebrationController.Phase.Idle));
            Assert.That(GamePause.IsPaused, Is.False);
            GameProgressData fresh = Saved(store);
            Assert.That(fresh.pendingAchievementIds, Is.Empty);
            Assert.That(fresh.celebratedAchievementIds, Is.Empty);
            Assert.That(fresh.totalGold, Is.EqualTo(0));
            Assert.That(fresh.collectedTreasureIds, Is.Empty);
        }

        [UnityTest, Order(1)]
        public IEnumerator AnOwedCelebration_WaitsWhilePaused_AndOpensAfterTheResume_NeverOverThePauseMenu()
        {
            yield return EnsureTables();
            yield return StartMap(AncientPath, new string[0], 2705, pending: new[] { "game_complete" });
            GamePause.Pause(); // the player paused the game during the entry delay
            yield return new WaitForSecondsRealtime(FinalCelebrationController.EntrySeconds + 1.5f);
            Assert.That(final.IsOpen, Is.False, "it does not open over a paused game");
            Assert.That(final.ShowCount, Is.EqualTo(0));
            GamePause.Resume();
            yield return WaitForFinalToOpen();
            Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete));
            Assert.That(GamePause.IsPaused, Is.True);
        }

        // ================================================================ touches (real hit tests)

        [UnityTest, Order(1)]
        public IEnumerator TheOpenPanels_TakeEveryTouch_TheJoystickTheCameraAndTheButtonsBeneathGetNothing_AndEveryButtonIsHitByItsOwnCentre()
        {
            yield return EnsureTables();
            yield return StartMap(AncientPath, new string[0], 2705, pending: new[] { "game_complete", "collection_complete" });
            yield return WaitForFinalToOpen();
            for (int panelIndex = 0; panelIndex < 2; panelIndex++)
            {
                FinalCelebrationController.Skin skin = final.ActiveSkin;
                Assert.That(skin, Is.Not.Null);
                var points = new List<(string name, Vector2 point)>
                {
                    ("joystick", ScreenCentre((RectTransform)One<VirtualJoystick>().transform)),
                    ("PAUSE button", ScreenCentre((RectTransform)FindButton("PauseButton").transform)),
                    ("DIG button", ScreenCentre((RectTransform)FindButton("DigButton").transform)),
                    ("COLLECT button", ScreenCentre((RectTransform)FindButton("CollectButton").transform)),
                    ("camera area (right)", new Vector2(Screen.width * 0.9f, Screen.height * 0.5f)),
                    ("camera area (centre)", new Vector2(Screen.width * 0.5f, Screen.height * 0.08f)),
                    ("bottom left corner", new Vector2(8f, 8f)),
                    ("top right corner", new Vector2(Screen.width - 8f, Screen.height - 8f)),
                };
                foreach ((string name, Vector2 point) in points)
                {
                    var results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
                    Assert.That(results, Is.Not.Empty, name);
                    Assert.That(results[0].gameObject.transform.IsChildOf(final.PanelRoot.transform), Is.True, $"panel {panelIndex}, {name}: the first thing a touch hits is the panel, not '{results[0].gameObject.name}'");
                }

                foreach ((string name, Button button) in new[] { ("CONTINUE EXPLORING", skin.continueButton), ("AREA SELECTION", skin.areaSelectionButton), ("MAIN MENU", skin.mainMenuButton) })
                {
                    var rect = (RectTransform)button.transform;
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    foreach (Vector2 point in new[] { ScreenCentre(rect), Vector2.Lerp(corners[0], corners[2], 0.15f), Vector2.Lerp(corners[0], corners[2], 0.85f) })
                    {
                        var results = new List<RaycastResult>();
                        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
                        Assert.That(results, Is.Not.Empty, name);
                        Assert.That(results[0].gameObject.transform.IsChildOf(button.transform), Is.True, $"panel {panelIndex}: a tap on {name} reaches {name}, not '{results[0].gameObject.name}'");
                    }
                }

                if (panelIndex == 0)
                {
                    // a real finger on the joystick and a real camera drag do nothing; taps on the DIG / PAUSE places do nothing
                    Vector3 position = Player.position;
                    float yaw = One<ThirdPersonCameraController>().Yaw;
                    yield return DragJoystick(0.8f);
                    yield return DragCamera(30);
                    Assert.That(Vector3.Distance(Player.position, position), Is.LessThan(0.05f), "the joystick is dead behind the panel");
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, One<ThirdPersonCameraController>().Yaw)), Is.LessThan(0.5f), "the camera does not orbit behind the panel");
                    Assert.That(One<VirtualJoystick>().Value, Is.EqualTo(Vector2.zero));
                    yield return Click(points[1].point);
                    yield return Click(points[2].point);
                    Assert.That(final.IsOpen, Is.True);
                    Assert.That(hunt.IsDigging, Is.False);
                    Assert.That(requestedScenes, Is.Empty);
                    final.ContinueExploring(); // on to the collection panel
                    yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection));
                }
            }
        }

        // ================================================================ sound

        [UnityTest, Order(1)]
        public IEnumerator WithSfxOff_ThePanelIsSilent_AndTheMusicKeepsPlaying_WithSfxOn_ItPlaysItsOwnSound()
        {
            yield return EnsureTables();
            GameSettings.Current.SetSfxVolume(0f);
            yield return StartMap(AncientPath, new string[0], 2705, pending: new[] { "game_complete" });
            AmbientMusicPlayer music = One<AmbientMusicPlayer>();
            bool musicBefore = music.IsPlaying;
            yield return WaitForFinalToOpen();
            Assert.That(final.JinglePlayCount, Is.EqualTo(0), "SFX volume 0: no celebration sound");
            Assert.That(final.LastJingleVolume, Is.EqualTo(0f));
            Assert.That(music.IsPlaying, Is.EqualTo(musicBefore), "the music is untouched");
            Assert.That(final.GetComponent<AudioSource>(), Is.Null, "no audio source is even created");
            final.ContinueExploring();
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;

            GameSettings.Current.SetSfxVolume(0.8f);
            yield return StartMap(AncientPath, new string[0], 2705, pending: new[] { "game_complete" });
            yield return WaitForFinalToOpen();
            Assert.That(final.JinglePlayCount, Is.EqualTo(1));
            Assert.That(final.LastJingleVolume, Is.EqualTo(FinalCelebrationController.JingleVolume * 0.8f).Within(0.001f));
            Assert.That(final.GetComponent<AudioSource>().ignoreListenerPause, Is.True, "plays although the game is paused");
        }

        [Test, Order(1)]
        public void TheFanfare_IsShortQuietAndSilentAtBothEnds()
        {
            AudioClip clip = SoundSynth.GameComplete();
            try
            {
                Assert.That(clip.length, Is.InRange(2.5f, 4.5f), "a short fanfare");
                var samples = new float[clip.samples];
                clip.GetData(samples, 0);
                Assert.That(samples.Max(s => Mathf.Abs(s)), Is.InRange(0.3f, 0.75f), "quiet enough: below the peak of the other effects");
                Assert.That(Mathf.Abs(samples[0]), Is.LessThan(0.01f), "no click at the start");
                Assert.That(Mathf.Abs(samples[^1]), Is.LessThan(0.001f), "no click at the end");
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        // ================================================================ layout on every target screen (run these last: they resize the Game View)

        private static readonly (string name, int width, int height, float side, float bottom)[] Screens =
        {
            ("iPhone 15 Pro Max", 2796, 1290, 59f * 3f, 21f * 3f),
            ("iPhone 15 Pro", 2556, 1179, 59f * 3f, 21f * 3f),
            ("iPhone SE 16:9", 1334, 750, 0f, 0f),
            ("Android 16:9", 1920, 1080, 0f, 0f),
            ("Android 19.5:9", 2340, 1080, 0f, 0f),
            ("Android 20:9", 2400, 1080, 0f, 0f),
            ("Android 21:9", 2520, 1080, 0f, 0f),
            ("2.6:1", 2808, 1080, 0f, 0f),
            ("4:3 tablet", 1600, 1200, 0f, 0f),
        };

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Contains(Rect outer, Rect inner, float slack = 1f)
        {
            return inner.xMin >= outer.xMin - slack && inner.xMax <= outer.xMax + slack && inner.yMin >= outer.yMin - slack && inner.yMax <= outer.yMax + slack;
        }

        private void AssertLayout(string name, string panelName, FinalCelebrationController.Skin skin)
        {
            SafeAreaFitter fitter = final.GetComponentInParent<SafeAreaFitter>();
            Rect allowed = SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), fitter.GestureMarginFraction * Screen.height);
            Rect window = ScreenRect(skin.window);
            Rect banner = ScreenRect((RectTransform)skin.window.Find("TitleBanner"));
            Rect whole = Rect.MinMaxRect(window.xMin, window.yMin, window.xMax, Mathf.Max(window.yMax, banner.yMax));
            Assert.That(Contains(allowed, whole), Is.True, $"{name} / {panelName}: the panel ({whole}) stays inside the safe area and the gesture margin ({allowed})");

            float reference = Mathf.Min(Screen.height, Screen.width * 9f / 16f); // the height of the 1080-unit reference layout in pixels
            float unit = reference / 1080f;
            var buttons = new List<(string label, Rect rect, float minHeight, float minWidth)>
            {
                ("CONTINUE EXPLORING", ScreenRect((RectTransform)skin.continueButton.transform), 140f, 800f),
                ("AREA SELECTION", ScreenRect((RectTransform)skin.areaSelectionButton.transform), 126f, 380f),
                ("MAIN MENU", ScreenRect((RectTransform)skin.mainMenuButton.transform), 126f, 380f),
            };
            foreach ((string label, Rect rect, float minHeight, float minWidth) in buttons)
            {
                Assert.That(Contains(window, rect), Is.True, $"{name} / {panelName}: {label} sits inside the window");
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(minHeight * unit * 0.95f), $"{name} / {panelName}: {label} is a comfortable touch target tall");
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(minWidth * unit), $"{name} / {panelName}: {label} is wide enough to hit");
            }

            Assert.That(buttons[0].rect.Overlaps(buttons[1].rect) || buttons[0].rect.Overlaps(buttons[2].rect) || buttons[1].rect.Overlaps(buttons[2].rect), Is.False, $"{name} / {panelName}: the three buttons do not overlap");
            Assert.That(Mathf.Abs(buttons[0].rect.center.x - window.center.x), Is.LessThan(2f), $"{name} / {panelName}: CONTINUE EXPLORING is centred");

            Rect picture = skin.picture != null ? ScreenRect((RectTransform)skin.picture.transform) : Rect.zero;
            foreach (TMPro.TMP_Text label in skin.window.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                label.ForceMeshUpdate();
                RectTransform rect = label.rectTransform;
                bool isTitle = label.name == "Heading"; // the title sits on the banner, which overhangs the window's top edge
                Rect labelRect = ScreenRect(rect);
                Assert.That(Contains(isTitle ? banner : window, labelRect), Is.True, $"{name} / {panelName}: '{label.text}' is inside its {(isTitle ? "banner" : "window")}");
                Assert.That(label.textBounds.size.x, Is.LessThanOrEqualTo(rect.rect.width + 1f), $"{name} / {panelName}: '{label.text}' does not overflow its box sideways");
                Assert.That(label.textBounds.size.y, Is.LessThanOrEqualTo(rect.rect.height + 1f), $"{name} / {panelName}: '{label.text}' does not overflow its box vertically");
                if (label.GetComponentInParent<Button>() == null)
                {
                    foreach ((string buttonLabel, Rect buttonRect, float _, float _) in buttons)
                    {
                        Assert.That(labelRect.Overlaps(buttonRect), Is.False, $"{name} / {panelName}: '{label.text}' does not run under {buttonLabel}");
                    }

                    if (skin.picture != null && !isTitle)
                    {
                        Assert.That(labelRect.Overlaps(picture), Is.False, $"{name} / {panelName}: '{label.text}' does not run over the picture");
                    }
                }
            }

            Assert.That(Text(skin.title).Length, Is.GreaterThan(5));
            Assert.That(ScreenRect(skin.trophy).center.y, Is.GreaterThan(window.yMin), $"{name} / {panelName}: the trophy / badge is inside the window");
        }

        private IEnumerator LayoutOnEveryScreen()
        {
            yield return EnsureTables();
            yield return StartMap(AncientPath, new string[0], 2705, pending: new[] { "game_complete", "collection_complete" });
            yield return WaitForFinalToOpen();
            gameView = new TestGameView();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(screenSide, screenBottom, Screen.width - 2f * screenSide, Screen.height - screenBottom);
            for (int panelIndex = 0; panelIndex < 2; panelIndex++)
            {
                string panelName = final.CurrentAchievement;
                foreach ((string name, int width, int height, float side, float bottom) in Screens)
                {
                    screenSide = side;
                    screenBottom = bottom;
                    gameView.SetResolution(width, height);
                    yield return WaitUntil(() => Screen.width == width && Screen.height == height, 3f);
                    Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(width, height)), name + ": the Game View has the screen size");
                    yield return null;
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertLayout(name, panelName, final.ActiveSkin);
                }

                if (panelIndex == 0)
                {
                    final.ContinueExploring();
                    yield return new WaitForSecondsRealtime(FinalCelebrationController.EntranceSeconds + 0.15f);
                    Assert.That(final.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection));
                }
            }
        }

        [UnityTest, Order(2)]
        public IEnumerator Layout_BothFinalPanels_FitEveryTargetScreen_SafeAreaButtonsAndTexts()
        {
            yield return LayoutOnEveryScreen();
        }
    }
}
#endif
