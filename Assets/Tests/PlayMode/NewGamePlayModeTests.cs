#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
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
    /// <summary>
    /// NEW GAME (Main Menu -> Settings): the question, CANCEL, START NEW GAME, what is reset (gold, collected ids, area flags, in the SAVE and in the
    /// running service), what is kept (volumes, camera, the chosen miner, other PlayerPrefs), that it survives a reload, that the Pause Menu has no such
    /// button, and the iPhone landscape layout. Every test runs on an in-memory save that mirrors a finished Green Mine (500 gold); the developer's real
    /// save file is hashed before the first and after the last test and must be byte-for-byte the same.
    /// </summary>
    public sealed class NewGamePlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GreenMinePath = "Assets/Scenes/GreenMine.unity";
        private const string CatalogPath = "Assets/Data/Areas/AreaCatalog.asset";
        private const string SentinelKey = "ic3d.test.newgame.sentinel";

        /// <summary>The shape of a real finished save: 12 collected ids, 500 gold, Rocky Canyon unlocked, Green Mine complete.</summary>
        private const string FinishedSave =
            "{\"version\":1,\"totalGold\":500,\"collectedTreasureIds\":[\"green_mine.gold_coin_01\",\"green_mine.meadow_01\",\"green_mine.riverbank_01\"," +
            "\"green_mine.rocky_01\",\"green_mine.rocky_03\",\"green_mine.forest_01\",\"green_mine.mine_01\",\"green_mine.mine_tunnel_01\",\"green_mine.mine_room_01\"," +
            "\"green_mine.mine_room_02\",\"green_mine.rocky_04\",\"green_mine.bridge_01\"],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[\"green_mine\"]}";

        private static string realSavePath;
        private static string realSaveHashBefore;

        private Scene loadedScene;
        private MemoryProgressStore store;
        private AreaCatalog catalog;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousBehaviour;

        // ------------------------------------------------------------------ the real save must not move

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
            Assert.That(catalog, Is.Not.Null);
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(0f, 0f, Screen.width, Screen.height);
            previousBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouseNewGame");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null)
            {
                InputSystem.RemoveDevice(mouse);
                mouse = null;
            }

            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehaviour;
            SafeAreaFitter.SafeAreaOverride = null;
            PlayerPrefs.DeleteKey(SentinelKey); // only the key this test added
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            Time.timeScale = 1f;
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator LoadScene(string path, MemoryProgressStore withStore)
        {
            store = withStore;
            ProgressManager.StoreFactoryOverride = () => withStore;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, path);
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.8f); // entrance animations
        }

        private IEnumerator UnloadScene()
        {
            yield return SceneManager.UnloadSceneAsync(loadedScene);
            loadedScene = default;
            yield return null;
        }

        private T Find<T>(string name) where T : Component
        {
            foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            {
                if (item.name == name && item.gameObject.scene == loadedScene)
                {
                    return item;
                }
            }

            Assert.Fail($"{typeof(T).Name} '{name}' not found in {loadedScene.name}");
            return null;
        }

        private static AreaCardView Card(AreaSelectController screen, string areaId)
        {
            for (int i = 0; i < screen.CardCount; i++)
            {
                if (screen.GetCard(i).AreaId == areaId)
                {
                    return screen.GetCard(i);
                }
            }

            Assert.Fail("no card for " + areaId);
            return null;
        }

        private IEnumerator OpenSettings()
        {
            Find<Button>("SettingsButton").onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect CanvasRect(RectTransform rect)
        {
            var canvasTransform = (RectTransform)rect.GetComponentInParent<Canvas>().rootCanvas.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = canvasTransform.InverseTransformPoint(corners[0]);
            Vector3 max = canvasTransform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private IEnumerator Press(Vector2 position)
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

        private static Vector2 Centre(Button button) => ScreenRect((RectTransform)button.transform).center;

        private GameProgressData Saved() => JsonUtility.FromJson<GameProgressData>(store.Json);

        private static void AssertFresh(GameProgressData data, string what)
        {
            Assert.That(data.totalGold, Is.EqualTo(0), what + ": gold");
            Assert.That(data.collectedTreasureIds, Is.Empty, what + ": collected treasure ids");
            Assert.That(data.unlockedAreaIds, Is.EqualTo(new List<string> { "green_mine" }), what + ": only Green Mine is unlocked (Rocky Canyon and Ancient Mine locked again)");
            Assert.That(data.completedAreaIds, Is.Empty, what + ": no area is complete");
        }

        // ================================================================ the service (plain C#)

        private sealed class FailingStore : IProgressStore
        {
            private readonly MemoryProgressStore inner;
            public FailingStore(string json) { inner = new MemoryProgressStore(json); }
            public string Json => inner.Json;
            public bool TryRead(out string json) => inner.TryRead(out json);
            public bool Write(string json) => false;      // the disk is full / read-only
            public void Delete() { }
            public void BackupCorrupt(string json) { }
        }

        [Test]
        public void StartNewGame_WritesAFreshSave_ResetsTheService_RaisesNoAreaEvents_AndCanBeReloaded()
        {
            var memory = new MemoryProgressStore(FinishedSave);
            ProgressService service = ProgressService.Create(catalog, memory);
            Assert.That(service.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
            Assert.That(service.TotalGold, Is.EqualTo(500));
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(12));
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(service.IsAreaCompleted("green_mine"), Is.True);

            int events = 0, changed = 0;
            service.AreaCompleted += _ => events++;
            service.AreaUnlocked += _ => events++;
            service.Changed += () => changed++;

            Assert.That(service.StartNewGame(), Is.True);
            Assert.That(events, Is.EqualTo(0), "a new game announces nothing");
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.CollectedTreasureIds, Is.Empty);
            Assert.That(service.IsCollected("green_mine.meadow_01"), Is.False, "an earlier treasure is findable again");
            Assert.That(service.IsAreaUnlocked("green_mine"), Is.True);
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False, "Rocky Canyon locked again (500 gold)");
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False, "Ancient Mine locked (1200 gold)");
            Assert.That(service.IsAreaCompleted("green_mine"), Is.False);
            AssertFresh(JsonUtility.FromJson<GameProgressData>(memory.Json), "the written save");

            // "closing and reopening the game" reads the same save
            ProgressService reopened = ProgressService.Create(catalog, memory);
            Assert.That(reopened.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded), "a clean file, nothing to recover");
            Assert.That(reopened.TotalGold, Is.EqualTo(0));
            Assert.That(reopened.CollectedTreasureIds, Is.Empty);

            // and a treasure collected before pays again, once
            Assert.That(reopened.RegisterCollected("green_mine.meadow_01", 10), Is.EqualTo(RegisterResult.Accepted));
            Assert.That(reopened.RegisterCollected("green_mine.meadow_01", 10), Is.EqualTo(RegisterResult.AlreadyCollected));
            Assert.That(reopened.TotalGold, Is.EqualTo(10));
        }

        [Test]
        public void StartNewGame_WhenTheSaveCannotBeWritten_ChangesNothing()
        {
            var failing = new FailingStore(FinishedSave);
            ProgressService service = ProgressService.Create(catalog, failing);
            int changed = 0;
            service.Changed += () => changed++;
            string warning = null;
            ProgressLog.WarningSink = message => warning = message;
            try
            {
                Assert.That(service.StartNewGame(), Is.False, "the failed write is reported");
            }
            finally
            {
                ProgressLog.WarningSink = message => ProgressTestIsolation.Warnings.Add(message);
            }

            Assert.That(warning, Is.Not.Null.And.Contains("NEW GAME"));
            Assert.That(changed, Is.EqualTo(0));
            Assert.That(service.TotalGold, Is.EqualTo(500), "no half-reset game: the running state is untouched");
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(12));
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(failing.Json, Is.EqualTo(FinishedSave), "and so is the save");
        }

        // ================================================================ the Main Menu

        [UnityTest]
        public IEnumerator NewGameButton_AsksFirst_AndCancelChangesNothing()
        {
            yield return LoadScene(MainMenuPath, new MemoryProgressStore(FinishedSave));
            GameSettings.Current.SetMusicVolume(0.3f);
            yield return OpenSettings();
            var panel = Find<SettingsPanelController>("SettingsPanel");
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu.IsSettingsOpen, Is.True);
            Assert.That(panel.NewGameAvailable, Is.True, "the Main Menu's Settings has NEW GAME");
            Button newGame = panel.NewGameButton;
            Assert.That(newGame.gameObject.activeInHierarchy, Is.True);
            Assert.That(newGame.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("NEW GAME"));
            Assert.That(((RectTransform)newGame.transform).rect.height, Is.GreaterThanOrEqualTo(126f), "a comfortable touch target (48 dp)");
            Assert.That(panel.IsNewGameDialogOpen, Is.False, "the question only appears when asked for");

            string before = store.Json;
            int writes = store.WriteCount;
            newGame.onClick.Invoke();
            yield return null;
            Assert.That(panel.IsNewGameDialogOpen, Is.True);
            Assert.That(panel.NewGameDialog.activeInHierarchy, Is.True);
            Assert.That(panel.DialogTitle, Is.EqualTo("START A NEW GAME?"));
            Assert.That(panel.DialogBody, Is.EqualTo("Your gold, collected treasures and area progress will be reset. This cannot be undone."));
            Assert.That(panel.CancelNewGameButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("CANCEL"));
            Assert.That(panel.ConfirmNewGameButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("START NEW GAME"));
            Assert.That(store.Json, Is.EqualTo(before), "asking changes nothing");
            Assert.That(store.WriteCount, Is.EqualTo(writes));

            panel.CancelNewGameButton.onClick.Invoke();
            yield return null;
            Assert.That(panel.IsNewGameDialogOpen, Is.False);
            Assert.That(panel.NewGameDialog.activeSelf, Is.False);
            Assert.That(menu.IsSettingsOpen, Is.True, "CANCEL returns to Settings");
            Assert.That(store.Json, Is.EqualTo(before), "CANCEL: the save is byte-for-byte the same");
            Assert.That(store.WriteCount, Is.EqualTo(writes), "CANCEL: nothing was written");
            ProgressService service = Object.FindAnyObjectByType<ProgressManager>().Service;
            Assert.That(service.TotalGold, Is.EqualTo(500));
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(12));
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.3f).Within(1e-4f));

            // Escape / the close button with the question open only closes the question
            newGame.onClick.Invoke();
            yield return null;
            panel.Close();
            Assert.That(panel.IsNewGameDialogOpen, Is.False);
            Assert.That(menu.IsSettingsOpen, Is.True, "Back / Escape closes only the question first");
            Assert.That(store.Json, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator StartNewGame_ResetsTheRealProgress_ButKeepsSettingsMinerAndOtherPrefs_AndOnlyOnce()
        {
            PlayerPrefs.SetInt(SentinelKey, 7);
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return LoadScene(MainMenuPath, new MemoryProgressStore(FinishedSave));
            GameSettings.Current.SetMusicVolume(0.3f);
            GameSettings.Current.SetSfxVolume(0.6f);
            GameSettings.Current.SetCameraSensitivity(1.4f);
            yield return OpenSettings();
            var panel = Find<SettingsPanelController>("SettingsPanel");
            int writes = store.WriteCount;

            panel.NewGameButton.onClick.Invoke();
            yield return null;
            panel.ConfirmNewGameButton.onClick.Invoke();
            yield return null;
            Assert.That(panel.DialogState, Is.EqualTo(SettingsPanelController.NewGameState.Done));
            Assert.That(panel.DialogTitle, Is.EqualTo("NEW GAME STARTED"));
            Assert.That(panel.CancelNewGameButton.gameObject.activeSelf, Is.False, "only OK is left");
            Assert.That(panel.ConfirmNewGameButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("OK"));
            AssertFresh(Saved(), "the saved game");
            Assert.That(store.WriteCount, Is.EqualTo(writes + 1), "saved exactly once");

            ProgressService service = Object.FindAnyObjectByType<ProgressManager>().Service;
            Assert.That(service.TotalGold, Is.EqualTo(0), "the running progress is reset too, not only the file or the HUD");
            Assert.That(service.CollectedTreasureIds, Is.Empty);
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False);
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False);

            // stage 17A: the introduction opens right after the reset; skipping it opens the Character Selection (the "NEW GAME STARTED" note is closed with the Settings panel)
            var intro = Find<IntroSlidesController>("IntroPanel");
            Assert.That(intro.IsOpen, Is.True, "the introduction follows START NEW GAME");
            intro.Skip();
            yield return null;
            Assert.That(intro.IsOpen, Is.False);
            Assert.That(panel.IsOpen, Is.False);
            var characters = Find<CharacterSelectController>("CharacterPanel");
            Assert.That(characters.IsOpen, Is.True, "SKIP leads to the existing Character Selection");
            characters.Cancel();
            yield return null;

            panel.ConfirmNewGameButton.onClick.Invoke(); // OK (nothing is open any more: nothing happens)
            yield return null;
            Assert.That(panel.IsNewGameDialogOpen, Is.False);
            Assert.That(store.WriteCount, Is.EqualTo(writes + 1), "OK writes nothing");

            // personal settings and everything outside the save are untouched
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(GameSettings.Current.CameraSensitivity, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "Ruby is still the chosen miner");
            Assert.That(PlayerPrefs.GetInt(SentinelKey, -1), Is.EqualTo(7), "no PlayerPrefs were wiped");
            Assert.That(panel.MusicSlider.value, Is.EqualTo(0.3f).Within(1e-4f));

            // the Area Selection shows the new game: Green Mine 0/500, the other two locked with their gold
            panel.Close();
            yield return null;
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            AreaSelectController areas = Find<AreaSelectController>("AreaPanel");
            AreaCardView green = Card(areas, "green_mine"), rocky = Card(areas, "rocky_canyon"), ancient = Card(areas, "ancient_mine");
            Assert.That(green.State.Availability, Is.EqualTo(AreaAvailability.Playable));
            Assert.That(green.TreasuresLabel.text, Is.EqualTo("TREASURES: 0 / 15"));
            Assert.That(green.CompleteVisible, Is.False);
            Assert.That(rocky.State.Availability, Is.EqualTo(AreaAvailability.Locked));
            Assert.That(rocky.UnlockLabel.text, Is.EqualTo("UNLOCKS AT 500 TOTAL GOLD"));
            Assert.That(ancient.State.Availability, Is.EqualTo(AreaAvailability.Locked));
            Assert.That(ancient.UnlockLabel.text, Is.EqualTo("UNLOCKS AT 1200 TOTAL GOLD"));
        }

        [UnityTest]
        public IEnumerator TheNewGame_SurvivesLeavingAndReloading_AndEveryTreasureCanBeDugAgain()
        {
            yield return LoadScene(MainMenuPath, new MemoryProgressStore(FinishedSave));
            MemoryProgressStore saved = store;
            yield return OpenSettings();
            var panel = Find<SettingsPanelController>("SettingsPanel");
            panel.NewGameButton.onClick.Invoke();
            yield return null;
            panel.ConfirmNewGameButton.onClick.Invoke();
            yield return null;
            AssertFresh(Saved(), "right after START NEW GAME");

            // quit to the desktop and start the game again: the menu reads the file
            yield return UnloadScene();
            Assert.That(ProgressService.Create(catalog, saved).TotalGold, Is.EqualTo(0), "a fresh service on the same save: no 500 gold");
            yield return LoadScene(MainMenuPath, saved);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            AreaSelectController areas = Find<AreaSelectController>("AreaPanel");
            Assert.That(Card(areas, "green_mine").TreasuresLabel.text, Is.EqualTo("TREASURES: 0 / 15"));
            Assert.That(Card(areas, "rocky_canyon").State.Availability, Is.EqualTo(AreaAvailability.Locked));
            Assert.That(Card(areas, "ancient_mine").State.Availability, Is.EqualTo(AreaAvailability.Locked));
            yield return UnloadScene();

            // into Green Mine: 0 / 500 GOLD and all 15 treasures hidden again, even the ones the old save had collected
            yield return LoadScene(GreenMinePath, saved);
            var hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            Assert.That(hunt.TotalGold, Is.EqualTo(0));
            bool sawHud = false;
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text.gameObject.scene == loadedScene && text.text.EndsWith("/ 500 GOLD"))
                {
                    Assert.That(text.text, Is.EqualTo("0 / 500 GOLD"), "the HUD");
                    sawHud = true;
                }
            }

            Assert.That(sawHud, Is.True, "a gold counter exists in Green Mine");
            var spots = new List<TreasureSpot>();
            foreach (GameObject root in loadedScene.GetRootGameObjects())
            {
                spots.AddRange(root.GetComponentsInChildren<TreasureSpot>(true));
            }

            Assert.That(spots.Count, Is.EqualTo(15), "Green Mine's 15 treasures");
            foreach (TreasureSpot spot in spots)
            {
                Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden), spot.SpotId + " can be found again");
            }

            // one that the old save had collected: dig and collect it for real
            TreasureSpot again = spots.Find(s => s.SpotId == "green_mine.meadow_01");
            Assert.That(again, Is.Not.Null);
            Transform player = null;
            foreach (GameObject root in loadedScene.GetRootGameObjects())
            {
                if (root.name == "Player")
                {
                    player = root.transform;
                }
            }

            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = again.transform.position + new Vector3(0f, 1.1f, -1.5f);
            controller.enabled = true;
            yield return new WaitForSecondsRealtime(0.6f);
            Button dig = null, collect = null;
            foreach (Button b in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (b.gameObject.scene == loadedScene && b.name == "DigButton") { dig = b; }
                if (b.gameObject.scene == loadedScene && b.name == "CollectButton") { collect = b; }
            }

            Assert.That(dig.gameObject.activeInHierarchy && dig.interactable, Is.True, "DIG is offered on a treasure the old save had collected");
            dig.onClick.Invoke();
            float end = Time.realtimeSinceStartup + 3f;
            while (again.State != TreasureState.Revealed && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.2f);
            collect.onClick.Invoke();
            yield return null;
            Assert.That(again.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(again.Definition.GoldValue), "it pays again, once: the total is exactly its value");
            Assert.That(Saved().collectedTreasureIds, Is.EqualTo(new List<string> { again.SpotId }));
        }

        [UnityTest]
        public IEnumerator ThePauseMenu_HasNoNewGameButton()
        {
            yield return LoadScene(GreenMinePath, new MemoryProgressStore(FinishedSave));
            int panels = 0;
            foreach (SettingsPanelController panel in Resources.FindObjectsOfTypeAll<SettingsPanelController>())
            {
                if (panel.gameObject.scene != loadedScene)
                {
                    continue;
                }

                panels++;
                Assert.That(panel.NewGameAvailable, Is.False, "the in-game settings never offer NEW GAME");
                Assert.That(panel.NewGameButton.gameObject.activeSelf, Is.False);
                Assert.That(((RectTransform)panel.NewGameButton.transform.parent.Find("BackButton")).anchoredPosition.x, Is.EqualTo(0f), "BACK stays centred as before");
            }

            Assert.That(panels, Is.GreaterThan(0), "the Pause Menu's settings panel exists");
        }

        // ================================================================ iPhone landscape

        [UnityTest]
        public IEnumerator IphoneLandscape_NewGame_Back_AndTheQuestion_StayInsideTheSafeArea_AndWorkWithRealTaps()
        {
            // iPhone 15 Pro Max landscape: 59 pt (6.3 % of the width) free left and right, 21 pt (4.9 % of the height) at the bottom
            SafeAreaFitter.SafeAreaOverride = () => new Rect(Screen.width * 0.0633f, Screen.height * 0.0488f, Screen.width * (1f - 2f * 0.0633f), Screen.height * (1f - 0.0488f));
            yield return LoadScene(MainMenuPath, new MemoryProgressStore(FinishedSave));
            yield return OpenSettings();
            var panel = Find<SettingsPanelController>("SettingsPanel");
            Rect safe = new Rect(Screen.width * 0.0633f, Screen.height * 0.0488f, Screen.width * (1f - 2f * 0.0633f), Screen.height * (1f - 0.0488f));

            var backButton = (RectTransform)panel.transform.Find("Window/BackButton");
            Assert.That(backButton, Is.Not.Null, "the settings BACK button");
            Rect back = ScreenRect(backButton);
            Rect newGame = ScreenRect((RectTransform)panel.NewGameButton.transform);
            Assert.That(safe.Contains(back.min) && safe.Contains(back.max), Is.True, $"BACK {back} inside the safe area {safe}");
            Assert.That(safe.Contains(newGame.min) && safe.Contains(newGame.max), Is.True, $"NEW GAME {newGame} inside the safe area {safe}");
            Assert.That(back.xMax, Is.LessThan(newGame.xMin), "side by side, not overlapping");
            Rect backCanvas = CanvasRect(backButton), newGameCanvas = CanvasRect((RectTransform)panel.NewGameButton.transform);
            Assert.That(newGameCanvas.xMin - backCanvas.xMax, Is.GreaterThanOrEqualTo(20f), "a clear gap between the two buttons");
            Assert.That(newGameCanvas.height, Is.GreaterThanOrEqualTo(126f));

            // a real tap on NEW GAME opens the question
            yield return Press(Centre(panel.NewGameButton));
            Assert.That(panel.IsNewGameDialogOpen, Is.True, "a real tap on NEW GAME asks the question");
            foreach (Button b in new[] { panel.CancelNewGameButton, panel.ConfirmNewGameButton })
            {
                Rect r = ScreenRect((RectTransform)b.transform);
                Assert.That(safe.Contains(r.min) && safe.Contains(r.max), Is.True, $"{b.name} {r} inside the safe area");
            }

            Assert.That(ScreenRect((RectTransform)panel.CancelNewGameButton.transform).xMax, Is.LessThan(ScreenRect((RectTransform)panel.ConfirmNewGameButton.transform).xMin));

            // a real tap on CANCEL changes nothing
            string before = store.Json;
            yield return Press(Centre(panel.CancelNewGameButton));
            Assert.That(panel.IsNewGameDialogOpen, Is.False);
            Assert.That(store.Json, Is.EqualTo(before));

            // a real tap on NEW GAME, then on START NEW GAME, resets
            yield return Press(Centre(panel.NewGameButton));
            Assert.That(panel.IsNewGameDialogOpen, Is.True);
            yield return Press(Centre(panel.ConfirmNewGameButton));
            Assert.That(panel.DialogState, Is.EqualTo(SettingsPanelController.NewGameState.Done));
            AssertFresh(Saved(), "after the real taps");

            // stage 17A: the introduction is on top now; real taps on NEXT, NEXT and LET'S PLAY lead back to the Main Menu
            var intro = Find<IntroSlidesController>("IntroPanel");
            Assert.That(intro.IsOpen, Is.True);
            yield return Press(Centre(intro.NextButton));
            yield return Press(Centre(intro.NextButton));
            Assert.That(intro.PageIndex, Is.EqualTo(2));
            yield return Press(Centre(intro.PlayButton));
            Assert.That(intro.IsOpen, Is.False);
            Assert.That(panel.IsNewGameDialogOpen, Is.False);
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(Find<CharacterSelectController>("CharacterPanel").IsOpen, Is.True, "LET'S PLAY leads to the existing Character Selection");
        }
    }
}
#endif
