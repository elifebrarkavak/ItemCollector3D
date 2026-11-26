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
    /// <summary>
    /// LEVEL COMPLETE (stage 17C): the celebration after EVERY treasure of Green Mine (15) or Rocky Canyon (20) is collected, and its three buttons. The treasures are collected through the real DIG and COLLECT
    /// buttons (real mouse taps through the Input System, marked "real tap"), the panel buttons are tapped for real too. Saves are in memory only (the developer's real save file is hashed before and after).
    /// The layout on different screens is in <see cref="LevelCompleteLayoutPlayModeTests"/>.
    /// </summary>
    public sealed class LevelCompletePlayModeTests
    {
        private const string GreenPath = "Assets/Scenes/GreenMine.unity";
        private const string RockyPath = "Assets/Scenes/RockyCanyon.unity";
        private const string AncientPath = "Assets/Scenes/AncientMine.unity";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string CatalogPath = "Assets/Data/Areas/AreaCatalog.asset";

        // treasures left for the test to collect (in this order), in open ground that PlaceAtDistance can stand next to
        private static readonly string[] GreenLeft = { "green_mine.meadow_01", "green_mine.camp_01", "green_mine.forest_01" };
        private static readonly string[] RockyLeft = { "rocky_canyon.outpost_01", "rocky_canyon.overlook_01", "rocky_canyon.riverbed_01" };

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
        private LevelCompleteController panel;
        private readonly List<string> requestedScenes = new();
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
            // The UI input module drops every pointer event while the Editor / Game View is not the focused window (it loses focus when another app is used or the Game View is resized), so the taps below
            // would be swallowed at random. The module has an explicit switch for this; it is restored in TearDown.
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouseLevelComplete");
            touchscreen = InputSystem.AddDevice<Touchscreen>("IC3D_TestTouchscreenLevelComplete");
            keyboard = InputSystem.AddDevice<Keyboard>("IC3D_TestKeyboardLevelComplete");
            requestedScenes.Clear();
            SceneNavigator.LoadHook = name => requestedScenes.Add(name);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (tapRetries > 0)
            {
                Debug.Log($"[LevelComplete tests] {tapRetries} tap(s) had to be repeated (the Editor swallowed a synthetic click)");
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
            SceneNavigator.ConsumeAreaSelectionRequest(); // a request that the hook swallowed must not open the Area Selection in the next test
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            GamePause.Resume();
            GamePause.SetModalOpen(false);
            if (gameView != null)
            {
                yield return gameView.Restore(); // also when an assertion failed
                gameView = null;
                yield return RefocusSimulator(); // resizing the Game View leaves the docked Simulator unfocused: real taps in the next test would miss
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            scene = default;
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

        /// <summary>The treasures of a scene by id and gold (read once per scene from a throw-away load).</summary>
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

        private static string SaveJson(IEnumerable<string> ids, int totalGold, IEnumerable<string> celebrated = null, int tutorial = 0, IEnumerable<string> pending = null)
        {
            var data = new GameProgressData { totalGold = totalGold, tutorialState = tutorial };
            data.collectedTreasureIds.AddRange(ids);
            data.unlockedAreaIds.Add("green_mine");
            if (totalGold >= 500) { data.unlockedAreaIds.Add("rocky_canyon"); data.completedAreaIds.Add("green_mine"); }
            if (totalGold >= 1200) { data.unlockedAreaIds.Add("ancient_mine"); data.completedAreaIds.Add("rocky_canyon"); }
            if (totalGold >= 2500) { data.completedAreaIds.Add("ancient_mine"); }
            if (celebrated != null) { data.celebratedAreaIds.AddRange(celebrated); }
            if (pending != null) { data.pendingCelebrationAreaIds.AddRange(pending); }
            return JsonUtility.ToJson(data);
        }

        private static GameProgressData Saved(MemoryProgressStore s) => JsonUtility.FromJson<GameProgressData>(s.Json);

        /// <summary>The player answered the celebration: gold, treasures, unlocks and tutorial are exactly as before; the area is recorded as celebrated and no longer pending.</summary>
        private void AssertAnswered(GameProgressData before, string areaId)
        {
            GameProgressData now = Saved(store);
            Assert.That(now.totalGold, Is.EqualTo(before.totalGold), "gold unchanged");
            Assert.That(now.collectedTreasureIds, Is.EqualTo(before.collectedTreasureIds), "treasures unchanged");
            Assert.That(now.unlockedAreaIds, Is.EqualTo(before.unlockedAreaIds));
            Assert.That(now.completedAreaIds, Is.EqualTo(before.completedAreaIds));
            Assert.That(now.tutorialState, Is.EqualTo(before.tutorialState));
            Assert.That(now.celebratedAreaIds, Is.EqualTo(new[] { areaId }), "recorded as celebrated");
            Assert.That(now.pendingCelebrationAreaIds, Is.Empty, "no longer pending");
        }

        /// <summary>Loads an area with every treasure collected except the first <paramref name="left"/> ids of <paramref name="leftOrder"/>.</summary>
        private IEnumerator StartArea(string path, string[] leftOrder, int left, int extraGold = 0, string[] celebrated = null, int tutorial = 0, int forcedTotal = -1, string[] pending = null)
        {
            yield return EnsureTable(path);
            List<(string id, int gold)> table = Tables[path];
            string[] uncollected = leftOrder.Take(left).ToArray();
            var seeded = table.Where(t => !uncollected.Contains(t.id)).ToList();
            int total = forcedTotal >= 0 ? forcedTotal : seeded.Sum(t => t.gold) + extraGold;
            store = new MemoryProgressStore(SaveJson(seeded.Select(t => t.id), total, celebrated, tutorial, pending));
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
            panel = TryOne<LevelCompleteController>();
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
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail("Not found: " + typeof(T).Name);
            return null;
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

        /// <summary>
        /// A real tap that is repeated (up to three times, after re-focusing the Simulator window) until its effect <paramref name="done"/> is seen. The Editor sometimes swallows a synthetic click
        /// (it loses focus, or the Simulator's own touchscreen takes the pointer): that is the test environment, not the game. A tap that really does nothing still fails after the last attempt.
        /// </summary>
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
            if (spot.State != TreasureState.Revealed)
            {
                var hits = new List<RaycastResult>();
                Vector2 at = ScreenCentre((RectTransform)dig.transform);
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = at }, hits);
                Debug.Log($"[tap-diag] DIG missed: focused={Application.isFocused} screen={Screen.width}x{Screen.height} at={at} topHit={(hits.Count > 0 ? hits[0].gameObject.name : "none")} active={dig.gameObject.activeInHierarchy} interactable={dig.interactable} paused={GamePause.IsPaused} timeScale={Time.timeScale} digging={hunt.IsDigging} pointer={Pointer.current?.name} mouseAdded={mouse?.added} eventSystem={EventSystem.current != null} modal={GamePause.ModalOpen}");
            }

            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "the dig revealed " + spot.SpotId);
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.25f);
            Canvas.ForceUpdateCanvases();
            yield return ClickUntil((RectTransform)collect.transform, () => spot.State == TreasureState.Collected); // real tap
            yield return WaitUntil(() => spot.State == TreasureState.Collected, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), "the collect took " + spot.SpotId);
        }

        /// <summary>Collects the last treasure(s) up to and including <paramref name="last"/> and waits for the panel to open.</summary>
        private IEnumerator CollectLastAndWaitForPanel(TreasureSpot last)
        {
            yield return TapDigAndCollect(last);
            yield return WaitUntil(() => panel.IsOpen, 15f);
            Assert.That(panel.IsOpen, Is.True, "the LEVEL COMPLETE panel opened after the last COLLECT");
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntranceSeconds + 0.15f); // the entrance animation (unscaled time)
            Canvas.ForceUpdateCanvases();
        }

        // ================================================================ the save and the data (plain C#)

        [Test, Order(1)]
        public void Save_CelebrationFlag_OldSavesLoadUntouched_ItIsNotTheGoldGoal_NewGameClearsIt_OddDataIsTidiedSilently()
        {
            // a game from before 17C: the field is missing; it loads as it is (no repair, no write) and nothing is celebrated
            const string oldSave = "{\"version\":1,\"totalGold\":625,\"collectedTreasureIds\":[\"green_mine.meadow_01\",\"green_mine.camp_01\"],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[\"green_mine\"],\"tutorialState\":2}";
            var old = new MemoryProgressStore(oldSave);
            ProgressService service = ProgressService.Create(catalog, old);
            Assert.That(service.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded), "an old save is loaded as it is, not 'repaired'");
            Assert.That(old.WriteCount, Is.EqualTo(0), "loading never rewrites an old save");
            Assert.That(old.BackupCount, Is.EqualTo(0));
            Assert.That(old.Json, Is.EqualTo(oldSave));
            Assert.That(service.CelebratedAreaIds, Is.Empty);
            Assert.That(service.IsAreaCompleted("green_mine"), Is.True, "the GOLD goal of Green Mine (500) is reached ...");
            Assert.That(service.IsCompletionCelebrated("green_mine"), Is.False, "... which is not the celebration");

            // reaching a gold threshold never records a celebration; marking does, once, without touching gold, treasure or unlocks
            var fresh = new MemoryProgressStore();
            ProgressService game = ProgressService.Create(catalog, fresh);
            game.RegisterCollected("t.one", 499);
            game.RegisterCollected("t.two", 2);
            Assert.That(game.IsAreaCompleted("green_mine") && game.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(game.IsCompletionCelebrated("green_mine"), Is.False);
            int gold = game.TotalGold;
            Assert.That(game.MarkCompletionCelebrated("green_mine"), Is.True);
            Assert.That(game.MarkCompletionCelebrated("green_mine"), Is.True, "marking twice is harmless");
            Assert.That(Saved(fresh).celebratedAreaIds, Is.EqualTo(new[] { "green_mine" }), "recorded once");
            Assert.That(game.TotalGold, Is.EqualTo(gold));
            Assert.That(game.CollectedTreasureIds.Count, Is.EqualTo(2));
            Assert.That(game.MarkCompletionCelebrated("nowhere"), Is.False, "an unknown area is refused");
            Assert.That(game.MarkCompletionCelebrated(null), Is.False);

            // it survives a restart; the other areas are not affected
            ProgressService reopened = ProgressService.Create(catalog, new MemoryProgressStore(fresh.Json));
            Assert.That(reopened.IsCompletionCelebrated("green_mine"), Is.True);
            Assert.That(reopened.IsCompletionCelebrated("rocky_canyon"), Is.False);
            Assert.That(reopened.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));

            // NEW GAME resets it (and writes it)
            Assert.That(game.StartNewGame(), Is.True);
            Assert.That(game.IsCompletionCelebrated("green_mine"), Is.False);
            Assert.That(Saved(fresh).celebratedAreaIds, Is.Empty);
            Assert.That(game.Tutorial, Is.EqualTo(TutorialStatus.Pending), "17B is unchanged");

            // odd data (duplicates, an unknown area, blanks) is tidied silently: not a "Recovered" load
            var odd = new MemoryProgressStore("{\"version\":1,\"totalGold\":0,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[],\"celebratedAreaIds\":[\"green_mine\",\"green_mine\",\"bogus\",\"\"]}");
            ProgressService tidied = ProgressService.Create(catalog, odd);
            Assert.That(tidied.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(tidied.CelebratedAreaIds, Is.EqualTo(new[] { "green_mine" }));

            // by id: only the ids of THIS list count, gold is not involved
            Assert.That(game.CountCollected(new[] { "t.one", "nothing" }), Is.EqualTo(0), "after NEW GAME nothing is collected");
            Assert.That(service.CountCollected(new[] { "green_mine.meadow_01", "green_mine.camp_01", "green_mine.forest_01" }), Is.EqualTo(2));
            Assert.That(service.CountCollected(null), Is.EqualTo(0));
        }

        [Test, Order(1)]
        public void TheNextAreaAndItsScene_FollowTheCatalog_AncientMineIsTheLastOne_AndItsSceneHasNoPanel()
        {
            ProgressService service = ProgressService.Create(catalog, new MemoryProgressStore());
            Assert.That(LevelCompleteController.FollowingArea(service, "green_mine"), Is.EqualTo("rocky_canyon"));
            Assert.That(LevelCompleteController.FollowingArea(service, "rocky_canyon"), Is.EqualTo("ancient_mine"));
            Assert.That(LevelCompleteController.FollowingArea(service, "ancient_mine"), Is.Null, "Ancient Mine is the last area: its GAME COMPLETE is stage 17D");
            Assert.That(LevelCompleteController.FollowingArea(service, "nowhere"), Is.Null);
            Assert.That(SceneNavigator.SceneForArea("rocky_canyon"), Is.EqualTo(SceneNavigator.RockyCanyonScene));
            Assert.That(SceneNavigator.SceneForArea("ancient_mine"), Is.EqualTo(SceneNavigator.AncientMineScene));
            Assert.That(SceneNavigator.SceneForArea("green_mine"), Is.EqualTo(SceneNavigator.GreenMineScene));
            Assert.That(SceneNavigator.SceneForArea("nowhere"), Is.Null);

            string scriptGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/UI/LevelCompleteController.cs");
            string prefabGuid = AssetDatabase.AssetPathToGUID(LevelCompleteUiPrefab);
            Assert.That(File.ReadAllText(LevelCompleteUiPrefab).Contains(scriptGuid), Is.True, "the prefab carries the controller");
            Assert.That(File.ReadAllText(GreenPath).Contains(prefabGuid), Is.True, "Green Mine hosts the panel (a prefab instance)");
            Assert.That(File.ReadAllText(RockyPath).Contains(prefabGuid), Is.True, "Rocky Canyon too");
            string ancient = File.ReadAllText(AncientPath);
            Assert.That(ancient.Contains(prefabGuid) || ancient.Contains(scriptGuid), Is.False, "Ancient Mine has no LEVEL COMPLETE (stage 17D)");
        }

        private const string LevelCompleteUiPrefab = "Assets/Prefabs/UI/LevelComplete.prefab";

        [UnityTest, Order(1)]
        public IEnumerator TheTreasureTotals_AreFifteenSixHundredTwentyFive_TwentyEightForty_AndTheEconomyOpensTheNextArea()
        {
            yield return EnsureTable(GreenPath);
            yield return EnsureTable(RockyPath);
            yield return EnsureTable(AncientPath);
            int green = Tables[GreenPath].Sum(t => t.gold);
            int rocky = Tables[RockyPath].Sum(t => t.gold);
            int ancient = Tables[AncientPath].Sum(t => t.gold);
            Assert.That((Tables[GreenPath].Count, green), Is.EqualTo((15, 625)), "Green Mine: 15 treasures, 625 gold");
            Assert.That((Tables[RockyPath].Count, rocky), Is.EqualTo((20, 840)), "Rocky Canyon: 20 treasures, 840 gold");
            Assert.That((Tables[AncientPath].Count, ancient), Is.EqualTo((25, 1240)), "Ancient Mine: 25 treasures, 1240 gold (unchanged)");
            Assert.That(Tables[GreenPath].Select(t => t.id).Distinct().Count(), Is.EqualTo(15), "ids are unique");

            AreaEntry rockyEntry = catalog.Get("rocky_canyon");
            AreaEntry ancientEntry = catalog.Get("ancient_mine");
            Assert.That(rockyEntry.unlockTotalGold, Is.EqualTo(500));
            Assert.That(ancientEntry.unlockTotalGold, Is.EqualTo(1200));
            Assert.That(catalog.Get("ancient_mine").goalTotalGold, Is.EqualTo(2500), "the final goal is untouched");
            Assert.That(green, Is.GreaterThanOrEqualTo(rockyEntry.unlockTotalGold), "15 / 15 Green Mine opens Rocky Canyon on its own");
            // Rocky Canyon can only be entered with 500 gold; whatever the player had, all 20 treasures bring it to at least 500 + 840
            Assert.That(rockyEntry.unlockTotalGold + rocky, Is.GreaterThanOrEqualTo(ancientEntry.unlockTotalGold), "20 / 20 Rocky Canyon opens Ancient Mine whatever else was collected");
            Assert.That(green + rocky, Is.GreaterThanOrEqualTo(ancientEntry.unlockTotalGold));
            Assert.That(1200 + ancient, Is.LessThan(2500), "1200 gold into Ancient Mine and all 25 treasures is 2440: NOT the 2500 final (stage 17D)");
        }

        // ================================================================ when the panel opens

        [UnityTest, Order(1)]
        public IEnumerator GreenMine_14Of15_ShowsNothing_ThenTheLastCollect_OpensTheCelebration_AfterTheRewardIsDone_AndGoldIsPaidOnce()
        {
            CharacterSelection.Select(CharacterSelection.MaleId);
            yield return StartArea(GreenPath, GreenLeft, 2);
            TreasureSpot first = Spot(GreenLeft[0]);
            TreasureSpot last = Spot(GreenLeft[1]);
            var bits = new List<string>();
            Assert.That(panel.CurrentPhase, Is.EqualTo(LevelCompleteController.Phase.Idle));
            Assert.That(panel.Summarize().Collected, Is.EqualTo(13));
            Assert.That(One<TutorialController>().IsRunning, Is.False);

            // 14 / 15: nothing, however long we wait
            yield return TapDigAndCollect(first);
            yield return new WaitForSecondsRealtime(LevelCompleteController.SettleSeconds + 2.6f);
            Assert.That(panel.Summarize().Collected, Is.EqualTo(14));
            Assert.That(panel.CurrentPhase, Is.EqualTo(LevelCompleteController.Phase.Idle), "14 / 15 does not celebrate");
            Assert.That(panel.IsOpen || panel.PanelRoot.activeSelf, Is.False);
            Assert.That(panel.ShowCount, Is.EqualTo(0));
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Saved(store).celebratedAreaIds, Is.Empty);
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.Empty, "nothing is owed at 14 / 15");

            // 15 / 15: the last COLLECT. The reward and the collect animation play out first, then the panel opens
            int goldBefore = hunt.TotalGold;
            float collectedAt = -1f, openedAt = -1f;
            last.Collected += _ => collectedAt = Time.realtimeSinceStartup;
            panel.Opened += _ => openedAt = Time.realtimeSinceStartup;
            FloatingRewardText rewardText = One<FloatingRewardText>();
            int rewardsBefore = rewardText.ShowCount;
            bool animatedWhileClosed = false;
            yield return TapDigAndCollect(last);
            Assert.That(panel.IsOpen, Is.False, "the panel is not open yet ...");
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }), "... but the celebration is already owed in the save, from the moment of the last COLLECT");
            Assert.That(Saved(store).collectedTreasureIds, Does.Contain(last.SpotId));
            while (!panel.IsOpen && Time.realtimeSinceStartup < collectedAt + 15f)
            {
                animatedWhileClosed |= last.VisualAnimator != null && last.VisualAnimator.IsAnimating;
                yield return null;
            }

            Assert.That(panel.IsOpen, Is.True, "the panel opens after the last COLLECT");
            Assert.That(openedAt - collectedAt, Is.GreaterThanOrEqualTo(TreasureVisualAnimator.CollectSeconds + LevelCompleteController.SettleSeconds - 0.2f),
                "it waited for the collect animation (0.4 s) and the settle time instead of cutting the effect");
            Assert.That(last.VisualAnimator.IsAnimating, Is.False, "the collect animation had finished");
            Assert.That(rewardText.ShowCount, Is.EqualTo(rewardsBefore + 1), "the normal reward text was shown");
            Assert.That(rewardText.IsVisible, Is.False, "... and was finished (not cut) when the panel came");
            Assert.That(One<ProgressNotificationPresenter>().IsBusy, Is.False, "no banner was cut either");
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntranceSeconds + 0.15f);
            bits.Add("animatedWhileClosed=" + animatedWhileClosed);

            // gold: exactly once, saved
            int value = last.Definition.GoldValue;
            Assert.That(hunt.TotalGold, Is.EqualTo(goldBefore + value), "the last treasure paid its gold once");
            GameProgressData saved = Saved(store);
            Assert.That(saved.collectedTreasureIds.Count(id => id == last.SpotId), Is.EqualTo(1));
            Assert.That(saved.collectedTreasureIds.Count, Is.EqualTo(15));
            Assert.That(saved.totalGold, Is.EqualTo(goldBefore + value));
            Assert.That(saved.pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }), "the celebration stays owed until the player answers it");
            Assert.That(saved.celebratedAreaIds, Is.Empty, "not celebrated yet: the player has not answered");
            Assert.That(One<ProgressManager>().Service.RegisterCollected(last.SpotId, value), Is.EqualTo(RegisterResult.AlreadyCollected));
            Assert.That(hunt.TotalGold, Is.EqualTo(goldBefore + value), "a second registration gives nothing");
            Assert.That(panel.Summarize().Collected, Is.EqualTo(15));

            // the panel
            Assert.That(panel.TitleString, Is.EqualTo("LEVEL COMPLETE!"));
            Assert.That(panel.AreaString, Is.EqualTo("GREEN MINE COMPLETED"));
            Assert.That(panel.CountString, Is.EqualTo("15 / 15 TREASURES COLLECTED"));
            Assert.That(panel.GoldString, Is.EqualTo("625 GOLD AVAILABLE IN THIS AREA"), "what the AREA can pay ...");
            Assert.That(panel.TotalString, Is.EqualTo($"YOUR TOTAL GOLD: {goldBefore + value}"), "... is not the player's total gold");
            Assert.That(panel.NextString, Is.EqualTo("GO TO ROCKY CANYON"));
            Assert.That(panel.NextAreaId, Is.EqualTo("rocky_canyon"));
            Assert.That(panel.NextButton.interactable, Is.True);
            Assert.That(panel.StayButton.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("STAY HERE"));
            Assert.That(panel.AreaSelectionButton.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("AREA SELECTION"));
            Assert.That(panel.Picture.sprite, Is.Not.Null, "the picture of the area");
            Assert.That(panel.Picture.sprite.name, Does.Contain("green_mine"));
            Assert.That(panel.Trophy.gameObject.activeInHierarchy, Is.True, "the trophy");
            Assert.That(panel.Window.localScale.x, Is.EqualTo(1f).Within(0.02f), "the entrance animation has settled");

            // the game is paused and the HUD is hidden; the music goes on; the jingle played once
            Assert.That(GamePause.IsPaused, Is.True);
            Assert.That(GamePause.ModalOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            foreach (HideWhilePaused hidden in Object.FindObjectsByType<HideWhilePaused>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hidden.gameObject.scene == scene && hidden.isActiveAndEnabled)
                {
                    Assert.That(hidden.IsHidden, Is.True, hidden.name + " is hidden behind the panel");
                }
            }

            Assert.That(panel.JinglePlayCount, Is.EqualTo(1), "one short jingle");
            Assert.That(panel.LastJingleVolume, Is.EqualTo(LevelCompleteController.JingleVolume * GameSettings.SfxScale).Within(0.001f));
            AmbientMusicPlayer music = One<AmbientMusicPlayer>();
            Assert.That(music.IsPlaying, Is.True, "the background music keeps playing");
            Assert.That(music.Source.ignoreListenerPause, Is.True);
            Assert.That(FindButton("PauseButton").gameObject.activeSelf, Is.False, "no PAUSE button under the panel");
            Debug.Log("[LevelComplete] " + string.Join(", ", bits) + $", opened {(openedAt - collectedAt):F2} s after the COLLECT");
        }

        [UnityTest, Order(1)]
        public IEnumerator RockyCanyon_19Of20_ShowsNothing_ThenTheLastCollect_OpensTheCelebration_WithTheAncientMineButton()
        {
            yield return StartArea(RockyPath, RockyLeft, 2, extraGold: 625);
            TreasureSpot first = Spot(RockyLeft[0]);
            TreasureSpot last = Spot(RockyLeft[1]);
            Assert.That(panel.Summarize().Collected, Is.EqualTo(18));

            yield return TapDigAndCollect(first);
            yield return new WaitForSecondsRealtime(LevelCompleteController.SettleSeconds + 2.6f);
            Assert.That(panel.Summarize().Collected, Is.EqualTo(19));
            Assert.That(panel.CurrentPhase, Is.EqualTo(LevelCompleteController.Phase.Idle), "19 / 20 does not celebrate");
            Assert.That(panel.ShowCount, Is.EqualTo(0));
            Assert.That(GamePause.IsPaused, Is.False);

            int goldBefore = hunt.TotalGold;
            yield return CollectLastAndWaitForPanel(last);
            Assert.That(panel.AreaString, Is.EqualTo("ROCKY CANYON COMPLETED"));
            Assert.That(panel.CountString, Is.EqualTo("20 / 20 TREASURES COLLECTED"));
            Assert.That(panel.GoldString, Is.EqualTo("840 GOLD AVAILABLE IN THIS AREA"));
            Assert.That(panel.TotalString, Is.EqualTo($"YOUR TOTAL GOLD: {goldBefore + last.Definition.GoldValue}"));
            Assert.That(panel.NextString, Is.EqualTo("GO TO ANCIENT MINE"));
            Assert.That(panel.NextAreaId, Is.EqualTo("ancient_mine"));
            Assert.That(panel.NextButton.interactable, Is.True, "Ancient Mine is open (1200 gold)");
            Assert.That(panel.Picture.sprite.name, Does.Contain("rocky_canyon"));
            Assert.That(panel.AccentColor.r, Is.GreaterThan(panel.AccentColor.g), "warm orange for Rocky Canyon");
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.EqualTo(new[] { "rocky_canyon" }));
            Assert.That(Saved(store).celebratedAreaIds, Is.Empty);
            Assert.That(hunt.TotalGold, Is.EqualTo(goldBefore + last.Definition.GoldValue), "paid once");
            Assert.That(One<TreasureHuntController>().AreaId, Is.EqualTo("rocky_canyon"));
        }

        [UnityTest, Order(1)]
        public IEnumerator TheGoldGoalAlone_NeverOpensIt_ButItStillUnlocksRockyCanyon_AsBefore()
        {
            // 13 / 15 collected and 1 gold short of the 500 that opens Rocky Canyon: the next treasure crosses the line
            yield return StartArea(GreenPath, GreenLeft, 2, forcedTotal: 499);
            TreasureSpot crossing = Spot(GreenLeft[0]);
            ProgressService service = One<ProgressManager>().Service;
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False);
            yield return TapDigAndCollect(crossing);
            Assert.That(service.TotalGold, Is.GreaterThanOrEqualTo(500));
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.True, "500 gold opens Rocky Canyon");
            Assert.That(service.IsAreaCompleted("green_mine"), Is.True, "and reaches the gold goal of Green Mine (AREA COMPLETE)");
            ProgressNotificationPresenter notices = One<ProgressNotificationPresenter>();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(notices.ShownCount, Is.GreaterThan(0), "the usual AREA COMPLETE / NEW AREA UNLOCKED banners");
            yield return WaitUntil(() => !notices.IsBusy, 8f);
            yield return new WaitForSecondsRealtime(LevelCompleteController.SettleSeconds + 1.5f);
            Assert.That(panel.Summarize().Collected, Is.EqualTo(14), "14 / 15");
            Assert.That(panel.CurrentPhase, Is.EqualTo(LevelCompleteController.Phase.Idle), "the gold goal is not a full collection: no LEVEL COMPLETE");
            Assert.That(panel.ShowCount, Is.EqualTo(0));
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Saved(store).celebratedAreaIds, Is.Empty);
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.Empty, "the gold goal owes no celebration");
        }

        [UnityTest, Order(1)]
        public IEnumerator ThePanelWaitsForTheTutorial_WhenItIsPending_AndBothAreRecorded()
        {
            // a NEW GAME save whose tutorial is still pending (an artificial 14 / 15 so the very first collect is also the last)
            yield return StartArea(GreenPath, GreenLeft, 1, tutorial: (int)TutorialStatus.Pending);
            TutorialController tutorial = One<TutorialController>();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tutorial.IsRunning, Is.True, "the 17B tutorial is running");
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return TapDigAndCollect(last);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Complete), "the tutorial completes with this collect (17B unchanged)");
            Assert.That(panel.IsOpen, Is.False, "TUTORIAL COMPLETE! is still on screen: the celebration waits for it");
            yield return WaitUntil(() => panel.IsOpen, 15f);
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(tutorial.IsRunning, Is.False, "the tutorial was finished before the panel opened: no two overlays at once");
            Assert.That(tutorial.UiRoot.activeInHierarchy, Is.False);
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.False);
            GameProgressData saved = Saved(store);
            Assert.That(saved.tutorialState, Is.EqualTo((int)TutorialStatus.Finished));
            Assert.That(saved.pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }));
            Assert.That(tutorial.SkipButton.gameObject.activeInHierarchy, Is.False, "no SKIP TUTORIAL behind the panel");
        }

        // ================================================================ the three buttons

        [UnityTest, Order(1)]
        public IEnumerator GoToRockyCanyon_ByARealTap_LoadsRockyCanyon_WithRuby_TheGoldAndTheTreasuresKept()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            GameSettings.Current.SetSfxVolume(0.6f);
            GameSettings.Current.SetCameraSensitivity(1.5f);
            yield return StartArea(GreenPath, GreenLeft, 1);
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return CollectLastAndWaitForPanel(last);
            int gold = hunt.TotalGold;
            Assert.That(gold, Is.EqualTo(625));
            GameProgressData savedBefore = Saved(store);
            Assert.That(savedBefore.pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }));

            yield return ClickUntil((RectTransform)panel.NextButton.transform, () => requestedScenes.Count > 0); // real tap on GO TO ROCKY CANYON
            Assert.That(requestedScenes, Is.EqualTo(new[] { SceneNavigator.RockyCanyonScene }), "the existing SceneNavigator was asked for Rocky Canyon, once");
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(GamePause.IsPaused, Is.False, "the next scene never starts frozen");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            AssertAnswered(savedBefore, "green_mine"); // the choice is recorded; gold and treasures are exactly as before

            // what the real SceneManager does next: Rocky Canyon from the same save and settings
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadScene(RockyPath, store);
            ProgressService service = One<ProgressManager>().Service;
            Assert.That(service.TotalGold, Is.EqualTo(625), "the total gold is kept");
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(15), "the collected treasures are kept");
            Assert.That(hunt.AreaId, Is.EqualTo("rocky_canyon"));
            Assert.That(hunt.IsAreaAccessible, Is.True, "the Gold lock: 625 >= 500");
            Assert.That(One<PlayerCharacterVisual>().AppliedId, Is.EqualTo(CharacterSelection.FemaleId), "Ruby spawns in Rocky Canyon");
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.6f).Within(0.001f), "the Music / SFX / camera settings are kept");
            Assert.That(GameSettings.Current.CameraSensitivity, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(One<LevelCompleteController>().CurrentPhase, Is.EqualTo(LevelCompleteController.Phase.Idle), "the celebration does not open by itself in the next scene");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(One<LevelCompleteController>().ShowCount, Is.EqualTo(0));

            // the scene works: HUD, camera, detector, DIG and COLLECT
            ThirdPersonCameraController camera = One<ThirdPersonCameraController>();
            Assert.That(camera.Target, Is.EqualTo(Player), "the camera follows the player");
            Assert.That(Vector3.Distance(camera.transform.position, Player.position), Is.LessThan(20f), "the camera is next to the player");
            Assert.That(Physics.Raycast(Player.position + Vector3.up * 0.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore), Is.True, "the player stands on the ground at the start point");
            Assert.That(One<HudController>().CurrentGold, Is.EqualTo(625), "the HUD shows the carried gold");
            TreasureSpot rocky = Spot(RockyLeft[0]);
            Teleport(Player, PlaceAtDistance(rocky, 6f));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(One<TreasureDetector>().HasSignal, Is.True, "the detector works");
            yield return TapDigAndCollect(rocky);
            Assert.That(hunt.TotalGold, Is.EqualTo(625 + rocky.Definition.GoldValue), "DIG and COLLECT work in the new area");
        }

        [UnityTest, Order(1)]
        public IEnumerator GoToAncientMine_ByARealTap_LoadsAncientMine_WithBarnaby_TheGoldAndTheTreasuresKept()
        {
            CharacterSelection.Select(CharacterSelection.MaleId);
            yield return StartArea(RockyPath, RockyLeft, 1, extraGold: 625);
            TreasureSpot last = Spot(RockyLeft[0]);
            yield return CollectLastAndWaitForPanel(last);
            int gold = hunt.TotalGold;
            Assert.That(gold, Is.EqualTo(625 + 840));

            yield return ClickUntil((RectTransform)panel.NextButton.transform, () => requestedScenes.Count > 0); // real tap on GO TO ANCIENT MINE
            Assert.That(requestedScenes, Is.EqualTo(new[] { SceneNavigator.AncientMineScene }));
            Assert.That(GamePause.IsPaused, Is.False);

            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadScene(AncientPath, store);
            Assert.That(One<ProgressManager>().Service.TotalGold, Is.EqualTo(gold), "the total gold is kept");
            Assert.That(One<ProgressManager>().Service.CollectedTreasureIds.Count, Is.EqualTo(20), "the 20 Rocky Canyon treasures are kept");
            Assert.That(hunt.AreaId, Is.EqualTo("ancient_mine"));
            Assert.That(hunt.IsAreaAccessible, Is.True, "the Gold lock: 1465 >= 1200");
            Assert.That(One<PlayerCharacterVisual>().AppliedId, Is.EqualTo(CharacterSelection.MaleId), "Barnaby spawns in Ancient Mine");
            Assert.That(Physics.Raycast(Player.position + Vector3.up * 0.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore), Is.True, "the player stands on the cave floor at the start point");
            Assert.That(One<HudController>().CurrentGold, Is.EqualTo(gold));
            Assert.That(One<ThirdPersonCameraController>().Target, Is.EqualTo(Player));
            Assert.That(scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<LevelCompleteController>(true) != null), Is.False, "Ancient Mine has no LEVEL COMPLETE (17D)");
        }

        [UnityTest, Order(1)]
        public IEnumerator GoTo_RefusesALockedNextArea_AndKeepsThePanelOpen()
        {
            // an inconsistent save (all 15 treasures but only a few gold): Rocky Canyon would still be locked, and the panel must not be a way in
            yield return StartArea(GreenPath, GreenLeft, 1, forcedTotal: 0);
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return CollectLastAndWaitForPanel(last);
            ProgressService service = One<ProgressManager>().Service;
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.EqualTo(service.TotalGold >= 500));
            if (service.IsAreaUnlocked("rocky_canyon"))
            {
                Assert.Inconclusive("the last treasure alone is worth 500 gold or more: this scenario needs a cheaper treasure");
            }

            Assert.That(panel.NextButton.interactable, Is.False, "GO TO ROCKY CANYON is disabled while the area is locked");
            Assert.That(panel.TotalString, Does.Contain("OPENS AT 500 GOLD"));
            Assert.That(panel.GoToNextArea(), Is.EqualTo(LevelCompleteController.GoResult.Locked), "and refused when asked directly");
            yield return Click(ScreenCentre((RectTransform)panel.NextButton.transform));
            Assert.That(requestedScenes, Is.Empty, "no scene was requested");
            Assert.That(panel.IsOpen, Is.True, "the panel stays open");
            Assert.That(panel.StayButton.interactable && panel.AreaSelectionButton.interactable, Is.True, "STAY HERE and AREA SELECTION still work");
        }

        [UnityTest, Order(1)]
        public IEnumerator StayHere_ByARealTap_ReturnsToNormalPlay_ChangesNothing_AndNewGameClearsTheRecord()
        {
            yield return StartArea(GreenPath, GreenLeft, 1);
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return CollectLastAndWaitForPanel(last);
            GameProgressData savedBefore = Saved(store);
            int gold = hunt.TotalGold;

            yield return ClickUntil((RectTransform)panel.StayButton.transform, () => !panel.IsOpen); // real tap
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(panel.PanelRoot.activeSelf, Is.False, "the panel is gone");
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(GamePause.ModalOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "the game runs again");
            Assert.That(requestedScenes, Is.Empty, "STAY HERE does not change the scene: the game is not restarted");
            AssertAnswered(savedBefore, "green_mine"); // only the celebration record changed: no gold or treasure record
            Assert.That(hunt.TotalGold, Is.EqualTo(gold));
            yield return new WaitForSecondsRealtime(0.3f);
            foreach (HideWhilePaused hidden in Object.FindObjectsByType<HideWhilePaused>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hidden.gameObject.scene == scene && hidden.isActiveAndEnabled)
                {
                    Assert.That(hidden.IsHidden, Is.False, hidden.name + " is back");
                }
            }

            Assert.That(FindButton("PauseButton").gameObject.activeSelf, Is.True, "the PAUSE button is back");

            // the miner walks and the camera turns again (real joystick drag, real camera drag)
            Vector3 before = Player.position;
            yield return DragJoystick(1.5f);
            Assert.That(Vector3.Distance(Player.position, before), Is.GreaterThan(1.5f), "the joystick works again");
            float yaw = One<ThirdPersonCameraController>().Yaw;
            yield return DragCamera(40);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, One<ThirdPersonCameraController>().Yaw)), Is.GreaterThan(5f), "the camera orbits again");

            // Pause / Resume still work, and the panel does not come back
            Button pause = FindButton("PauseButton");
            Canvas.ForceUpdateCanvases();
            yield return ClickUntil((RectTransform)pause.transform, () => GamePause.IsPaused); // real tap
            Assert.That(GamePause.IsPaused, Is.True, "the Pause Menu opens");
            One<PauseMenuController>().Resume();
            Assert.That(GamePause.IsPaused, Is.False);
            yield return new WaitForSecondsRealtime(LevelCompleteController.SettleSeconds + 1.2f);
            Assert.That(panel.ShowCount, Is.EqualTo(1), "it opened exactly once");
            Assert.That(panel.IsOpen, Is.False);

            // NEW GAME erases the record, so the area can be celebrated again
            ProgressService service = One<ProgressManager>().Service;
            Assert.That(service.IsCompletionCelebrated("green_mine"), Is.True);
            Assert.That(service.StartNewGame(), Is.True);
            Assert.That(service.IsCompletionCelebrated("green_mine"), Is.False, "NEW GAME resets the record");
            Assert.That(Saved(store).celebratedAreaIds, Is.Empty);
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.Empty);
        }

        [UnityTest, Order(1)]
        public IEnumerator TheAndroidBackKey_DoesStayHere_AndNeverOpensThePauseMenu()
        {
            yield return StartArea(GreenPath, GreenLeft, 1);
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return CollectLastAndWaitForPanel(last);
            Assert.That(panel.IsOpen, Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            Assert.That(panel.IsOpen, Is.False, "Back closes the panel like STAY HERE");
            Assert.That(GamePause.IsPaused, Is.False, "and does not open the Pause Menu");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(requestedScenes, Is.Empty);
            Assert.That(FindButton("PauseButton").gameObject.activeSelf, Is.True);
            Assert.That(panel.ShowCount, Is.EqualTo(1));

            // with the panel closed Back is the Pause Menu's again
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(GamePause.IsPaused, Is.True, "the Pause Menu answers Back again once the panel is closed");
            One<PauseMenuController>().Resume();
        }

        [UnityTest, Order(1)]
        public IEnumerator AreaSelection_ByARealTap_OpensTheAreaSelection_WithTheSavedGoldTheOpenAreasAndTheMiner()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return StartArea(GreenPath, GreenLeft, 1);
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return CollectLastAndWaitForPanel(last);
            GameProgressData savedBefore = Saved(store);

            yield return ClickUntil((RectTransform)panel.AreaSelectionButton.transform, () => requestedScenes.Count > 0); // real tap
            Assert.That(requestedScenes, Is.EqualTo(new[] { SceneNavigator.MainMenuScene }), "the Main Menu is requested through the SceneNavigator");
            Assert.That(SceneNavigator.AreaSelectionRequested, Is.True, "... with the Area Selection open");
            Assert.That(panel.IsOpen, Is.False, "the panel is closed");
            Assert.That(GamePause.IsPaused, Is.False);
            AssertAnswered(savedBefore, "green_mine");

            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadScene(MainMenuPath, store);
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu.IsAreaSelectOpen, Is.True, "the existing Area Selection is open");
            Assert.That(SceneNavigator.AreaSelectionRequested, Is.False, "the request was used up");
            AreaSelectController areas = Object.FindAnyObjectByType<AreaSelectController>();
            AreaCardState green = areas.GetState("green_mine");
            AreaCardState rocky = areas.GetState("rocky_canyon");
            AreaCardState ancient = areas.GetState("ancient_mine");
            Assert.That(green.TotalGold, Is.EqualTo(625), "the saved total gold");
            Assert.That(green.Availability, Is.EqualTo(AreaAvailability.Playable));
            Assert.That(green.Completed, Is.True);
            Assert.That(rocky.Availability, Is.EqualTo(AreaAvailability.Playable), "Rocky Canyon is open (625 >= 500)");
            Assert.That(ancient.Availability, Is.EqualTo(AreaAvailability.Locked), "Ancient Mine is still locked (625 < 1200)");
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "the miner choice is kept");
            Assert.That(areas.TryEnter("ancient_mine"), Is.EqualTo(AreaEnterResult.Locked), "a locked area still cannot be entered");
            Assert.That(areas.TryEnter("rocky_canyon"), Is.EqualTo(AreaEnterResult.Entered), "an open one can be chosen");
            Assert.That(requestedScenes.Last(), Is.EqualTo(SceneNavigator.RockyCanyonScene));

            // BACK leads to the normal Main Menu
            areas.Cancel();
            Assert.That(menu.IsAreaSelectOpen, Is.False);
            Assert.That(Object.FindAnyObjectByType<MenuCharacterStage>(), Is.Not.Null);

            // a later, ordinary visit of the Main Menu does not open the Area Selection by itself
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadScene(MainMenuPath, store);
            yield return null;
            Assert.That(Object.FindAnyObjectByType<MainMenuController>().IsAreaSelectOpen, Is.False);
        }

        // ================================================================ touches

        [UnityTest, Order(1)]
        public IEnumerator TheOpenPanel_TakesEveryTouch_TheJoystickTheCameraAndTheButtonsBeneathGetNothing()
        {
            yield return StartArea(GreenPath, GreenLeft, 1);
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return CollectLastAndWaitForPanel(last);
            Assert.That(panel.IsOpen, Is.True);

            // every point of the screen that has a control under the panel: the first thing hit is the panel
            var points = new List<(string name, Vector2 point)>
            {
                ("joystick", ScreenCentre((RectTransform)One<VirtualJoystick>().transform)),
                ("PAUSE button", ScreenCentre((RectTransform)FindButton("PauseButton").transform)),
                ("DIG button", ScreenCentre((RectTransform)FindButton("DigButton").transform)),
                ("COLLECT button", ScreenCentre((RectTransform)FindButton("CollectButton").transform)),
                ("camera area (right)", new Vector2(Screen.width * 0.85f, Screen.height * 0.5f)),
                ("camera area (centre)", new Vector2(Screen.width * 0.5f, Screen.height * 0.12f)),
                ("bottom left corner", new Vector2(8f, 8f)),
                ("top right corner", new Vector2(Screen.width - 8f, Screen.height - 8f)),
            };
            foreach ((string name, Vector2 point) in points)
            {
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
                Assert.That(results, Is.Not.Empty, name);
                Assert.That(results[0].gameObject.transform.IsChildOf(panel.PanelRoot.transform), Is.True, $"{name}: the first thing a touch hits is the panel, not '{results[0].gameObject.name}'");
            }

            // a real finger on the joystick and a real camera drag do nothing
            Vector3 position = Player.position;
            float yaw = One<ThirdPersonCameraController>().Yaw;
            yield return DragJoystick(0.8f);
            yield return DragCamera(30);
            Assert.That(Vector3.Distance(Player.position, position), Is.LessThan(0.05f), "the joystick is dead behind the panel");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, One<ThirdPersonCameraController>().Yaw)), Is.LessThan(0.5f), "the camera does not orbit behind the panel");
            Assert.That(One<VirtualJoystick>().Value, Is.EqualTo(Vector2.zero));

            // taps on the DIG / PAUSE places do nothing
            yield return Click(points[1].point);
            yield return Click(points[2].point);
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(hunt.IsDigging, Is.False);
            Assert.That(requestedScenes, Is.Empty);
        }

        // ================================================================ never again

        [UnityTest, Order(1)]
        public IEnumerator TheCelebration_IsNotRepeated_ByAReload_AnOldSaveWithEveryTreasure_OrAnAlreadyCelebratedArea()
        {
            yield return EnsureTable(GreenPath);
            List<(string id, int gold)> table = Tables[GreenPath];

            // (1) an old save (from before 17C) that already has all 15 treasures: opening the scene never celebrates and never rewrites the file
            string oldSave = SaveJson(table.Select(t => t.id), 625);
            oldSave = oldSave.Replace(",\"celebratedAreaIds\":[]", string.Empty);
            Assert.That(oldSave, Does.Not.Contain("celebratedAreaIds"), "an old save has no such field");
            var oldStore = new MemoryProgressStore(oldSave);
            yield return LoadScene(GreenPath, oldStore);
            yield return new WaitForSecondsRealtime(LevelCompleteController.SettleSeconds + 1.5f);
            Assert.That(panel.ShowCount, Is.EqualTo(0), "an old, complete save does not open the panel by itself");
            Assert.That(panel.CurrentPhase, Is.EqualTo(LevelCompleteController.Phase.Idle));
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(oldStore.Json, Is.EqualTo(oldSave), "the old save is not touched");
            Assert.That(oldStore.WriteCount, Is.EqualTo(0));
            Assert.That(panel.Summarize().IsComplete, Is.True, "(and it IS complete)");
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;

            // (2) the same save, loaded again and again: still nothing
            for (int i = 0; i < 2; i++)
            {
                yield return LoadScene(GreenPath, new MemoryProgressStore(oldSave));
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(panel.ShowCount, Is.EqualTo(0), "load " + i);
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
            }

            // (3) an area that was already celebrated: collecting its last treasure does not open it again (14 / 15, then the last)
            yield return StartArea(GreenPath, GreenLeft, 1, celebrated: new[] { "green_mine" });
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return TapDigAndCollect(last);
            yield return new WaitForSecondsRealtime(LevelCompleteController.SettleSeconds + 3f);
            Assert.That(panel.Summarize().IsComplete, Is.True);
            Assert.That(panel.ShowCount, Is.EqualTo(0), "already celebrated: no second celebration");
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.Empty, "and nothing is owed either");
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;

            // (4) the same for an old, finished Rocky Canyon (20 / 20, from before 17C, no celebration fields at all)
            yield return EnsureTable(RockyPath);
            string oldRocky = SaveJson(Tables[RockyPath].Select(t => t.id), 625 + 840);
            oldRocky = oldRocky.Replace(",\"celebratedAreaIds\":[]", string.Empty).Replace(",\"pendingCelebrationAreaIds\":[]", string.Empty);
            Assert.That(oldRocky, Does.Not.Contain("elebration"), "an old save has neither field");
            var oldRockyStore = new MemoryProgressStore(oldRocky);
            yield return LoadScene(RockyPath, oldRockyStore);
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntrySeconds + LevelCompleteController.SettleSeconds + 1.5f);
            Assert.That(panel.Summarize().IsComplete, Is.True);
            Assert.That(panel.ShowCount, Is.EqualTo(0), "an old, finished Rocky Canyon does not celebrate by itself");
            Assert.That(oldRockyStore.Json, Is.EqualTo(oldRocky));
            Assert.That(oldRockyStore.WriteCount, Is.EqualTo(0));
        }


        // ================================================================ a celebration that was owed when the player left

        [Test, Order(1)]
        public void Save_PendingCelebration_IsOwedUntilAnswered_SurvivesARestart_NewGameClearsIt_OldAndOddSavesAreSafe()
        {
            // a game from before the pending state: no field, loaded as it is, nothing owed
            const string oldSave = "{\"version\":1,\"totalGold\":625,\"collectedTreasureIds\":[\"green_mine.meadow_01\"],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[\"green_mine\"],\"tutorialState\":2,\"celebratedAreaIds\":[]}";
            var old = new MemoryProgressStore(oldSave);
            ProgressService oldService = ProgressService.Create(catalog, old);
            Assert.That(oldService.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(old.WriteCount, Is.EqualTo(0), "an old save is never rewritten on loading");
            Assert.That(old.Json, Is.EqualTo(oldSave));
            Assert.That(oldService.PendingCelebrationAreaIds, Is.Empty);
            Assert.That(oldService.IsCompletionPending("green_mine"), Is.False);

            // the last treasure: the celebration is owed, and only that is written (gold, treasures and unlocks untouched)
            var store = new MemoryProgressStore();
            ProgressService game = ProgressService.Create(catalog, store);
            game.RegisterCollected("t.one", 10);
            GameProgressData before = Saved(store);
            Assert.That(game.MarkCompletionPending("green_mine"), Is.True);
            Assert.That(game.MarkCompletionPending("green_mine"), Is.True, "marking twice is harmless");
            GameProgressData owed = Saved(store);
            Assert.That(owed.pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }), "owed once");
            Assert.That(owed.celebratedAreaIds, Is.Empty);
            Assert.That((owed.totalGold, owed.collectedTreasureIds.Count), Is.EqualTo((before.totalGold, before.collectedTreasureIds.Count)), "gold and treasures are as they were");
            Assert.That(game.IsCompletionPending("rocky_canyon"), Is.False, "per area");
            Assert.That(game.MarkCompletionPending("nowhere"), Is.False, "an unknown area is refused");
            Assert.That(game.MarkCompletionPending(null), Is.False);

            // it survives a restart
            var restartedStore = new MemoryProgressStore(store.Json);
            ProgressService restarted = ProgressService.Create(catalog, restartedStore);
            Assert.That(restarted.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(restarted.IsCompletionPending("green_mine"), Is.True, "still owed after a restart");
            Assert.That(restarted.IsCompletionCelebrated("green_mine"), Is.False);
            Assert.That(restartedStore.WriteCount, Is.EqualTo(0));

            // the answer: celebrated and no longer pending, in ONE write
            Assert.That(restarted.MarkCompletionCelebrated("green_mine"), Is.True);
            Assert.That(restartedStore.WriteCount, Is.EqualTo(1), "one save for both changes");
            GameProgressData answered = Saved(restartedStore);
            Assert.That(answered.celebratedAreaIds, Is.EqualTo(new[] { "green_mine" }));
            Assert.That(answered.pendingCelebrationAreaIds, Is.Empty);
            Assert.That(answered.totalGold, Is.EqualTo(before.totalGold));
            Assert.That(restarted.MarkCompletionPending("green_mine"), Is.False, "a celebrated area is never owed again");
            Assert.That(restarted.IsCompletionPending("green_mine"), Is.False);
            Assert.That(restarted.MarkCompletionCelebrated("green_mine"), Is.True, "answering twice is harmless");
            Assert.That(restartedStore.WriteCount, Is.EqualTo(1), "and writes nothing more");

            // NEW GAME clears what is owed and what was celebrated
            Assert.That(game.MarkCompletionPending("rocky_canyon"), Is.True);
            Assert.That(game.StartNewGame(), Is.True);
            Assert.That(game.IsCompletionPending("rocky_canyon"), Is.False);
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.Empty);
            Assert.That(Saved(store).celebratedAreaIds, Is.Empty);

            // odd data is tidied silently: duplicates, unknown or blank areas, and an area that is both owed and celebrated (celebrated wins)
            var odd = new MemoryProgressStore("{\"version\":1,\"totalGold\":0,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[],\"celebratedAreaIds\":[\"green_mine\"],\"pendingCelebrationAreaIds\":[\"green_mine\",\"rocky_canyon\",\"rocky_canyon\",\"bogus\",\"\"]}");
            ProgressService tidied = ProgressService.Create(catalog, odd);
            Assert.That(tidied.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded), "not a 'Recovered' load");
            Assert.That(tidied.PendingCelebrationAreaIds, Is.EqualTo(new[] { "rocky_canyon" }));
            Assert.That(tidied.IsCompletionPending("green_mine"), Is.False);
        }

        [UnityTest, Order(1)]
        public IEnumerator LeavingBeforeAnsweringTheCelebration_KeepsItOwed_TheNextEntryShowsItOnce_TheAnswerEndsIt()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return StartArea(GreenPath, GreenLeft, 1);
            TreasureSpot last = Spot(GreenLeft[0]);
            yield return TapDigAndCollect(last); // the last COLLECT ...
            Assert.That(panel.IsOpen, Is.False, "... and the player leaves while the celebration is still waiting for the reward");
            Assert.That(panel.CurrentPhase, Is.EqualTo(LevelCompleteController.Phase.Waiting));
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }), "owed in the save already");
            GameProgressData collectedState = Saved(store);
            int gold = collectedState.totalGold;
            Assert.That(collectedState.collectedTreasureIds.Count, Is.EqualTo(15));
            yield return SceneManager.UnloadSceneAsync(scene); // (another scene / the Main Menu / quitting)
            scene = default;
            Assert.That(GamePause.IsPaused, Is.False, "the player was never paused");

            // 1) the next entry (a restarted app: a fresh store over the same text) shows it, once, after the scene has settled
            yield return LoadScene(GreenPath, new MemoryProgressStore(store.Json));
            float enteredAt = Time.realtimeSinceStartup;
            float openedAt = -1f;
            panel.Opened += _ => openedAt = Time.realtimeSinceStartup;
            Assert.That(panel.IsOpen, Is.False, "not at once: the player sees the place first");
            yield return WaitUntil(() => panel.IsOpen, 10f);
            Assert.That(panel.IsOpen, Is.True, "the owed celebration is shown on the next entry");
            Assert.That(openedAt - enteredAt, Is.GreaterThanOrEqualTo(LevelCompleteController.EntrySeconds - 0.15f));
            Assert.That(panel.ShowCount, Is.EqualTo(1));
            Assert.That(panel.AreaString, Is.EqualTo("GREEN MINE COMPLETED"));
            Assert.That(panel.CountString, Is.EqualTo("15 / 15 TREASURES COLLECTED"));
            Assert.That(panel.GoldString, Is.EqualTo("625 GOLD AVAILABLE IN THIS AREA"));
            Assert.That(panel.TotalString, Is.EqualTo($"YOUR TOTAL GOLD: {gold}"));
            Assert.That(panel.NextString, Is.EqualTo("GO TO ROCKY CANYON"));
            Assert.That(panel.JinglePlayCount, Is.EqualTo(1));
            Assert.That(GamePause.IsPaused, Is.True);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(panel.ShowCount, Is.EqualTo(1), "it does not trigger twice");
            Assert.That(hunt.TotalGold, Is.EqualTo(gold), "no gold was paid again");
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }), "still owed: nobody has answered it");
            Assert.That(Saved(store).celebratedAreaIds, Is.Empty);

            // 2) the app is closed with the panel open and no choice made: it is owed still, and comes back
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            GamePause.Resume(); // (a closed app starts unpaused)
            GamePause.SetModalOpen(false);
            yield return LoadScene(GreenPath, new MemoryProgressStore(store.Json));
            yield return WaitUntil(() => panel.IsOpen, 10f);
            Assert.That(panel.IsOpen, Is.True, "no choice was made, so the celebration comes back once more");
            Assert.That(panel.ShowCount, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntranceSeconds + 0.2f);
            Canvas.ForceUpdateCanvases();

            // 3) the player answers (a real tap): from now on it is over
            GameProgressData beforeAnswer = Saved(store);
            yield return ClickUntil((RectTransform)panel.StayButton.transform, () => !panel.IsOpen); // real tap
            Assert.That(panel.IsOpen, Is.False);
            AssertAnswered(beforeAnswer, "green_mine");
            Assert.That(Saved(store).totalGold, Is.EqualTo(gold));
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return LoadScene(GreenPath, new MemoryProgressStore(store.Json));
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntrySeconds + LevelCompleteController.SettleSeconds + 1.5f);
            Assert.That(panel.ShowCount, Is.EqualTo(0), "an answered celebration never comes back");
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(One<ProgressManager>().Service.RegisterCollected(last.SpotId, 10), Is.EqualTo(RegisterResult.AlreadyCollected), "and nothing pays twice");
            Assert.That(hunt.TotalGold, Is.EqualTo(gold));
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
        }

        [UnityTest, Order(1)]
        public IEnumerator ARestartedRockyCanyon_ShowsItsOwedCelebration_AndGoToAnswersIt()
        {
            yield return StartArea(RockyPath, RockyLeft, 0, extraGold: 625, pending: new[] { "rocky_canyon" });
            Assert.That(panel.Summarize().IsComplete, Is.True);
            yield return WaitUntil(() => panel.IsOpen, 10f);
            Assert.That(panel.IsOpen, Is.True);
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntranceSeconds + 0.2f);
            Canvas.ForceUpdateCanvases();
            Assert.That(panel.AreaString, Is.EqualTo("ROCKY CANYON COMPLETED"));
            Assert.That(panel.CountString, Is.EqualTo("20 / 20 TREASURES COLLECTED"));
            Assert.That(panel.GoldString, Is.EqualTo("840 GOLD AVAILABLE IN THIS AREA"));
            Assert.That(panel.NextString, Is.EqualTo("GO TO ANCIENT MINE"));
            GameProgressData before = Saved(store);
            Assert.That(before.pendingCelebrationAreaIds, Is.EqualTo(new[] { "rocky_canyon" }));

            yield return ClickUntil((RectTransform)panel.NextButton.transform, () => requestedScenes.Count > 0); // real tap
            Assert.That(requestedScenes, Is.EqualTo(new[] { SceneNavigator.AncientMineScene }));
            AssertAnswered(before, "rocky_canyon");
        }

        [UnityTest, Order(1)]
        public IEnumerator AnOwedCelebration_WaitsForItsOwnArea_AndNewGameClearsIt()
        {
            // Green Mine is owed; the player enters Rocky Canyon first: no panel there, and Green Mine's record is untouched
            yield return StartArea(RockyPath, RockyLeft, 3, extraGold: 625, pending: new[] { "green_mine" });
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntrySeconds + LevelCompleteController.SettleSeconds + 1.5f);
            Assert.That(panel.Summarize().IsComplete, Is.False);
            Assert.That(panel.ShowCount, Is.EqualTo(0), "Green Mine's celebration is not shown in Rocky Canyon");
            Assert.That(Saved(store).pendingCelebrationAreaIds, Is.EqualTo(new[] { "green_mine" }), "it stays owed");
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;

            // NEW GAME over a save that owes it: nothing is owed afterwards, and Green Mine opens quietly
            yield return EnsureTable(GreenPath);
            var owing = new MemoryProgressStore(SaveJson(Tables[GreenPath].Select(t => t.id), 625, pending: new[] { "green_mine" }));
            Assert.That(ProgressService.Create(catalog, owing).StartNewGame(), Is.True);
            Assert.That(Saved(owing).pendingCelebrationAreaIds, Is.Empty, "NEW GAME cleared what was owed");
            Assert.That(Saved(owing).celebratedAreaIds, Is.Empty);
            yield return LoadScene(GreenPath, owing);
            yield return new WaitForSecondsRealtime(LevelCompleteController.EntrySeconds + LevelCompleteController.SettleSeconds + 1.5f);
            Assert.That(panel.ShowCount, Is.EqualTo(0), "a new game shows nothing");
            Assert.That(panel.Summarize().Collected, Is.EqualTo(0));
            Assert.That(GamePause.IsPaused, Is.False);
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

        private IEnumerator LayoutOnEveryScreen(string path, string[] leftOrder, int extraGold, string expectedArea)
        {
            yield return StartArea(path, leftOrder, 1, extraGold);
            yield return CollectLastAndWaitForPanel(Spot(leftOrder[0]));
            Assert.That(panel.AreaString, Does.StartWith(expectedArea));

            gameView = new TestGameView();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(screenSide, screenBottom, Screen.width - 2f * screenSide, Screen.height - screenBottom);
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
                AssertLayout(name);
            }
        }

        private void AssertLayout(string name)
        {
            SafeAreaFitter fitter = panel.GetComponentInParent<SafeAreaFitter>();
            Rect allowed = SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), fitter.GestureMarginFraction * Screen.height);
            Rect window = ScreenRect(panel.Window);
            Rect banner = ScreenRect((RectTransform)panel.Window.Find("TitleBanner"));
            Rect whole = Rect.MinMaxRect(window.xMin, window.yMin, window.xMax, Mathf.Max(window.yMax, banner.yMax));
            Assert.That(Contains(allowed, whole), Is.True, $"{name}: the panel ({whole}) stays inside the safe area and the gesture margin ({allowed})");

            Rect next = ScreenRect((RectTransform)panel.NextButton.transform);
            Rect stay = ScreenRect((RectTransform)panel.StayButton.transform);
            Rect selection = ScreenRect((RectTransform)panel.AreaSelectionButton.transform);
            Rect picture = ScreenRect((RectTransform)panel.Picture.transform);
            float reference = Mathf.Min(Screen.height, Screen.width * 9f / 16f); // the height of the 1080-unit reference layout in pixels
            float unit = reference / 1080f;
            foreach ((string label, Rect rect, float minUnits) in new[] { ("GO TO", next, 150f), ("STAY HERE", stay, 126f), ("AREA SELECTION", selection, 126f) })
            {
                Assert.That(Contains(window, rect), Is.True, $"{name}: {label} sits inside the window");
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(minUnits * unit * 0.95f), $"{name}: {label} is at least {minUnits} reference units (48 dp) tall");
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(380f * unit), $"{name}: {label} is wide enough to hit");
            }

            Assert.That(next.Overlaps(stay) || next.Overlaps(selection) || stay.Overlaps(selection), Is.False, $"{name}: the three buttons do not overlap");
            Assert.That(next.Overlaps(picture) || stay.Overlaps(picture) || selection.Overlaps(picture), Is.False, $"{name}: the buttons do not cover the picture");
            Assert.That(Mathf.Abs(next.center.x - window.center.x), Is.LessThan(2f), $"{name}: GO TO is centred");

            foreach (TMPro.TMP_Text label in panel.PanelRoot.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                label.ForceMeshUpdate();
                RectTransform rect = label.rectTransform;
                bool isTitle = label.name == "Heading"; // the title sits on the banner, which overhangs the window's top edge
                Assert.That(Contains(isTitle ? banner : window, ScreenRect(rect)), Is.True, $"{name}: '{label.text}' is inside its {(isTitle ? "banner" : "window")}");
                Assert.That(label.textBounds.size.x, Is.LessThanOrEqualTo(rect.rect.width + 1f), $"{name}: '{label.text}' does not overflow its box sideways");
                Assert.That(label.textBounds.size.y, Is.LessThanOrEqualTo(rect.rect.height + 1f), $"{name}: '{label.text}' does not overflow its box vertically");
                if (!isTitle && label.transform.parent == panel.Window)
                {
                    Assert.That(ScreenRect(rect).Overlaps(picture), Is.False, $"{name}: '{label.text}' does not run over the picture");
                }
            }

            Assert.That(ScreenRect(panel.Trophy).center.y, Is.GreaterThan(picture.center.y), $"{name}: the trophy sits on the picture's upper corner");
        }

        [UnityTest, Order(2)]
        public IEnumerator Layout_GreenMine_FitsEveryTargetScreen_SafeAreaButtonsAndTexts()
        {
            yield return LayoutOnEveryScreen(GreenPath, GreenLeft, 0, "GREEN MINE COMPLETED");
        }

        [UnityTest, Order(2)]
        public IEnumerator Layout_RockyCanyon_FitsEveryTargetScreen_SafeAreaButtonsAndTexts()
        {
            yield return LayoutOnEveryScreen(RockyPath, RockyLeft, 625, "ROCKY CANYON COMPLETED");
        }


        // ================================================================ sound

        [UnityTest, Order(1)]
        public IEnumerator WithSfxOff_ThePanelIsSilent_AndTheMusicKeepsPlaying()
        {
            GameSettings.Current.SetSfxVolume(0f);
            yield return StartArea(GreenPath, GreenLeft, 1);
            TreasureSpot last = Spot(GreenLeft[0]);
            AmbientMusicPlayer music = One<AmbientMusicPlayer>();
            bool musicBefore = music.IsPlaying;
            yield return CollectLastAndWaitForPanel(last);
            Assert.That(panel.IsOpen, Is.True, "the panel itself is not silenced");
            Assert.That(panel.JinglePlayCount, Is.EqualTo(0), "SFX volume 0: no celebration sound");
            Assert.That(panel.LastJingleVolume, Is.EqualTo(0f));
            Assert.That(music.IsPlaying, Is.EqualTo(musicBefore), "the music is untouched");
            Assert.That(panel.GetComponent<AudioSource>(), Is.Null, "no audio source is even created");
        }

        [Test, Order(1)]
        public void TheJingle_IsShortQuietAndSilentAtBothEnds()
        {
            AudioClip clip = SoundSynth.LevelComplete();
            try
            {
                Assert.That(clip.length, Is.InRange(1.2f, 2.5f), "a short jingle");
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
    }
}
#endif
