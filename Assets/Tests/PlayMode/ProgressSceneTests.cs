#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>Progress integrated with the real GameplayTest scene, controller and HUD (in-memory store, never the disk).</summary>
    public sealed class ProgressSceneTests
    {
        private const string ScenePath = "Assets/Scenes/GameplayTest.unity";
        private const string CoinId = "green_mine.gold_coin_01";
        private Scene loadedScene;
        private readonly List<GameObject> spawned = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            spawned.Clear();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore(); // back to the fixture default
        }

        private IEnumerator LoadScene(IProgressStore store)
        {
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null);
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
            yield return null;
        }

        private IEnumerator UnloadScene()
        {
            yield return SceneManager.UnloadSceneAsync(loadedScene);
            loadedScene = default;
            yield return null;
        }

        private Button FindButton(string name)
        {
            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button.name == name && button.gameObject.scene == loadedScene)
                {
                    return button;
                }
            }

            Assert.Fail("Button not found: " + name);
            return null;
        }

        private TreasureSpot Spawn(string prefabName, Vector3 position, string id = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Treasure/" + prefabName + ".prefab");
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(instance, loadedScene);
            spawned.Add(instance);
            TreasureSpot spot = instance.GetComponent<TreasureSpot>();
            if (id != null)
            {
                spot.SetSpotId(id);
            }

            return spot;
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForRevealed(TreasureSpot spot, float timeout = 3f)
        {
            float end = Time.time + timeout;
            while (spot.State == TreasureState.Hidden && Time.time < end)
            {
                yield return null;
            }

            yield return null;
        }

        /// <summary>Walks to the spot, digs it and collects it through the real HUD buttons.</summary>
        private IEnumerator DigAndCollect(TreasureSpot spot)
        {
            Transform player = GameObject.Find("Player").transform;
            Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -1.5f));
            yield return null;
            yield return null;
            FindButton("DigButton").onClick.Invoke();
            yield return WaitForRevealed(spot);
            yield return WaitSeconds(0.1f);
            FindButton("CollectButton").onClick.Invoke();
            yield return null;
        }

        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Scene_CollectingATreasure_SavesGoldAndId_AndHudShowsAreaGoal()
        {
            var store = new MemoryProgressStore();
            yield return LoadScene(store);
            ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            TreasureSpot coin = Object.FindAnyObjectByType<TreasureSpot>();
            TMP_Text goldText = GameObject.Find("GoldText").GetComponent<TMP_Text>();
            TMP_Text title = GameObject.Find("LevelTitle").GetComponent<TMP_Text>();

            Assert.That(manager, Is.Not.Null, "GameplayTest must contain a ProgressManager.");
            Assert.That(manager.IsReady, Is.True);
            Assert.That(manager.Service.LoadStatus, Is.EqualTo(ProgressLoadStatus.NewGame));
            Assert.That(goldText.text, Is.EqualTo("0 / 500 GOLD"), "Green Mine goal is 500.");
            Assert.That(title.text, Is.EqualTo("GREEN MINE"));
            Assert.That(coin.SpotId, Is.EqualTo(CoinId));
            Assert.That(coin.HasExplicitId, Is.True);

            yield return DigAndCollect(coin);

            Assert.That(coin.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(10));
            Assert.That(manager.Service.TotalGold, Is.EqualTo(10), "The controller reads the SAME gold: no second gold system.");
            Assert.That(manager.Service.IsCollected(CoinId), Is.True);
            Assert.That(goldText.text, Is.EqualTo("10 / 500 GOLD"));
            Assert.That(store.WriteCount, Is.EqualTo(1));
            StringAssert.Contains(CoinId, store.Json);
            StringAssert.Contains("\"totalGold\":10", store.Json);
        }

        [UnityTest]
        public IEnumerator Scene_Reload_DoesNotPayCollectedTreasureAgain_AndGoldKeepsAccumulating()
        {
            var store = new MemoryProgressStore();
            yield return LoadScene(store);
            TreasureSpot coin = Object.FindAnyObjectByType<TreasureSpot>();
            yield return DigAndCollect(coin);
            Assert.That(Object.FindAnyObjectByType<TreasureHuntController>().TotalGold, Is.EqualTo(10));

            // --- "close and reopen the game": unload the scene and load it again with the same saved data
            yield return UnloadScene();
            yield return LoadScene(store);

            ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            coin = Object.FindAnyObjectByType<TreasureSpot>();
            TMP_Text goldText = GameObject.Find("GoldText").GetComponent<TMP_Text>();
            Button digButton = FindButton("DigButton");
            int collectedEvents = 0;
            hunt.TreasureCollected += (_, _) => collectedEvents++;

            Assert.That(manager.Service.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(manager.Service.TotalGold, Is.EqualTo(10), "Total gold was loaded.");
            Assert.That(goldText.text, Is.EqualTo("10 / 500 GOLD"));
            Assert.That(coin.State, Is.EqualTo(TreasureState.Collected), "The collected coin does not come back.");
            Assert.That(coin.transform.Find("Visual").gameObject.activeSelf, Is.False);

            Transform player = GameObject.Find("Player").transform;
            Teleport(player, coin.transform.position + new Vector3(0f, 1.1f, -1.5f));
            yield return WaitSeconds(0.3f);
            Assert.That(digButton.gameObject.activeSelf, Is.False, "Nothing to dig: the coin was already collected.");
            digButton.onClick.Invoke();
            yield return null;
            Assert.That(hunt.IsDigging, Is.False);
            Assert.That(hunt.TotalGold, Is.EqualTo(10), "No second payment for the same treasure.");
            Assert.That(collectedEvents, Is.EqualTo(0));

            // gold is cumulative: a new treasure adds on top of the loaded total
            TreasureSpot nugget = Spawn("Treasure_GoldNugget", new Vector3(-30f, 0f, 10f));
            yield return null;
            yield return DigAndCollect(nugget);
            Assert.That(hunt.TotalGold, Is.EqualTo(35));
            Assert.That(goldText.text, Is.EqualTo("35 / 500 GOLD"));
            Assert.That(coin.State, Is.EqualTo(TreasureState.Collected));
        }

        [UnityTest]
        public IEnumerator Scene_AreaEvents_FireOncePerThreshold_AndNeverAgain()
        {
            var store = new MemoryProgressStore();
            yield return LoadScene(store);
            ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
            var log = new List<string>();
            manager.AreaCompleted += id => log.Add(ProgressService.AreaCompleteMessage + ":" + id);
            manager.AreaUnlocked += id => log.Add(ProgressService.NewAreaUnlockedMessage + ":" + id);

            yield return DigAndCollect(Object.FindAnyObjectByType<TreasureSpot>()); // 10
            Assert.That(log, Is.Empty);

            manager.Service.RegisterCollected("test.bulk_a", 489);
            Assert.That(manager.Service.TotalGold, Is.EqualTo(499));
            Assert.That(log, Is.Empty);

            manager.Service.RegisterCollected("test.bulk_b", 1); // 500
            Assert.That(log, Is.EqualTo(new[] { "AREA COMPLETE:green_mine", "NEW AREA UNLOCKED:rocky_canyon" }));

            manager.Service.RegisterCollected("test.bulk_c", 100);
            manager.Service.RegisterCollected("test.bulk_b", 100); // duplicate id: ignored
            Assert.That(log.Count, Is.EqualTo(2), "The same notification is never repeated.");
            Assert.That(manager.Service.TotalGold, Is.EqualTo(600));
        }

        [UnityTest]
        public IEnumerator Scene_LockedArea_CannotBePlayed_UntilTheUnlockGoldIsReached()
        {
            yield return LoadScene(new MemoryProgressStore());
            ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            TreasureSpot coin = Object.FindAnyObjectByType<TreasureSpot>();
            TMP_Text goldText = GameObject.Find("GoldText").GetComponent<TMP_Text>();
            TMP_Text title = GameObject.Find("LevelTitle").GetComponent<TMP_Text>();
            Button digButton = FindButton("DigButton");
            Transform player = GameObject.Find("Player").transform;

            hunt.SetAreaId("rocky_canyon"); // pretend this scene is Rocky Canyon
            Assert.That(hunt.IsAreaAccessible, Is.False, "Rocky Canyon needs 500 total gold.");
            Assert.That(goldText.text, Is.EqualTo("0 / 1200 GOLD"), "Rocky Canyon goal is 1200 (cumulative).");
            Assert.That(title.text, Is.EqualTo("ROCKY CANYON"));

            Teleport(player, coin.transform.position + new Vector3(0f, 1.1f, -1.5f));
            yield return WaitSeconds(0.3f);
            Assert.That(digButton.gameObject.activeSelf, Is.False, "DIG is not offered in a locked area.");
            digButton.onClick.Invoke();
            yield return null;
            Assert.That(hunt.IsDigging, Is.False);
            Assert.That(coin.State, Is.EqualTo(TreasureState.Hidden));

            // 500 total gold unlocks it
            manager.Service.RegisterCollected("test.unlock", 500);
            Assert.That(manager.Service.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(hunt.IsAreaAccessible, Is.True);
            yield return WaitSeconds(0.3f);
            Assert.That(digButton.gameObject.activeSelf, Is.True, "DIG appears once the area is unlocked.");

            yield return DigAndCollect(coin);
            Assert.That(hunt.TotalGold, Is.EqualTo(510), "Gold carried over: 500 + 10, nothing reset.");
            Assert.That(goldText.text, Is.EqualTo("510 / 1200 GOLD"));
        }

        [UnityTest]
        public IEnumerator Scene_TreasureIds_AreUnique_AndDuplicatesAreDetected()
        {
            yield return LoadScene(new MemoryProgressStore());

            TreasureIdReport sceneReport = TreasureIdValidator.Validate(TreasureSpot.Active);
            Assert.That(sceneReport.IsValid, Is.True, "GameplayTest ids must be unique.");
            Assert.That(sceneReport.SpotCount, Is.EqualTo(1));
            Assert.That(sceneReport.FallbackIdCount, Is.EqualTo(0), "The scene coin has an explicit id.");

            Spawn("Treasure_GoldNugget", new Vector3(-30f, 0f, 10f));
            Spawn("Treasure_GoldBar", new Vector3(-30f, 0f, 40f));
            Spawn("Treasure_AncientArtifact", new Vector3(30f, 0f, 10f));
            yield return null;
            TreasureIdReport four = TreasureIdValidator.Validate(TreasureSpot.Active);
            Assert.That(four.SpotCount, Is.EqualTo(4));
            Assert.That(four.IsValid, Is.True, "Four treasure types at four places: four different ids.");
            Assert.That(four.FallbackIdCount, Is.EqualTo(3));

            Spawn("Treasure_GoldBar", new Vector3(-30f, 0f, 70f), "dup.id");
            Spawn("Treasure_GoldBar", new Vector3(-30f, 0f, 90f), "dup.id");
            yield return null;
            TreasureIdReport dup = TreasureIdValidator.Validate(TreasureSpot.Active);
            Assert.That(dup.IsValid, Is.False);
            Assert.That(dup.DuplicateIds, Is.EqualTo(new[] { "dup.id" }));
        }

        [UnityTest]
        public IEnumerator Scene_DestroyOrder_NeverLeavesAStaleCollectedProvider_ForTheNextScene()
        {
            var store = new MemoryProgressStore();
            yield return LoadScene(store);
            yield return DigAndCollect(Object.FindAnyObjectByType<TreasureSpot>());
            Assert.That(TreasureSpot.AlreadyCollectedProvider, Is.Not.Null, "The controller registered its provider.");

            // Unity destroys scene objects in no fixed order: destroy the ProgressManager BEFORE the controller.
            Object.DestroyImmediate(Object.FindAnyObjectByType<ProgressManager>());
            Object.DestroyImmediate(Object.FindAnyObjectByType<TreasureHuntController>());
            Assert.That(TreasureSpot.AlreadyCollectedProvider, Is.Null,
                "A leftover provider would mark treasures of the NEXT scene as already collected.");
        }

        [UnityTest]
        public IEnumerator Scene_ReloadWithFreshSave_StartsWithEverythingUncollected()
        {
            yield return LoadScene(new MemoryProgressStore());
            yield return DigAndCollect(Object.FindAnyObjectByType<TreasureSpot>());
            yield return UnloadScene();

            yield return LoadScene(new MemoryProgressStore()); // a brand new game
            TreasureSpot coin = Object.FindAnyObjectByType<TreasureSpot>();
            Assert.That(coin.State, Is.EqualTo(TreasureState.Hidden), "Nothing carries over from the previous scene instance.");
            Assert.That(Object.FindAnyObjectByType<TreasureHuntController>().TotalGold, Is.EqualTo(0));
            yield return DigAndCollect(coin);
            Assert.That(Object.FindAnyObjectByType<TreasureHuntController>().TotalGold, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator Scene_TwoSpotsWithTheSameId_PayGoldOnlyOnce()
        {
            ProgressTestIsolation.Warnings.Clear();
            yield return LoadScene(new MemoryProgressStore());
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            TreasureSpot first = Spawn("Treasure_GoldBar", new Vector3(-30f, 0f, 40f), "dup.bar");
            TreasureSpot second = Spawn("Treasure_GoldBar", new Vector3(-30f, 0f, 70f), "dup.bar");
            yield return null;
            int collectedEvents = 0;
            hunt.TreasureCollected += (_, _) => collectedEvents++;

            yield return DigAndCollect(first);
            Assert.That(hunt.TotalGold, Is.EqualTo(50));
            Assert.That(collectedEvents, Is.EqualTo(1));

            yield return DigAndCollect(second);
            Assert.That(second.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(50), "The shared id was already collected: no gold the second time.");
            Assert.That(collectedEvents, Is.EqualTo(1), "No reward event for a treasure that paid nothing.");
            Assert.That(ProgressTestIsolation.Warnings.Exists(w => w.Contains("No gold granted for treasure 'dup.bar'")), Is.True,
                "The duplicate id must be reported as a warning.");
        }
    }
}
#endif
