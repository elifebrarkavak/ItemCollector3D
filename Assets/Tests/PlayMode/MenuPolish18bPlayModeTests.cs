#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using ItemCollector3D.UI;
using NUnit.Framework;
using TMPro;
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
    /// <summary>The save side of the first launch (stage 18B), as plain C# over in-memory stores.</summary>
    public sealed class FirstRunServiceTests
    {
        private const string CatalogPath = "Assets/Data/Areas/AreaCatalog.asset";
        private AreaCatalog catalog;

        [SetUp]
        public void Load()
        {
            catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
        }

        [Test]
        public void HasSave_ARealFirstLaunchHasNoSave_AZeroGoldSaveIs_AndAnyWriteCreatesIt()
        {
            // no file at all: a first launch
            var empty = new MemoryProgressStore();
            ProgressService first = ProgressService.Create(catalog, empty);
            Assert.That(first.LoadStatus, Is.EqualTo(ProgressLoadStatus.NewGame));
            Assert.That(first.HasSave, Is.False, "no save file = first launch");
            Assert.That(empty.WriteCount, Is.EqualTo(0), "loading a first launch writes nothing");
            Assert.That(first.UnlockedAreaCount, Is.EqualTo(1), "Green Mine is open from the start");

            // a game in progress with 0 gold: the file exists, so it is NOT a first launch
            var zero = new MemoryProgressStore(FinalAchievementsServiceTests.Json(new string[0], 0));
            ProgressService inProgress = ProgressService.Create(catalog, zero);
            Assert.That(inProgress.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(inProgress.HasSave, Is.True, "a save with 0 gold is a saved game, not a first launch");
            Assert.That(inProgress.TotalGold, Is.EqualTo(0));
            Assert.That(inProgress.Tutorial, Is.EqualTo(TutorialStatus.NotOffered), "an existing save is never pushed into the tutorial");

            // a write creates the save
            Assert.That(first.StartNewGame(), Is.True);
            Assert.That(first.HasSave, Is.True);
            Assert.That(first.Tutorial, Is.EqualTo(TutorialStatus.Pending), "the first adventure ends in the Green Mine tutorial");
            Assert.That(ProgressService.Create(catalog, empty).HasSave, Is.True, "and the next start finds it");

            // an unreadable file is a game that existed (recovered), not a first launch
            var broken = new MemoryProgressStore("{ this is not json");
            ProgressService recovered = ProgressService.Create(catalog, broken);
            Assert.That(recovered.LoadStatus, Is.EqualTo(ProgressLoadStatus.Recovered));
            Assert.That(recovered.HasSave, Is.True);

            // erasing the save makes the next start a first launch again
            ProgressService again = ProgressService.Create(catalog, new MemoryProgressStore(FinalAchievementsServiceTests.Json(new string[0], 5)));
            again.ResetAll();
            Assert.That(again.HasSave, Is.False);
        }

        [Test]
        public void UnlockedAreaCount_FollowsTheGold_500Opens2_1200Opens3()
        {
            foreach ((int gold, int areas) in new[] { (0, 1), (499, 1), (500, 2), (1199, 2), (1200, 3), (2705, 3) })
            {
                ProgressService service = ProgressService.Create(catalog, new MemoryProgressStore(FinalAchievementsServiceTests.Json(new string[0], gold)));
                Assert.That(service.UnlockedAreaCount, Is.EqualTo(areas), $"{gold} gold");
            }

            Assert.That(MainMenuController.SummaryText(655, 2), Is.EqualTo("655 GOLD · 2 AREAS UNLOCKED"));
            Assert.That(MainMenuController.SummaryText(0, 1), Is.EqualTo("0 GOLD · 1 AREA UNLOCKED"));
            Assert.That(MainMenuController.SummaryText(2705, 3), Is.EqualTo("2705 GOLD · 3 AREAS UNLOCKED"));
        }

        [Test]
        public void OldSavesAndTheNewFields_StayAsTheyAre_17CAnd17DRecordsSurviveNewGameLogic()
        {
            // an old save (no celebration fields, tutorial never offered) loads untouched and keeps its gold, treasures and the 17C / 17D records
            var data = new GameProgressData { totalGold = 655 };
            data.collectedTreasureIds.AddRange(TreasureCatalog.IdsOf("green_mine"));
            data.unlockedAreaIds.AddRange(new[] { "green_mine", "rocky_canyon" });
            data.completedAreaIds.Add("green_mine");
            data.celebratedAreaIds.Add("green_mine");
            data.celebratedAchievementIds.Add("collection_complete");
            string json = JsonUtility.ToJson(data);
            var store = new MemoryProgressStore(json);
            ProgressService service = ProgressService.Create(catalog, store);
            Assert.That(service.HasSave, Is.True);
            Assert.That(service.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(store.WriteCount, Is.EqualTo(0), "reading never rewrites an old save");
            Assert.That(service.TotalGold, Is.EqualTo(655));
            Assert.That(service.CollectedTreasureCountIn("green_mine"), Is.EqualTo(15));
            Assert.That(service.IsCompletionCelebrated("green_mine"), Is.True, "17C record kept");
            Assert.That(service.IsAchievementCelebrated("collection_complete"), Is.False.Or.True, "17D record is whatever the save says");
            Assert.That(service.Tutorial, Is.EqualTo(TutorialStatus.NotOffered));

            // NEW GAME is the only thing that re-enables the tutorial and clears every record
            Assert.That(service.StartNewGame(), Is.True);
            Assert.That(service.Tutorial, Is.EqualTo(TutorialStatus.Pending));
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.CelebratedAreaIds, Is.Empty);
            Assert.That(service.CelebratedAchievementIds, Is.Empty);
            Assert.That(service.PendingAchievementIds, Is.Empty);
        }
    }

    /// <summary>
    /// Menu and navigation polish (stage 18B): START ADVENTURE / CONTINUE GAME, the first adventure (introduction -> miner -> Area Selection -> Green Mine tutorial), the safe NEW GAME question, the Area Selection
    /// information, the miner screen, the introduction buttons and the layout on the target screens. Saves are in memory only (the developer's real save file is hashed before and after). Real taps go through the
    /// Input System mouse; calls to <c>Play()</c>, <c>Next()</c> and so on are state checks, not touch tests, and are marked as such.
    /// </summary>
    public sealed class MenuPolish18bPlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GreenPath = "Assets/Scenes/GreenMine.unity";
        private const string CatalogPath = "Assets/Data/Areas/AreaCatalog.asset";

        private static string realSavePath, realSaveHashBefore;

        private Scene scene;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode previousBehaviour;
        private InputSettings.BackgroundBehavior previousBackground;
        private MemoryProgressStore store;
        private MemorySettingsStore settingsStore;
        private readonly List<string> requestedScenes = new();
        private TestGameView gameView;
        private float screenSide, screenBottom;
        private AreaCatalog catalog;

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
            catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            settingsStore = new MemorySettingsStore();
            GameSettings.StoreFactoryOverride = () => settingsStore;
            GameSettings.ResetCurrent();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(0f, 0f, Screen.width, Screen.height);
            previousBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; // the UI input module drops pointer events while the Editor is not the focused window
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouseMenu18b");
            keyboard = InputSystem.AddDevice<Keyboard>("IC3D_TestKeyboardMenu18b");
            requestedScenes.Clear();
            SceneNavigator.LoadHook = name => requestedScenes.Add(name);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (InputDevice device in new InputDevice[] { mouse, keyboard })
            {
                if (device != null)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            mouse = null;
            keyboard = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehaviour;
            InputSystem.settings.backgroundBehavior = previousBackground;
            SafeAreaFitter.SafeAreaOverride = null;
            SceneNavigator.LoadHook = null;
            SceneNavigator.ConsumeAreaSelectionRequest();
            GamePause.Resume();
            GamePause.SetModalOpen(false);
            if (gameView != null)
            {
                yield return gameView.Restore();
                gameView = null;
                yield return RefocusSimulator();
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
                    type.GetMethod("ShowTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(window, null);
                    ((EditorWindow)window).Focus();
                }
            }

            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------ helpers

        private static MemoryProgressStore Saved(int gold, IEnumerable<string> ids = null, bool oldFormat = false)
        {
            return new MemoryProgressStore(FinalAchievementsServiceTests.Json(ids ?? new string[0], gold, oldFormat: oldFormat));
        }

        private static GameProgressData Parse(MemoryProgressStore s) => JsonUtility.FromJson<GameProgressData>(s.Json);

        private IEnumerator LoadMenu(MemoryProgressStore withStore)
        {
            store = withStore;
            ProgressManager.StoreFactoryOverride = () => withStore;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuPath, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(MainMenuPath);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.9f); // the buttons slide in: measure them at rest
        }

        private T Find<T>(string name) where T : Component
        {
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item.name == name && item.gameObject.scene == scene)
                {
                    return item;
                }
            }

            Assert.Fail($"{typeof(T).Name} '{name}' not found");
            return null;
        }

        private T One<T>() where T : Component
        {
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item.gameObject.scene == scene)
                {
                    return item;
                }
            }

            Assert.Fail("Not found: " + typeof(T).Name);
            return null;
        }

        private MainMenuController Menu => One<MainMenuController>();
        private IntroSlidesController Intro => One<IntroSlidesController>();
        private CharacterSelectController Characters => One<CharacterSelectController>();
        private AreaSelectController Areas => One<AreaSelectController>();
        private SettingsPanelController Settings => One<SettingsPanelController>();

        private static Vector2 Centre(Component c)
        {
            var corners = new Vector3[4];
            ((RectTransform)c.transform).GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
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

        /// <summary>A real tap that is repeated (up to three times) until its effect is seen: the Editor sometimes swallows a synthetic click.</summary>
        private IEnumerator Tap(Component target, System.Func<bool> done)
        {
            for (int attempt = 0; attempt < 3 && !done(); attempt++)
            {
                if (attempt > 0)
                {
                    yield return RefocusSimulator();
                    Canvas.ForceUpdateCanvases();
                }

                yield return Click(Centre(target));
                float end = Time.realtimeSinceStartup + 1.5f;
                while (!done() && Time.realtimeSinceStartup < end)
                {
                    yield return null;
                }
            }
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

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertSameRect(Rect actual, Rect expected, string message)
        {
            Assert.That(actual.center.x, Is.EqualTo(expected.center.x).Within(3f), message + " (x)");
            Assert.That(actual.center.y, Is.EqualTo(expected.center.y).Within(3f), message + " (y)");
            // LET'S PLAY breathes (an idle pulse of up to 2.5 %), so the size may differ by that much
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(expected.width * 0.04f), message + " (width)");
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(expected.height * 0.04f), message + " (height)");
        }

        private static bool Contains(Rect outer, Rect inner, float slack = 1f)
        {
            return inner.xMin >= outer.xMin - slack && inner.xMax <= outer.xMax + slack && inner.yMin >= outer.yMin - slack && inner.yMax <= outer.yMax + slack;
        }

        // ================================================================ the Main Menu: START ADVENTURE / CONTINUE GAME

        [UnityTest, Order(1)]
        public IEnumerator FirstLaunch_ShowsStartAdventure_NoSummary_NoNewGame_AndWritesNothing()
        {
            yield return LoadMenu(new MemoryProgressStore());
            Assert.That(Menu.IsFirstRun, Is.True);
            Assert.That(Menu.PlayLabelText, Is.EqualTo("START ADVENTURE"), "a new player is not told to CONTINUE");
            Assert.That(Menu.PlaySummaryText, Is.Empty, "no progress to summarise");
            Assert.That(Menu.NewGameVisible, Is.False, "there is nothing to start over");
            Assert.That(store.WriteCount, Is.EqualTo(0), "showing the menu never creates the save");
            Assert.That(Menu.ExitButton.gameObject.activeSelf, Is.True);
        }

        [UnityTest, Order(1)]
        public IEnumerator SavedGame_ShowsContinueGame_WithTheRealGoldAndOpenAreas_AndNewGame()
        {
            foreach ((int gold, string summary) in new[]
                     {
                         (0, "0 GOLD · 1 AREA UNLOCKED"),
                         (499, "499 GOLD · 1 AREA UNLOCKED"),
                         (500, "500 GOLD · 2 AREAS UNLOCKED"),
                         (655, "655 GOLD · 2 AREAS UNLOCKED"),
                         (1200, "1200 GOLD · 3 AREAS UNLOCKED"),
                         (2705, "2705 GOLD · 3 AREAS UNLOCKED"),
                     })
            {
                yield return LoadMenu(Saved(gold));
                Assert.That(Menu.IsFirstRun, Is.False, $"{gold} gold is a saved game, never a first launch");
                Assert.That(Menu.PlayLabelText, Is.EqualTo("CONTINUE GAME"), $"{gold} gold");
                Assert.That(Menu.PlaySummaryText, Is.EqualTo(summary), "the real total gold and the open areas");
                Assert.That(Menu.NewGameVisible, Is.True);
                Assert.That(store.WriteCount, Is.EqualTo(0));
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
            }
        }

        [UnityTest, Order(1)]
        public IEnumerator Continue_OpensTheAreaSelection_WithTheSavedProgress_ByARealTap()
        {
            var ids = TreasureCatalog.IdsOf("green_mine").Take(3).Concat(TreasureCatalog.IdsOf("rocky_canyon").Take(2));
            yield return LoadMenu(Saved(655, ids));
            string before = store.Json;
            yield return Tap(Menu.PlayButton, () => Menu.IsAreaSelectOpen); // real tap on CONTINUE GAME
            Assert.That(Menu.IsAreaSelectOpen, Is.True);
            Assert.That(Menu.IsIntroOpen, Is.False, "a saved game never gets the introduction");
            Assert.That(store.Json, Is.EqualTo(before), "continuing writes nothing");
            yield return new WaitForSecondsRealtime(0.6f);
            AreaSelectController areas = Areas;
            Assert.That(areas.TotalGoldLabel.text, Is.EqualTo("TOTAL GOLD: 655"), "said once, as the gold of the whole game");
            AreaCardView green = areas.GetCard(0), rocky = areas.GetCard(1), ancient = areas.GetCard(2);
            Assert.That((green.StatusLabel.text, green.TreasuresLabel.text, green.UnlockLabel.text), Is.EqualTo(("UNLOCKED", "TREASURES: 3 / 15", "OPEN FROM THE START")));
            Assert.That((rocky.StatusLabel.text, rocky.TreasuresLabel.text, rocky.UnlockLabel.text), Is.EqualTo(("UNLOCKED", "TREASURES: 2 / 20", "UNLOCKED AT 500 TOTAL GOLD")));
            Assert.That((ancient.StatusLabel.text, ancient.TreasuresLabel.text, ancient.UnlockLabel.text), Is.EqualTo(("LOCKED", "TREASURES: 0 / 25", "UNLOCKS AT 1200 TOTAL GOLD")));
            Assert.That(areas.TryEnter("ancient_mine"), Is.EqualTo(AreaEnterResult.Locked), "1200 still keeps Ancient Mine closed at 655");
            Assert.That(areas.GetState("rocky_canyon").Availability, Is.EqualTo(AreaAvailability.Playable), "500 opened Rocky Canyon");
            Assert.That(requestedScenes, Is.Empty);
        }

        // ================================================================ the first adventure

        [UnityTest, Order(1)]
        public IEnumerator FirstAdventure_StartAdventure_Intro_Character_AreaSelection_GreenMine_Tutorial_ByRealTaps()
        {
            yield return LoadMenu(new MemoryProgressStore());
            Assert.That(Menu.PlayLabelText, Is.EqualTo("START ADVENTURE"));

            // START ADVENTURE (real tap) -> the three introduction pages; nothing is saved yet
            yield return Tap(Menu.PlayButton, () => Menu.IsIntroOpen);
            Assert.That(Menu.IsIntroOpen, Is.True, "START ADVENTURE opens the introduction");
            Assert.That(Intro.PageIndex, Is.EqualTo(0));
            Assert.That(store.WriteCount, Is.EqualTo(0), "the save is created when the introduction ends, not when it starts");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Tap(Intro.NextButton, () => Intro.PageIndex == 1); // real tap
            yield return Tap(Intro.NextButton, () => Intro.PageIndex == 2); // real tap
            Assert.That(Intro.PageIndex, Is.EqualTo(2));
            Assert.That(store.WriteCount, Is.EqualTo(0));

            // LET'S PLAY (real tap) -> the save is created (a fresh game, tutorial pending) -> the miner selection
            yield return Tap(Intro.PlayButton, () => Menu.IsCharacterSelectOpen);
            Assert.That(Menu.IsCharacterSelectOpen, Is.True, "the introduction leads to the Character Selection");
            Assert.That(Menu.IsIntroOpen, Is.False);
            Assert.That(store.WriteCount, Is.EqualTo(1), "one write: the new adventure");
            GameProgressData saved = Parse(store);
            Assert.That(saved.tutorialState, Is.EqualTo((int)TutorialStatus.Pending), "the Green Mine tutorial is waiting");
            Assert.That(saved.totalGold, Is.EqualTo(0));
            Assert.That(saved.unlockedAreaIds, Is.EqualTo(new[] { "green_mine" }));

            // SELECT (real tap) -> the Area Selection
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Characters.SelectButton, () => Menu.IsAreaSelectOpen);
            Assert.That(Menu.IsAreaSelectOpen, Is.True, "choosing the miner leads to the Area Selection");
            Assert.That(Menu.IsCharacterSelectOpen, Is.False);
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.MaleId), "the miner choice is kept");
            Assert.That(requestedScenes, Is.Empty);

            // GREEN MINE (real tap on its PLAY)
            yield return new WaitForSecondsRealtime(0.7f);
            AreaCardView green = Areas.GetCard(0);
            yield return Tap(green.PlayButton, () => requestedScenes.Count > 0);
            Assert.That(requestedScenes, Is.EqualTo(new[] { SceneNavigator.GreenMineScene }));
            Assert.That(Parse(store).tutorialState, Is.EqualTo((int)TutorialStatus.Pending), "entering the map does not use up the tutorial");

            // the first entry of Green Mine: the five step tutorial is offered
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore(store.Json); // a restarted app: a fresh store over the same text
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GreenPath, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(GreenPath);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
            TutorialController tutorial = One<TutorialController>();
            Assert.That(tutorial.IsRunning, Is.True, "the tutorial starts on the first entry of Green Mine");
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Move), "step 1 of 5");
        }

        [UnityTest, Order(1)]
        public IEnumerator FirstAdventure_Skip_EndsTheIntro_StillLeadsToTheMiner_AndDoesNotSkipTheTutorial()
        {
            yield return LoadMenu(new MemoryProgressStore());
            Menu.Play(); // state check: START ADVENTURE
            yield return null;
            Assert.That(Menu.IsIntroOpen, Is.True);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Tap(Intro.SkipButton, () => Menu.IsCharacterSelectOpen); // real tap on SKIP
            Assert.That(Menu.IsCharacterSelectOpen, Is.True, "SKIP ends the introduction and goes on to the miner");
            Assert.That(Parse(store).tutorialState, Is.EqualTo((int)TutorialStatus.Pending), "SKIP never skips the tutorial");
            Assert.That(store.WriteCount, Is.EqualTo(1));
        }

        [UnityTest, Order(1)]
        public IEnumerator FirstAdventure_ClosingTheIntroHalfWay_KeepsAFirstLaunchAFirstLaunch_AndTheIntroStartsAgain()
        {
            yield return LoadMenu(new MemoryProgressStore());
            Menu.Play(); // state check
            yield return null;
            Intro.Next();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Intro.PageIndex, Is.EqualTo(1));
            yield return PressBack(); // the Android Back key
            Assert.That(Menu.IsIntroOpen, Is.False, "Back closes the introduction");
            Assert.That(store.WriteCount, Is.EqualTo(0), "nothing was saved: it is still a first launch");
            Assert.That(Menu.PlayLabelText, Is.EqualTo("START ADVENTURE"));
            Assert.That(Menu.IsFirstRun, Is.True);

            // the app is closed in the middle too: the next start is a first launch with the introduction from page 1
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return LoadMenu(new MemoryProgressStore(store.Json));
            Assert.That(Menu.PlayLabelText, Is.EqualTo("START ADVENTURE"));
            Menu.Play();
            yield return null;
            Assert.That(Menu.IsIntroOpen, Is.True);
            Assert.That(Intro.PageIndex, Is.EqualTo(0), "the introduction starts from the first page again");
        }

        [UnityTest, Order(1)]
        public IEnumerator FirstAdventure_BackOnTheMinerScreen_ReturnsToTheMenu_AsAContinueGame_NotToTheAreas()
        {
            yield return LoadMenu(new MemoryProgressStore());
            Menu.Play(); // state check
            yield return null;
            Intro.Play(); // state check: LET'S PLAY
            yield return null;
            Assert.That(Menu.IsCharacterSelectOpen, Is.True);
            Characters.Cancel(); // BACK
            yield return null;
            Assert.That(Menu.IsAreaSelectOpen, Is.False, "BACK does not go on");
            Assert.That(Menu.IsCharacterSelectOpen, Is.False);
            Assert.That(Menu.PlayLabelText, Is.EqualTo("CONTINUE GAME"), "the adventure was started: the menu now continues it");
            Assert.That(Menu.PlaySummaryText, Is.EqualTo("0 GOLD · 1 AREA UNLOCKED"));

            // the CHARACTER button of the menu still just returns to the menu after SELECT
            Menu.OpenCharacterSelect();
            yield return null;
            Characters.Confirm();
            yield return null;
            Assert.That(Menu.IsAreaSelectOpen, Is.False, "only the miner screen that follows the introduction leads on to the Area Selection");
        }

        [UnityTest, Order(1)]
        public IEnumerator ExistingSaves_NeverGetTheIntroOrTheTutorial_NotEvenWithZeroGold_OrInTheOldFormat()
        {
            foreach (bool oldFormat in new[] { false, true })
            {
                MemoryProgressStore saved = Saved(0, oldFormat: oldFormat);
                string before = saved.Json;
                yield return LoadMenu(saved);
                Assert.That(Menu.IsFirstRun, Is.False, $"old format {oldFormat}");
                Assert.That(Menu.PlayLabelText, Is.EqualTo("CONTINUE GAME"));
                yield return Tap(Menu.PlayButton, () => Menu.IsAreaSelectOpen || Menu.IsIntroOpen); // real tap
                Assert.That(Menu.IsAreaSelectOpen, Is.True, "straight to the Area Selection");
                Assert.That(Menu.IsIntroOpen, Is.False);
                Assert.That(saved.Json, Is.EqualTo(before), "the save is not rewritten, tutorial state untouched");
                Assert.That(saved.WriteCount, Is.EqualTo(0));
                Assert.That(ProgressService.Create(catalog, saved).Tutorial, Is.EqualTo(TutorialStatus.NotOffered));
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
            }
        }

        // ================================================================ NEW GAME: the safe question

        [UnityTest, Order(1)]
        public IEnumerator NewGame_FromTheMainMenu_OnlyAsksFirst_CancelBackAndOutsideTouchesChangeNothing_OnlyTheConfirmResets()
        {
            var ids = TreasureCatalog.IdsOf("green_mine").Take(4);
            yield return LoadMenu(Saved(655, ids));
            string before = store.Json;
            int writes = store.WriteCount;

            // a real tap on NEW GAME: the question opens, nothing is reset, the settings window is not on screen
            yield return Tap(Menu.NewGameButton, () => Settings.IsNewGameDialogOpen);
            Assert.That(Settings.IsNewGameDialogOpen, Is.True, "NEW GAME asks first");
            Assert.That(Settings.DialogTitle, Is.EqualTo("START A NEW GAME?"));
            Assert.That(Settings.DialogBody, Does.Contain("cannot be undone"), "the loss is spelled out");
            Assert.That(Settings.transform.Find("Window").gameObject.activeSelf, Is.False, "nothing of the settings window shows behind the question");
            Assert.That(store.Json, Is.EqualTo(before));
            Assert.That(store.WriteCount, Is.EqualTo(writes));

            // CANCEL is the big calm button, START NEW GAME the smaller red one
            Assert.That(ScreenRect((RectTransform)Settings.CancelNewGameButton.transform).width, Is.GreaterThan(ScreenRect((RectTransform)Settings.ConfirmNewGameButton.transform).width), "CANCEL is the larger button");
            Assert.That(Settings.ConfirmNewGameButton.GetComponent<Image>() ?? Settings.ConfirmNewGameButton.GetComponentInChildren<Image>(), Is.Not.Null);

            // a touch on the dimmed area around the question does nothing (it never confirms)
            var results = new List<RaycastResult>();
            Vector2 outside = new Vector2(Screen.width * 0.04f, Screen.height * 0.5f);
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = outside }, results);
            Assert.That(results, Is.Not.Empty, "the dim takes the touch");
            Assert.That(results[0].gameObject.GetComponentInParent<Button>(), Is.Null, "no button under an outside touch");
            yield return Click(outside);
            Assert.That(Settings.IsNewGameDialogOpen, Is.True, "an outside touch neither confirms nor breaks anything");
            Assert.That(store.Json, Is.EqualTo(before));

            // the Android Back key closes the question and leaves the save alone
            yield return PressBack();
            Assert.That(Settings.IsNewGameDialogOpen, Is.False);
            Assert.That(Menu.IsSettingsOpen, Is.False, "straight back to the Main Menu (no settings window was behind it)");
            Assert.That(store.Json, Is.EqualTo(before), "Back never confirms");
            Assert.That(store.WriteCount, Is.EqualTo(writes));

            // CANCEL (real tap) does the same
            yield return Tap(Menu.NewGameButton, () => Settings.IsNewGameDialogOpen);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Tap(Settings.CancelNewGameButton, () => !Settings.IsNewGameDialogOpen);
            Assert.That(Settings.IsNewGameDialogOpen, Is.False);
            Assert.That(store.Json, Is.EqualTo(before), "CANCEL: the real save is byte for byte the same");
            Assert.That(Menu.PlayLabelText, Is.EqualTo("CONTINUE GAME"));
            Assert.That(Menu.PlaySummaryText, Is.EqualTo("655 GOLD · 2 AREAS UNLOCKED"), "the progress is still there");

            // only START NEW GAME (real tap) resets: one write, the tutorial is pending again, the introduction opens
            yield return Tap(Menu.NewGameButton, () => Settings.IsNewGameDialogOpen);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Tap(Settings.ConfirmNewGameButton, () => Menu.IsIntroOpen);
            Assert.That(store.WriteCount, Is.EqualTo(writes + 1), "saved exactly once");
            GameProgressData fresh = Parse(store);
            Assert.That(fresh.totalGold, Is.EqualTo(0));
            Assert.That(fresh.collectedTreasureIds, Is.Empty);
            Assert.That(fresh.tutorialState, Is.EqualTo((int)TutorialStatus.Pending), "NEW GAME re-enables the tutorial");
            Assert.That(fresh.celebratedAchievementIds, Is.Empty, "17D records are reset too");
            Assert.That(fresh.celebratedAreaIds, Is.Empty, "17C records are reset too");
            Assert.That(Menu.IsIntroOpen, Is.True, "NEW GAME re-enables the introduction");
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.MaleId), "the miner is not touched");
        }

        [UnityTest, Order(1)]
        public IEnumerator NewGame_ThroughSettings_StillWorks_AndKeepsTheMusicAndSfxSettings()
        {
            GameSettings.Current.SetMusicVolume(0.3f);
            GameSettings.Current.SetSfxVolume(0.6f);
            yield return LoadMenu(Saved(100));
            Menu.OpenSettings();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(Settings.NewGameButton.gameObject.activeInHierarchy, Is.True, "the old way through Settings is still there");
            Settings.NewGameButton.onClick.Invoke(); // state check
            yield return null;
            Assert.That(Settings.IsNewGameDialogOpen, Is.True);
            Assert.That(Settings.transform.Find("Window").gameObject.activeSelf, Is.False, "the window is hidden behind the question here too");
            Settings.CancelNewGame();
            yield return null;
            Assert.That(Settings.transform.Find("Window").gameObject.activeSelf, Is.True, "CANCEL brings the settings window back");
            Assert.That(Menu.IsSettingsOpen, Is.True, "and stays in Settings");
            Assert.That(Settings.MusicSlider.value, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(Settings.SfxSlider.value, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.3f).Within(1e-4f), "Music / SFX settings are kept");
        }

        // ================================================================ the miner screen

        [UnityTest, Order(1)]
        public IEnumerator CharacterSelection_SelectedIsGreenAndTicked_SelectIsGold_TheChoiceIsKept_ArrowsWork_ByRealTaps()
        {
            yield return LoadMenu(Saved(655));
            Menu.OpenCharacterSelect();
            yield return new WaitForSecondsRealtime(0.8f);
            CharacterSelectController screen = Characters;
            Assert.That(screen.ViewingId, Is.EqualTo(CharacterSelection.MaleId), "Barnaby is the saved miner");
            Assert.That(screen.SelectLabelText, Is.EqualTo("SELECTED"));
            Assert.That(screen.SelectedMarkVisible, Is.True, "ticked");
            Assert.That(screen.SelectedLookShown, Is.True, "and green: the state does not rest on a small check mark alone");

            // the right arrow (real tap) shows Ruby: SELECT, gold, no tick
            Button right = Find<Button>("RightArrow");
            yield return Tap(right, () => screen.ViewingId == CharacterSelection.FemaleId);
            Assert.That(screen.ViewingId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(screen.SelectLabelText, Is.EqualTo("SELECT"));
            Assert.That(screen.SelectedMarkVisible, Is.False);
            Assert.That(screen.SelectedLookShown, Is.False, "the gold SELECT look");
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.MaleId), "browsing does not save");

            // the left arrow goes back; SELECT on Ruby (real tap) saves her and closes (not on to the areas: the menu's own CHARACTER button)
            Button left = Find<Button>("LeftArrow");
            yield return Tap(left, () => screen.ViewingId == CharacterSelection.MaleId);
            Assert.That(screen.ViewingId, Is.EqualTo(CharacterSelection.MaleId));
            yield return Tap(right, () => screen.ViewingId == CharacterSelection.FemaleId);
            yield return Tap(screen.SelectButton, () => !screen.IsOpen);
            Assert.That(screen.IsOpen, Is.False);
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "Ruby is saved");
            Assert.That(screen.ClosedByConfirm, Is.True);
            Assert.That(Menu.IsAreaSelectOpen, Is.False);

            // reopened: Ruby is the SELECTED one now
            Menu.OpenCharacterSelect();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(screen.ViewingId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(screen.SelectLabelText, Is.EqualTo("SELECTED"));
            Assert.That(screen.SelectedLookShown, Is.True);
        }

        // ================================================================ the Area Selection

        [UnityTest, Order(1)]
        public IEnumerator AreaSelection_ThreeCardsOneSizeOneRow_TheFocusedOneIsOutlined_TextsFitInside()
        {
            yield return LoadMenu(Saved(655, TreasureCatalog.IdsOf("green_mine").Take(15)));
            Menu.OpenAreaSelect();
            yield return new WaitForSecondsRealtime(1.2f);
            AreaSelectController areas = Areas;
            var frames = new List<Rect>();
            for (int i = 0; i < areas.CardCount; i++)
            {
                frames.Add(ScreenRect((RectTransform)areas.GetCard(i).transform));
            }

            for (int i = 1; i < frames.Count; i++)
            {
                Assert.That(frames[i].width, Is.EqualTo(frames[0].width).Within(1.5f), "the three cards have one width");
                Assert.That(frames[i].height, Is.EqualTo(frames[0].height).Within(1.5f), "... and one height");
                Assert.That(frames[i].yMax, Is.EqualTo(frames[0].yMax).Within(1.5f), "... and stand in one row");
            }

            Assert.That(areas.GetCard(0).IsFocused, Is.True, "the first playable card is the focused one");
            Assert.That(areas.GetCard(0).RingVisible, Is.True, "outlined: a shape, not only a colour");
            Assert.That(areas.GetCard(1).RingVisible, Is.False);
            Assert.That(areas.GetCard(2).RingVisible, Is.False);
            Assert.That(areas.GetCard(2).StatusLockVisible, Is.True, "LOCKED has a padlock, UNLOCKED a check mark");
            Assert.That(areas.GetCard(0).StatusCheckVisible, Is.True);

            for (int i = 0; i < areas.CardCount; i++)
            {
                AreaCardView card = areas.GetCard(i);
                foreach (TMP_Text label in new[] { card.StatusLabel, card.TreasuresLabel, card.UnlockLabel })
                {
                    label.ForceMeshUpdate();
                    Rect cardRect = frames[i];
                    Assert.That(Contains(cardRect, ScreenRect(label.rectTransform)), Is.True, $"{card.AreaId}: '{label.text}' is inside its card");
                    Assert.That(label.textBounds.size.x, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1f), $"{card.AreaId}: '{label.text}' does not overflow sideways");
                }
            }
        }

        // ================================================================ the introduction buttons

        [UnityTest, Order(1)]
        public IEnumerator Intro_BackNextSkipLetsPlay_KeepOnePlaceOnEveryPage_AndWork()
        {
            yield return LoadMenu(new MemoryProgressStore());
            Menu.Play(); // state check
            yield return new WaitForSecondsRealtime(0.9f);
            IntroSlidesController intro = Intro;
            Rect Rc(Button b) => ScreenRect((RectTransform)b.transform);

            // page 1: SKIP and NEXT, no BACK
            Assert.That(intro.BackButton.gameObject.activeSelf, Is.False);
            Assert.That(intro.SkipButton.gameObject.activeSelf && intro.NextButton.gameObject.activeSelf, Is.True);
            Assert.That(intro.PlayButton.gameObject.activeSelf, Is.False);
            Rect skip1 = Rc(intro.SkipButton), next1 = Rc(intro.NextButton);
            Assert.That(skip1.center.y, Is.GreaterThan(Screen.height * 0.8f), "SKIP is in the top corner");
            Assert.That(skip1.center.x, Is.GreaterThan(Screen.width * 0.7f), "... on the right");

            // page 2 (real tap on NEXT): BACK appears, SKIP and NEXT do not move
            yield return Tap(intro.NextButton, () => intro.PageIndex == 1);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(intro.PageIndex, Is.EqualTo(1));
            Rect back2 = Rc(intro.BackButton);
            AssertSameRect(Rc(intro.SkipButton), skip1, "SKIP stays where it was");
            AssertSameRect(Rc(intro.NextButton), next1, "NEXT stays where it was");
            Assert.That(back2.center.x, Is.LessThan(next1.center.x), "BACK is at the left, NEXT at the right");

            // page 3 (real tap): LET'S PLAY takes exactly the place and size of NEXT, BACK stays, SKIP is gone
            yield return Tap(intro.NextButton, () => intro.PageIndex == 2);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(intro.PageIndex, Is.EqualTo(2));
            Assert.That(intro.NextButton.gameObject.activeSelf, Is.False);
            AssertSameRect(Rc(intro.PlayButton), next1, "LET'S PLAY has NEXT's size and place");
            AssertSameRect(Rc(intro.BackButton), back2, "BACK stays where it was");
            Assert.That(intro.SkipButton.gameObject.activeSelf, Is.False, "nothing to skip on the last page");

            // BACK (real tap) goes back one page
            yield return Tap(intro.BackButton, () => intro.PageIndex == 1);
            Assert.That(intro.PageIndex, Is.EqualTo(1));
        }

        // ================================================================ layout on the target screens

        private static readonly (string name, int width, int height, float side, float bottom)[] Screens =
        {
            ("iPhone 15 Pro Max", 2796, 1290, 59f * 3f, 21f * 3f),
            ("4:3", 1600, 1200, 0f, 0f),
            ("16:9", 1920, 1080, 0f, 0f),
            ("19.5:9", 2340, 1080, 0f, 0f),
            ("20:9", 2400, 1080, 0f, 0f),
            ("21:9", 2520, 1080, 0f, 0f),
        };

        private IEnumerator SetScreen((string name, int width, int height, float side, float bottom) screen)
        {
            screenSide = screen.side;
            screenBottom = screen.bottom;
            gameView.SetResolution(screen.width, screen.height);
            float end = Time.realtimeSinceStartup + 3f;
            while ((Screen.width != screen.width || Screen.height != screen.height) && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(screen.width, screen.height)), screen.name + ": the Game View has the screen size");
            yield return null;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private void AssertTextsFit(string where, Transform root)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(false))
            {
                label.ForceMeshUpdate();
                RectTransform rect = label.rectTransform;
                Assert.That(label.textBounds.size.x, Is.LessThanOrEqualTo(rect.rect.width + 1.5f), $"{where}: '{label.text}' does not overflow its box sideways");
                Assert.That(label.textBounds.size.y, Is.LessThanOrEqualTo(rect.rect.height + 1.5f), $"{where}: '{label.text}' does not overflow its box vertically");
            }
        }

        private void AssertButtonsInside(string where, Rect safe, float minUnits, params Button[] buttons)
        {
            float reference = Mathf.Min(Screen.height, Screen.width * 9f / 16f);
            float unit = reference / 1080f;
            foreach (Button b in buttons)
            {
                Rect r = ScreenRect((RectTransform)b.transform);
                Assert.That(Contains(safe, r, 2f), Is.True, $"{where}: {b.name} {r} is inside the safe area {safe}");
                Assert.That(Mathf.Min(r.width, r.height), Is.GreaterThanOrEqualTo(minUnits * unit * 0.9f), $"{where}: {b.name} is a comfortable touch target");
            }
        }

        [UnityTest, Order(2)]
        public IEnumerator Layout_MainMenu_Continue_NewGame_Question_Intro_Character_AreaCards_FitEveryTargetScreen()
        {
            yield return LoadMenu(Saved(655, TreasureCatalog.IdsOf("green_mine").Take(6)));
            gameView = new TestGameView();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(screenSide, screenBottom, Screen.width - 2f * screenSide, Screen.height - screenBottom);
            foreach (var screen in Screens)
            {
                yield return SetScreen(screen);
                Rect safe = SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), 0.055f * Screen.height);

                // Main Menu (CONTINUE GAME state): the five buttons are inside, big enough and not overlapping; the texts fit
                Menu.RefreshHome();
                yield return new WaitForSecondsRealtime(0.9f); // the buttons slide in again whenever the menu comes back: measure them at rest
                Canvas.ForceUpdateCanvases();
                AssertButtonsInside(screen.name + " / Main Menu", safe, 126f, Menu.PlayButton, Menu.ExitButton, Menu.NewGameButton, Find<Button>("CharacterButton"), Find<Button>("SettingsButton"));
                Rect exit = ScreenRect((RectTransform)Menu.ExitButton.transform), newGame = ScreenRect((RectTransform)Menu.NewGameButton.transform);
                Assert.That(exit.Overlaps(newGame), Is.False, screen.name + ": EXIT and NEW GAME do not overlap");
                Assert.That(Mathf.Abs(exit.center.y - newGame.center.y), Is.LessThan(2f), screen.name + ": they share one row");
                Rect settingsRect = ScreenRect((RectTransform)Find<Button>("SettingsButton").transform);
                Assert.That(settingsRect.yMin, Is.GreaterThanOrEqualTo(exit.yMax - 1f), screen.name + ": the bottom row sits under SETTINGS");
                AssertTextsFit(screen.name + " / Main Menu", Menu.PlayButton.transform);
                AssertTextsFit(screen.name + " / Main Menu", Menu.ExitButton.transform);
                AssertTextsFit(screen.name + " / Main Menu", Menu.NewGameButton.transform);

                // the NEW GAME question
                Menu.OpenNewGameQuestion();
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(Settings.IsNewGameDialogOpen, Is.True);
                AssertButtonsInside(screen.name + " / question", safe, 126f, Settings.CancelNewGameButton, Settings.ConfirmNewGameButton);
                Assert.That(ScreenRect((RectTransform)Settings.CancelNewGameButton.transform).xMax, Is.LessThan(ScreenRect((RectTransform)Settings.ConfirmNewGameButton.transform).xMin), screen.name + ": side by side");
                AssertTextsFit(screen.name + " / question", Settings.NewGameDialog.transform);
                Settings.CancelNewGame();
                yield return null;

                // the miner screen
                Menu.OpenCharacterSelect();
                yield return new WaitForSecondsRealtime(0.45f);
                Canvas.ForceUpdateCanvases();
                AssertButtonsInside(screen.name + " / miner", safe, 126f, Characters.SelectButton, Find<Button>("LeftArrow"), Find<Button>("RightArrow"), Find<Button>("CharacterBackButton"));
                AssertTextsFit(screen.name + " / miner", Characters.transform);
                Characters.Cancel();
                yield return null;

                // the Area Selection
                Menu.OpenAreaSelect();
                yield return new WaitForSecondsRealtime(0.9f);
                Canvas.ForceUpdateCanvases();
                AreaSelectController areas = Areas;
                var buttons = new List<Button> { areas.BackButton };
                for (int i = 0; i < areas.CardCount; i++)
                {
                    AreaCardView card = areas.GetCard(i);
                    buttons.Add(new[] { card.PlayButton, card.ComingSoonButton, card.LockedButton }.First(b => b.gameObject.activeSelf));
                }

                AssertButtonsInside(screen.name + " / areas", safe, 126f, buttons.ToArray());
                Rect gold = ScreenRect((RectTransform)areas.TotalGoldLabel.transform);
                Assert.That(Contains(safe, gold, 2f), Is.True, screen.name + ": the total gold chip is inside the safe area");
                Rect banner = ScreenRect((RectTransform)areas.transform.Find("TitleBanner"));
                Assert.That(banner.Overlaps(ScreenRect((RectTransform)areas.TotalGoldLabel.transform.parent)), Is.False, screen.name + ": the SELECT AREA banner and the total gold chip do not overlap");
                float cardsTop = float.MinValue, cardsBottom = float.MaxValue;
                for (int i = 0; i < areas.CardCount; i++)
                {
                    Rect r = ScreenRect((RectTransform)areas.GetCard(i).transform);
                    cardsTop = Mathf.Max(cardsTop, r.yMax);
                    cardsBottom = Mathf.Min(cardsBottom, r.yMin);
                    Assert.That(Contains(safe, r, 2f), Is.True, $"{screen.name}: card {i} is inside the safe area");
                }

                AssertTextsFit(screen.name + " / areas", areas.transform);
                Assert.That(cardsBottom - safe.yMin, Is.LessThan(Screen.height * 0.42f), screen.name + ": the cards are not pushed to the top with a big hole under them");
                areas.Cancel();
                yield return null;
            }
        }

        [UnityTest, Order(2)]
        public IEnumerator Layout_Intro_FitsEveryTargetScreen_NavigationInsideTheSafeArea()
        {
            yield return LoadMenu(new MemoryProgressStore());
            gameView = new TestGameView();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(screenSide, screenBottom, Screen.width - 2f * screenSide, Screen.height - screenBottom);
            Menu.Play(); // state check
            foreach (var screen in Screens)
            {
                yield return SetScreen(screen);
                Rect safe = SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), 0.055f * Screen.height);
                for (int page = 0; page < 3; page++)
                {
                    while (Intro.PageIndex < page) { Intro.Next(); }
                    while (Intro.PageIndex > page) { Intro.Back(); }
                    yield return new WaitForSecondsRealtime(0.4f);
                    Canvas.ForceUpdateCanvases();
                    var shown = new List<Button> { Intro.BackButton, Intro.SkipButton, Intro.NextButton, Intro.PlayButton }.Where(b => b.gameObject.activeSelf).ToList();
                    AssertButtonsInside($"{screen.name} / intro page {page + 1}", safe, 108f, shown.ToArray());
                    AssertTextsFit($"{screen.name} / intro page {page + 1}", Intro.transform);
                    for (int i = 0; i < shown.Count; i++)
                    {
                        for (int j = i + 1; j < shown.Count; j++)
                        {
                            Assert.That(ScreenRect((RectTransform)shown[i].transform).Overlaps(ScreenRect((RectTransform)shown[j].transform)), Is.False, $"{screen.name} / page {page + 1}: {shown[i].name} and {shown[j].name} do not overlap");
                        }
                    }
                }
            }
        }
    }
}
#endif
