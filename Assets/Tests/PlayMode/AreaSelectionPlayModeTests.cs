#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>
    /// Area Selection (Main Menu -> PLAY): the flow (PLAY opens it, BACK returns, GREEN MINE enters the gameplay scene), the three card states
    /// (open / locked / COMING SOON) driven only by the saved total gold (thresholds 500 and 1200 from the AreaCatalog), that a locked or map-less
    /// area can never be entered, that the progress is right every time the screen opens, and that it fits every target phone screen.
    /// </summary>
    public sealed class AreaSelectionPlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string CatalogPath = "Assets/Data/Areas/AreaCatalog.asset";
        private const float MinTouchUnits = 126f;   // 48 dp on a 1080 px high, 2.625 px per dp phone
        private const float EdgeMargin = 24f;

        private static readonly (string name, int width, int height)[] TargetScreens =
        {
            ("16:9", 1920, 1080), ("18:9", 2160, 1080), ("19.5:9", 2340, 1080), ("20:9", 2400, 1080), ("21:9", 2520, 1080), ("2.6:1", 2808, 1080),
        };

        private Scene loadedScene;
        private TestGameView gameView;
        private MemoryProgressStore store;
        private AreaCatalog catalog;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, "AreaCatalog asset");
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore(); // never touch the real PlayerPrefs
            CharacterSelection.ResetCurrent();
            // a phone without a cut-out: the safe area is the whole screen (the Editor's own Screen.safeArea can lag behind the Game View size)
            SafeAreaFitter.SafeAreaOverride = () => new Rect(0f, 0f, Screen.width, Screen.height);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SafeAreaFitter.SafeAreaOverride = null;
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            if (gameView != null)
            {
                yield return gameView.Restore(); // also when an assertion failed
                gameView = null;
            }

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

        /// <summary>Loads the Main Menu with a saved game that has <paramref name="totalGold"/> total gold (in memory: the real save is never touched).</summary>
        private IEnumerator LoadMenu(int totalGold)
        {
            store = new MemoryProgressStore();
            ProgressService seed = ProgressService.Create(catalog, store);
            if (totalGold > 0)
            {
                seed.RegisterCollected("seed_treasure", totalGold);
            }
            else
            {
                seed.Save(); // a game in progress with 0 gold is a saved game too (not a first launch): PLAY continues it
            }

            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuPath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, MainMenuPath);
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(MainMenuPath);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
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

        private IEnumerator OpenAreaSelection(int totalGold, Action<AreaSelectController, MainMenuController> then = null)
        {
            yield return LoadMenu(totalGold);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            then?.Invoke(Find<AreaSelectController>("AreaPanel"), UnityEngine.Object.FindAnyObjectByType<MainMenuController>());
        }

        private static Button VisibleButton(AreaCardView card)
        {
            foreach (Button b in new[] { card.PlayButton, card.ComingSoonButton, card.LockedButton })
            {
                if (b != null && b.gameObject.activeSelf)
                {
                    return b;
                }
            }

            Assert.Fail("no action button is shown on " + card.AreaId);
            return null;
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

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Contains(Rect outer, Rect inner, float margin = 0f)
        {
            return inner.xMin >= outer.xMin + margin && inner.xMax <= outer.xMax - margin && inner.yMin >= outer.yMin + margin && inner.yMax <= outer.yMax - margin;
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax - 0.5f && a.xMax > b.xMin + 0.5f && a.yMin < b.yMax - 0.5f && a.yMax > b.yMin + 0.5f;
        }

        private IEnumerator OnEveryTargetScreen(Action check)
        {
            gameView ??= new TestGameView(); // restored by TearDown
            foreach ((string name, int width, int height) screen in TargetScreens)
            {
                gameView.SetResolution(screen.width, screen.height);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(screen.width, screen.height)),
                    $"the Game View did not switch to {screen.name}; the check would measure the wrong screen");
                try
                {
                    check();
                }
                catch (AssertionException e)
                {
                    throw new AssertionException($"On the {screen.name} screen ({screen.width}x{screen.height}): {e.Message}", e);
                }
            }
        }

        // ------------------------------------------------------------------ the pure state rules (no scene)

        [Test]
        public void Thresholds_500_And_1200_DecideWhichAreasAreOpen_FromTheSavedTotalGold()
        {
            AreaCatalog areas = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            Assert.That(areas.Get("rocky_canyon").unlockTotalGold, Is.EqualTo(500), "Rocky Canyon opens at 500 total gold");
            Assert.That(areas.Get("ancient_mine").unlockTotalGold, Is.EqualTo(1200), "Ancient Mine opens at 1200 total gold");

            // (total gold, green mine, rocky canyon, ancient mine); Green Mine and Ancient Mine are evaluated with their real scenes, Rocky Canyon without one (the table predates its scene): an open area WITH a scene is Playable
            var table = new (int gold, AreaAvailability green, AreaAvailability rocky, AreaAvailability ancient)[]
            {
                (0, AreaAvailability.Playable, AreaAvailability.Locked, AreaAvailability.Locked),
                (499, AreaAvailability.Playable, AreaAvailability.Locked, AreaAvailability.Locked),
                (500, AreaAvailability.Playable, AreaAvailability.ComingSoon, AreaAvailability.Locked),
                (1199, AreaAvailability.Playable, AreaAvailability.ComingSoon, AreaAvailability.Locked),
                (1200, AreaAvailability.Playable, AreaAvailability.ComingSoon, AreaAvailability.Playable),
                (2500, AreaAvailability.Playable, AreaAvailability.ComingSoon, AreaAvailability.Playable),
            };
            foreach (var row in table)
            {
                var memory = new MemoryProgressStore();
                ProgressService service = ProgressService.Create(areas, memory);
                if (row.gold > 0)
                {
                    service.RegisterCollected("t", row.gold);
                }

                Assert.That(AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GreenMineScene).Availability, Is.EqualTo(row.green), $"green_mine at {row.gold}");
                Assert.That(AreaSelectController.Evaluate(service, "rocky_canyon", "").Availability, Is.EqualTo(row.rocky), $"rocky_canyon at {row.gold}");
                Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", SceneNavigator.AncientMineScene).Availability, Is.EqualTo(row.ancient), $"ancient_mine at {row.gold}");

                // the state also survives a restart: the same save loaded by a new service gives the same answer
                ProgressService reloaded = ProgressService.Create(areas, memory);
                Assert.That(AreaSelectController.Evaluate(reloaded, "rocky_canyon", "").Availability, Is.EqualTo(row.rocky), $"rocky_canyon after reload at {row.gold}");
                Assert.That(AreaSelectController.Evaluate(reloaded, "ancient_mine", SceneNavigator.AncientMineScene).Availability, Is.EqualTo(row.ancient), $"ancient_mine after reload at {row.gold}");
            }
        }

        [Test]
        public void CardState_ShowsTheRightNumbersAndWords()
        {
            AreaCatalog areas = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            ProgressService service = ProgressService.Create(areas, new MemoryProgressStore());
            service.RegisterCollected("t", 120); // an unknown id: it pays its gold but is none of the 60 treasures

            // every card says the same three things in the same order: STATUS, TREASURES (by id) and when it opens (in TOTAL gold). None of them is a per map gold number
            AreaCardState green = AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GreenMineScene);
            Assert.That(green.StatusLine, Is.EqualTo("UNLOCKED"));
            Assert.That(green.TreasureText, Is.EqualTo("TREASURES: 0 / 15"));
            Assert.That(green.UnlockText, Is.EqualTo("OPEN FROM THE START"));
            Assert.That(green.Progress01, Is.EqualTo(0f), "the bar is the share of treasures, not gold");
            Assert.That(green.TotalGold, Is.EqualTo(120));

            AreaCardState rocky = AreaSelectController.Evaluate(service, "rocky_canyon", "");
            Assert.That(rocky.IsLocked, Is.True);
            Assert.That(rocky.StatusLine, Is.EqualTo("LOCKED"));
            Assert.That(rocky.TreasureText, Is.EqualTo("TREASURES: 0 / 20"));
            Assert.That(rocky.UnlockText, Is.EqualTo("UNLOCKS AT 500 TOTAL GOLD"));
            Assert.That(rocky.RemainingGold, Is.EqualTo(380), "500 - 120");
            Assert.That(rocky.StatusText, Is.EqualTo("LOCKED"));

            AreaCardState ancient = AreaSelectController.Evaluate(service, "ancient_mine", "");
            Assert.That(ancient.StatusLine, Is.EqualTo("LOCKED"));
            Assert.That(ancient.TreasureText, Is.EqualTo("TREASURES: 0 / 25"));
            Assert.That(ancient.UnlockText, Is.EqualTo("UNLOCKS AT 1200 TOTAL GOLD"));
            Assert.That(ancient.RemainingGold, Is.EqualTo(1080));

            // two real treasures of Green Mine: the counts follow the ids; 520 total gold opens Rocky Canyon (no scene here: COMING SOON)
            service.RegisterCollected("green_mine.camp_01", 100);
            service.RegisterCollected("green_mine.forest_01", 300);
            green = AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GreenMineScene);
            Assert.That(green.TreasureText, Is.EqualTo("TREASURES: 2 / 15"));
            Assert.That(green.Progress01, Is.EqualTo(2f / 15f).Within(1e-4f));
            Assert.That(green.Completed, Is.False, "2 of 15 treasures: not complete, whatever the gold is");
            Assert.That(green.UnlockText, Is.EqualTo("OPEN FROM THE START"));
            rocky = AreaSelectController.Evaluate(service, "rocky_canyon", "");
            Assert.That(rocky.Availability, Is.EqualTo(AreaAvailability.ComingSoon));
            Assert.That(rocky.StatusText, Is.EqualTo("COMING SOON"));
            Assert.That(rocky.StatusLine, Is.EqualTo("UNLOCKED"));
            Assert.That(rocky.UnlockText, Is.EqualTo("UNLOCKED AT 500 TOTAL GOLD"));
            Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", "").RemainingGold, Is.EqualTo(680), "1200 - 520 left for Ancient Mine");

            // all 15 treasures of Green Mine: now it is complete (by treasures)
            foreach (string id in TreasureCatalog.IdsOf("green_mine"))
            {
                service.RegisterCollected(id, 0);
            }

            green = AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GreenMineScene);
            Assert.That(green.TreasureText, Is.EqualTo("TREASURES: 15 / 15"));
            Assert.That(green.Completed && green.ShowsCompleted, Is.True);
            Assert.That(green.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void UnlockThresholdAndRemainingGold_AreComputedFromTheCatalogAndTheService_NotHardCoded()
        {
            // a different catalog: the card text follows it
            AreaCatalog custom = AreaCatalog.CreateDefault();
            custom.Configure(new[]
            {
                new AreaEntry("green_mine", "GREEN MINE", 0, 300),
                new AreaEntry("rocky_canyon", "ROCKY CANYON", 300, 800),
                new AreaEntry("ancient_mine", "ANCIENT MINE", 800, 1500),
            });
            ProgressService service = ProgressService.Create(custom, new MemoryProgressStore());
            service.RegisterCollected("t", 120);

            Assert.That(AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GreenMineScene).UnlockText, Is.EqualTo("OPEN FROM THE START"));
            AreaCardState rocky300 = AreaSelectController.Evaluate(service, "rocky_canyon", "");
            Assert.That(rocky300.UnlockText, Is.EqualTo("UNLOCKS AT 300 TOTAL GOLD"));
            Assert.That(rocky300.RemainingGold, Is.EqualTo(180), "300 - 120");
            AreaCardState ancient800 = AreaSelectController.Evaluate(service, "ancient_mine", "");
            Assert.That(ancient800.UnlockText, Is.EqualTo("UNLOCKS AT 800 TOTAL GOLD"));
            Assert.That(ancient800.RemainingGold, Is.EqualTo(680), "800 - 120");

            // the real catalog: what is left shrinks by exactly the gold collected, one by one, until the area opens
            AreaCatalog areas = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            var store = new MemoryProgressStore();
            ProgressService real = ProgressService.Create(areas, store);
            int total = 0;
            foreach (int gold in new[] { 1, 99, 150, 249 }) // 499 in total: still one short
            {
                real.RegisterCollected("t" + total, gold);
                total += gold;
                AreaCardState rocky = AreaSelectController.Evaluate(real, "rocky_canyon", "");
                Assert.That(real.TotalGold, Is.EqualTo(total));
                Assert.That(rocky.RemainingGold, Is.EqualTo(500 - total), $"Rocky Canyon at {total}");
                Assert.That(rocky.UnlockText, Is.EqualTo("UNLOCKS AT 500 TOTAL GOLD"), "the threshold itself never changes");
                Assert.That(AreaSelectController.Evaluate(real, "ancient_mine", "").RemainingGold, Is.EqualTo(1200 - total));
            }

            Assert.That(AreaSelectController.Evaluate(real, "rocky_canyon", "").RemainingGold, Is.EqualTo(1));
            real.RegisterCollected("open", 1); // 500 total
            AreaCardState open = AreaSelectController.Evaluate(real, "rocky_canyon", "");
            Assert.That(open.IsLocked, Is.False, "at 500 gold nothing is left to unlock");
            Assert.That(open.StatusLine, Is.EqualTo("UNLOCKED"));
            Assert.That(open.UnlockText, Is.EqualTo("UNLOCKED AT 500 TOTAL GOLD"));
        }

        // ------------------------------------------------------------------ the flow

        [UnityTest]
        public IEnumerator Play_OpensAreaSelection_NotGameplay_AndBackReturnsToTheMainMenu()
        {
            yield return LoadMenu(0);
            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;
            MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            Transform buttons = Find<Transform>("Buttons");
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            Assert.That(screen.IsOpen, Is.False, "closed until PLAY");

            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(requested, Is.Null, "PLAY must not go straight to the gameplay scene any more");
            Assert.That(menu.IsAreaSelectOpen, Is.True);
            Assert.That(screen.IsOpen, Is.True);
            Assert.That(buttons.gameObject.activeSelf, Is.False, "the menu column gives way to the Area Selection");
            Assert.That(Find<Transform>("TitleRoot").gameObject.activeSelf, Is.False, "and so does the game title");
            Assert.That(screen.transform.Find("TitleBanner/Heading").GetComponent<TMP_Text>().text, Is.EqualTo("SELECT AREA"));
            Assert.That(screen.CardCount, Is.EqualTo(3));
            Assert.That(screen.GetCard(0).AreaId, Is.EqualTo("green_mine"));
            Assert.That(screen.GetCard(1).AreaId, Is.EqualTo("rocky_canyon"));
            Assert.That(screen.GetCard(2).AreaId, Is.EqualTo("ancient_mine"));
            MenuCharacterStage stage = UnityEngine.Object.FindAnyObjectByType<MenuCharacterStage>();
            Assert.That(stage.Anchor.gameObject.activeSelf, Is.False, "the 3D miner makes room for the cards");

            Find<Button>("AreaBackButton").onClick.Invoke();
            yield return null;
            Assert.That(requested, Is.Null, "BACK loads nothing");
            Assert.That(menu.IsAreaSelectOpen, Is.False);
            Assert.That(buttons.gameObject.activeSelf, Is.True, "BACK returns to the Main Menu");
            Assert.That(Find<Transform>("TitleRoot").gameObject.activeSelf, Is.True);
            Assert.That(stage.Anchor.gameObject.activeSelf, Is.True, "the miner is back");
            Assert.That(Find<Button>("PlayButton").interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator EscapeKey_AndroidBack_LeavesTheAreaSelectionForTheMainMenu()
        {
            InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Area Test Keyboard");
            try
            {
                string requested = null;
                yield return OpenAreaSelection(0);
                SceneNavigator.LoadHook = name => requested = name;
                MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
                keyboard.MakeCurrent();
                yield return null;
                Assert.That(menu.IsAreaSelectOpen, Is.True);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(menu.IsAreaSelectOpen, Is.False, "the Android Back key (Escape) closes the Area Selection");
                Assert.That(Find<Transform>("Buttons").gameObject.activeSelf, Is.True, "and shows the Main Menu");
                Assert.That(requested, Is.Null);
            }
            finally
            {
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
            }
        }

        [UnityTest]
        public IEnumerator CharacterSelection_StaysASeparateMenu()
        {
            yield return LoadMenu(0);
            MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            Find<Button>("CharacterButton").onClick.Invoke();
            yield return null;
            Assert.That(menu.IsCharacterSelectOpen, Is.True, "CHARACTER still opens the miner selection");
            Assert.That(menu.IsAreaSelectOpen, Is.False);
            Find<Button>("CharacterBackButton").onClick.Invoke();
            yield return null;
            Assert.That(menu.IsCharacterSelectOpen, Is.False);
            Assert.That(Find<Transform>("Buttons").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator GreenMine_Play_LoadsTheGreenMineScene_AndKeepsTheChosenMiner()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId); // Ruby
            yield return OpenAreaSelection(0);
            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView green = Card(screen, "green_mine");
            Assert.That(green.PlayButton.gameObject.activeSelf, Is.True, "Green Mine shows PLAY");
            Assert.That(green.PlayButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("PLAY"));

            green.PlayButton.onClick.Invoke();
            Assert.That(requested, Is.EqualTo(SceneNavigator.GreenMineScene), "GREEN MINE enters the GreenMine scene (GameplayTest stays a test sandbox)");
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "the chosen miner is untouched");

            requested = null;
            Assert.That(screen.TryEnter("green_mine"), Is.EqualTo(AreaEnterResult.Entered));
            Assert.That(requested, Is.EqualTo(SceneNavigator.GreenMineScene));
        }

        // ------------------------------------------------------------------ open / locked / coming soon

        [UnityTest]
        public IEnumerator FreshGame_GreenMineIsOpen_TheOtherTwoAreLocked_WithTheGoldTheyNeed()
        {
            yield return OpenAreaSelection(0);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView green = Card(screen, "green_mine");
            AreaCardView rocky = Card(screen, "rocky_canyon");
            AreaCardView ancient = Card(screen, "ancient_mine");

            Assert.That(green.State.Availability, Is.EqualTo(AreaAvailability.Playable));
            Assert.That(green.LockVisible, Is.False);
            Assert.That(green.ComingSoonVisible, Is.False);
            Assert.That(green.StatusLabel.text, Is.EqualTo("UNLOCKED"));
            Assert.That(green.TreasuresLabel.text, Is.EqualTo("TREASURES: 0 / 15"));
            Assert.That(green.UnlockLabel.text, Is.EqualTo("OPEN FROM THE START"));
            Assert.That(green.StatusCheckVisible && !green.StatusLockVisible, Is.True, "a check mark in front of UNLOCKED");

            foreach ((AreaCardView card, string treasures, string unlock) in new[] { (rocky, "TREASURES: 0 / 20", "UNLOCKS AT 500 TOTAL GOLD"), (ancient, "TREASURES: 0 / 25", "UNLOCKS AT 1200 TOTAL GOLD") })
            {
                Assert.That(card.State.Availability, Is.EqualTo(AreaAvailability.Locked), card.AreaId);
                Assert.That(card.LockVisible, Is.True, card.AreaId + " shows its padlock");
                Assert.That(card.ComingSoonVisible, Is.False, "locked is not COMING SOON");
                Assert.That(card.LockedButton.gameObject.activeSelf, Is.True);
                Assert.That(card.PlayButton.gameObject.activeSelf, Is.False);
                Assert.That(card.ComingSoonButton.gameObject.activeSelf, Is.False);
                Assert.That(card.StatusLabel.text, Is.EqualTo("LOCKED"), card.AreaId);
                Assert.That(card.StatusLockVisible && !card.StatusCheckVisible, Is.True, card.AreaId + ": a padlock in front of LOCKED");
                Assert.That(card.TreasuresLabel.text, Is.EqualTo(treasures), card.AreaId);
                Assert.That(card.UnlockLabel.text, Is.EqualTo(unlock), card.AreaId + " says when it opens, in TOTAL gold");
                Assert.That(card.LockedButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("LOCKED"));
            }

            // a locked card is visibly faded (the frame is tinted cool and dim), an open one is not
            Assert.That(rocky.transform.Find("Frame").GetComponent<Image>().color, Is.Not.EqualTo(Color.white), "locked: faded frame");
            Assert.That(rocky.transform.Find("Preview/Picture").GetComponent<Image>().color.r, Is.LessThan(0.7f), "locked: dimmed picture");
            Assert.That(green.transform.Find("Frame").GetComponent<Image>().color, Is.EqualTo(Color.white), "open: full colour");
            Assert.That(green.transform.Find("Preview/Picture").GetComponent<Image>().color, Is.EqualTo(Color.white));

            // each card has its own preview picture
            var pictures = new HashSet<Sprite>();
            for (int i = 0; i < screen.CardCount; i++)
            {
                Sprite picture = screen.GetCard(i).transform.Find("Preview/Picture").GetComponent<Image>().sprite;
                Assert.That(picture, Is.Not.Null);
                pictures.Add(picture);
            }

            Assert.That(pictures.Count, Is.EqualTo(3), "three different preview pictures");
        }

        [UnityTest]
        public IEnumerator Gold499_KeepsRockyCanyonLocked_Gold500_OpensItAsPlayable()
        {
            yield return OpenAreaSelection(499);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView rocky = Card(screen, "rocky_canyon");
            Assert.That(rocky.State.Availability, Is.EqualTo(AreaAvailability.Locked), "499 gold is not enough");
            Assert.That(rocky.UnlockLabel.text, Is.EqualTo("UNLOCKS AT 500 TOTAL GOLD"), "one gold short: still the same threshold");
            Assert.That(rocky.State.RemainingGold, Is.EqualTo(1));
            Assert.That(screen.TotalGoldLabel.text, Is.EqualTo("TOTAL GOLD: 499"), "the whole game's gold, said so, once");
            Assert.That(screen.TryEnter("rocky_canyon"), Is.EqualTo(AreaEnterResult.Locked));

            // the 500th gold is collected (in the gameplay scene), the player comes back to the menu: the screen shows it unlocked
            Find<Button>("AreaBackButton").onClick.Invoke();
            UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.RegisterCollected("one_more", 1);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(rocky.State.Availability, Is.EqualTo(AreaAvailability.Playable), "500 gold opens Rocky Canyon, which has its scene now");
            Assert.That(rocky.LockVisible, Is.False);
            Assert.That(rocky.ComingSoonVisible, Is.False, "it is a real area now, not COMING SOON");
            Assert.That(rocky.PlayButton.gameObject.activeSelf, Is.True, "it shows PLAY");
            Assert.That(Card(screen, "ancient_mine").State.Availability, Is.EqualTo(AreaAvailability.Locked), "Ancient Mine still needs 1200");

            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;
            rocky.PlayButton.onClick.Invoke();
            Assert.That(requested, Is.EqualTo(SceneNavigator.RockyCanyonScene), "PLAY opens RockyCanyon.unity");
        }

        [UnityTest]
        public IEnumerator Gold1199_KeepsAncientMineLocked_Gold1200_OpensItAsPlayable_AndPlayEntersAncientMine()
        {
            yield return OpenAreaSelection(1199);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView ancient = Card(screen, "ancient_mine");
            Assert.That(Card(screen, "rocky_canyon").State.Availability, Is.EqualTo(AreaAvailability.Playable));
            Assert.That(ancient.State.Availability, Is.EqualTo(AreaAvailability.Locked), "1199 gold is not enough");
            Assert.That(ancient.UnlockLabel.text, Is.EqualTo("UNLOCKS AT 1200 TOTAL GOLD"), "one gold short");
            Assert.That(ancient.State.RemainingGold, Is.EqualTo(1));
            Assert.That(screen.TryEnter("ancient_mine"), Is.EqualTo(AreaEnterResult.Locked));

            Find<Button>("AreaBackButton").onClick.Invoke();
            UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.RegisterCollected("one_more", 1);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(ancient.SceneName, Is.EqualTo(SceneNavigator.AncientMineScene), "the card opens AncientMine.unity");
            Assert.That(ancient.State.Availability, Is.EqualTo(AreaAvailability.Playable), "1200 gold opens Ancient Mine, which has its scene now");
            Assert.That(ancient.ComingSoonVisible, Is.False, "COMING SOON is gone");
            Assert.That(ancient.LockVisible, Is.False);
            Assert.That(ancient.PlayButton.gameObject.activeSelf, Is.True, "it shows PLAY");
            Assert.That(ancient.StatusLabel.text, Is.EqualTo("UNLOCKED"));
            Assert.That(ancient.TreasuresLabel.text, Is.EqualTo("TREASURES: 0 / 25"));
            Assert.That(ancient.UnlockLabel.text, Is.EqualTo("UNLOCKED AT 1200 TOTAL GOLD"));

            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;
            ancient.PlayButton.onClick.Invoke();
            Assert.That(requested, Is.EqualTo(SceneNavigator.AncientMineScene), "PLAY opens AncientMine.unity");
        }

        [UnityTest]
        public IEnumerator ComingSoon_LooksDifferentFromLocked_AndCannotBeEntered()
        {
            // 500 gold: Rocky Canyon is playable, Ancient Mine is still locked
            yield return OpenAreaSelection(500);
            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView rocky = Card(screen, "rocky_canyon");
            AreaCardView ancient = Card(screen, "ancient_mine");

            Assert.That(rocky.State.Availability, Is.EqualTo(AreaAvailability.Playable));
            Assert.That(rocky.ComingSoonVisible, Is.False);
            Assert.That(rocky.LockVisible, Is.False, "no padlock: it is open");
            Assert.That(rocky.PlayButton.gameObject.activeSelf, Is.True, "it shows PLAY");
            Assert.That(rocky.transform.Find("Frame").GetComponent<Image>().color, Is.EqualTo(Color.white), "not faded: it is unlocked");

            // Ancient Mine: locked -> padlock, no banner
            Assert.That(ancient.LockVisible, Is.True);
            Assert.That(ancient.ComingSoonVisible, Is.False);
            Assert.That(ancient.UnlockedVisible, Is.False);
            ancient.LockedButton.onClick.Invoke();
            Assert.That(screen.TryEnter("ancient_mine"), Is.EqualTo(AreaEnterResult.Locked));
            Assert.That(screen.TryEnter("no_such_area"), Is.EqualTo(AreaEnterResult.UnknownArea));
            Assert.That(requested, Is.Null, "a locked or unknown area loads nothing");
            Assert.That(ancient.IsDenyAnimationPlaying, Is.True, "a refused tap gives visible feedback");

            // 1200 gold: an open area WITHOUT a map is COMING SOON. Every real area has a scene now, so Ancient Mine's card instance (a throw-away copy in this test scene) is told it has none
            ancient.Configure("ancient_mine", "");
            Find<Button>("AreaBackButton").onClick.Invoke();
            UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.RegisterCollected("more", 700);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(ancient.State.Availability, Is.EqualTo(AreaAvailability.ComingSoon));
            Assert.That(ancient.ComingSoonVisible, Is.True, "COMING SOON banner");
            Assert.That(ancient.UnlockedVisible, Is.True, "UNLOCKED chip");
            Assert.That(ancient.LockVisible, Is.False, "no padlock: it is open");
            Assert.That(ancient.ComingSoonButton.gameObject.activeSelf, Is.True);
            Assert.That(ancient.ComingSoonButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("COMING SOON"));
            Assert.That(ancient.transform.Find("Frame").GetComponent<Image>().color, Is.EqualTo(Color.white), "not faded: it is unlocked");
            Assert.That(ancient.CompleteVisible, Is.False, "an area without a map is never shown as complete");
            ancient.ComingSoonButton.onClick.Invoke();
            Assert.That(screen.TryEnter("ancient_mine"), Is.EqualTo(AreaEnterResult.ComingSoon));
            Assert.That(requested, Is.Null, "COMING SOON loads nothing");
            Assert.That(ancient.IsDenyAnimationPlaying, Is.True);

            // even with 4000 gold the area without a scene stays COMING SOON and never loads anything
            Find<Button>("AreaBackButton").onClick.Invoke();
            UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.RegisterCollected("big", 3000);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(ancient.State.Availability, Is.EqualTo(AreaAvailability.ComingSoon));
            Assert.That(screen.TryEnter("ancient_mine"), Is.EqualTo(AreaEnterResult.ComingSoon));
            Assert.That(requested, Is.Null);
        }

        [UnityTest]
        public IEnumerator ProgressIsCorrect_EveryTimeTheScreenOpens_AndOpeningItNeverChangesTheSave()
        {
            yield return OpenAreaSelection(120);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            string saved = store.Json;
            int writes = store.WriteCount;

            AreaCardView green = Card(screen, "green_mine");
            Assert.That(screen.TotalGoldLabel.text, Is.EqualTo("TOTAL GOLD: 120"));
            Assert.That(green.TreasuresLabel.text, Is.EqualTo("TREASURES: 0 / 15"), "the seeded gold has no treasure ids");
            Assert.That(Card(screen, "rocky_canyon").UnlockLabel.text, Is.EqualTo("UNLOCKS AT 500 TOTAL GOLD"));
            Assert.That(green.TargetFill, Is.EqualTo(0f), "the bar counts treasures, never gold");

            // collect two real treasures (and more gold), leave and open again
            Find<Button>("AreaBackButton").onClick.Invoke();
            ProgressService live = UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service;
            live.RegisterCollected("green_mine.camp_01", 30);
            live.RegisterCollected("green_mine.forest_01", 300); // 450 total
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(green.TreasuresLabel.text, Is.EqualTo("TREASURES: 2 / 15"), "the numbers are re-read every time the screen opens");
            Assert.That(screen.TotalGoldLabel.text, Is.EqualTo("TOTAL GOLD: 450"));
            Assert.That(Card(screen, "rocky_canyon").State.RemainingGold, Is.EqualTo(50));
            Assert.That(green.TargetFill, Is.EqualTo(2f / 15f).Within(1e-4f));
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(green.DisplayedFill, Is.EqualTo(2f / 15f).Within(0.01f), "the bar fills up to the share of treasures");

            // opening and closing the screen wrote nothing to the save
            Find<Button>("AreaBackButton").onClick.Invoke();
            Find<Button>("PlayButton").onClick.Invoke();
            Find<Button>("AreaBackButton").onClick.Invoke();
            Assert.That(store.WriteCount, Is.EqualTo(writes + 2), "only the two RegisterCollected calls wrote (opening / closing the screen is read-only)");
            Assert.That(saved, Is.Not.EqualTo(store.Json));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.TotalGold, Is.EqualTo(450));
        }

        [UnityTest]
        public IEnumerator SelectionFeedback_FocusedCardIsOutlinedAtTheSameSize_AndAClosedCardShakesOnTap()
        {
            yield return OpenAreaSelection(0);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView green = Card(screen, "green_mine");
            AreaCardView rocky = Card(screen, "rocky_canyon");
            yield return new WaitForSecondsRealtime(1.0f);

            Assert.That(screen.FocusedIndex, Is.EqualTo(0), "the playable area is focused when the screen opens");
            Assert.That(green.IsFocused, Is.True);
            Assert.That(rocky.IsFocused, Is.False);
            Assert.That(green.transform.localScale.x, Is.EqualTo(rocky.transform.localScale.x).Within(0.001f), "stage 18B: all three cards keep one size");
            Assert.That(green.RingVisible, Is.True, "the focused card is outlined (a shape, not only a colour)");
            Assert.That(rocky.RingVisible, Is.False);

            rocky.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.That(rocky.IsDenyAnimationPlaying, Is.True, "tapping a locked card shakes it");
            Assert.That(screen.FocusedIndex, Is.EqualTo(0), "a locked card cannot take the focus");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(rocky.IsDenyAnimationPlaying, Is.False, "the shake ends");
            Assert.That(((RectTransform)rocky.transform).anchoredPosition.x, Is.EqualTo(0f).Within(0.01f), "and the card rests where it was");
        }

        // ------------------------------------------------------------------ fit on every phone screen

        [UnityTest]
        public IEnumerator Cards_BackButton_AndTexts_StayOnScreen_AndTouchTargetsAre48dp_OnEveryTargetScreen()
        {
            yield return OpenAreaSelection(520);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            yield return new WaitForSecondsRealtime(1.0f); // entrance animations finish

            yield return OnEveryTargetScreen(() =>
            {
                Rect canvas = CanvasRect((RectTransform)Find<Canvas>("UI Canvas").transform);
                Assert.That(canvas.height, Is.EqualTo(1080f).Within(0.5f), "the reference height is never cut");
                Assert.That(Find<UiFitScale>("AreaPanel").CurrentScale, Is.EqualTo(1f).Within(1e-4f), "on a normal phone the cards keep their full authored size (full size touch targets)");

                Rect title = CanvasRect((RectTransform)screen.transform.Find("TitleBanner"));
                Assert.That(Contains(canvas, title, 0f), Is.True, "the SELECT AREA banner is on screen");
                Assert.That(title.center.x, Is.EqualTo(canvas.center.x).Within(1f), "and centred");

                Button back = screen.BackButton;
                Rect backRect = CanvasRect((RectTransform)back.transform);
                Assert.That(Contains(canvas, backRect, EdgeMargin), Is.True, $"BACK is on screen ({backRect})");
                Assert.That(backRect.width, Is.GreaterThanOrEqualTo(MinTouchUnits));
                Assert.That(backRect.height, Is.GreaterThanOrEqualTo(MinTouchUnits), "BACK is at least 48 dp high");
                Assert.That(Overlaps(backRect, title), Is.False, "BACK does not touch the banner");

                var frames = new List<Rect>();
                for (int i = 0; i < screen.CardCount; i++)
                {
                    AreaCardView card = screen.GetCard(i);
                    Rect frame = CanvasRect((RectTransform)card.transform.Find("Frame"));
                    frames.Add(frame);
                    Assert.That(Contains(canvas, frame, EdgeMargin), Is.True, $"{card.AreaId}: the whole card is on screen with a margin ({frame} in {canvas})");
                    Assert.That(Contains(canvas, CanvasRect((RectTransform)card.transform.Find("Shadow")), 0f), Is.True, $"{card.AreaId}: even its shadow");

                    Rect banner = CanvasRect((RectTransform)card.transform.Find("NameBanner"));
                    Assert.That(Contains(canvas, banner, 0f), Is.True, $"{card.AreaId}: the name banner is on screen");
                    Assert.That(Overlaps(banner, backRect), Is.False, $"{card.AreaId}: the name banner clears BACK");
                    Assert.That(Overlaps(banner, title), Is.False, $"{card.AreaId}: the name banner clears the SELECT AREA banner");

                    Button button = VisibleButton(card);
                    Rect buttonRect = CanvasRect((RectTransform)button.transform);
                    Assert.That(buttonRect.height, Is.GreaterThanOrEqualTo(MinTouchUnits), $"{card.AreaId}: the button is at least 48 dp high");
                    Assert.That(buttonRect.width, Is.GreaterThanOrEqualTo(MinTouchUnits));
                    Assert.That(Contains(frame, buttonRect, 0f), Is.True, $"{card.AreaId}: the button is inside its card");

                    // every label fits its own box (nothing overflows the card)
                    foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(false))
                    {
                        text.ForceMeshUpdate();
                        Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1f), $"{card.AreaId}: '{text.text}' fits its box");
                        if (!text.transform.IsChildOf(card.transform.Find("NameBanner"))) // the name banner straddles the card's top edge by design
                        {
                            Assert.That(Contains(frame, CanvasRect(text.rectTransform), -1f), Is.True, $"{card.AreaId}: '{text.text}' stays inside the card");
                        }
                    }
                }

                // vertical balance: the cards sit low enough to leave no big empty strip under them, but stay above the Android gesture zone
                float lowest = float.MaxValue;
                foreach (Rect frame in frames) { lowest = Mathf.Min(lowest, frame.yMin); }
                float bottomGap = lowest - canvas.yMin;
                Assert.That(bottomGap, Is.LessThanOrEqualTo(100f), $"the empty strip under the cards is small ({bottomGap} units)");
                Assert.That(bottomGap, Is.GreaterThanOrEqualTo(0.055f * 1080f), $"the cards stay out of the bottom gesture zone ({bottomGap} units)");

                for (int i = 1; i < frames.Count; i++)
                {
                    Assert.That(Overlaps(frames[i - 1], frames[i]), Is.False, "the cards do not overlap");
                    Assert.That(frames[i].center.y, Is.EqualTo(frames[0].center.y).Within(4f), "the cards share one row");
                }

                Assert.That(((frames[0].center.x + frames[2].center.x) * 0.5f), Is.EqualTo(canvas.center.x).Within(8f), "the row of cards is centred");
            });
        }

        [UnityTest]
        public IEnumerator LockBadge_IsSmall_InACorner_AndLeavesTheMiddleOfThePictureFree()
        {
            yield return OpenAreaSelection(0);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            yield return new WaitForSecondsRealtime(1.0f);
            const float oldBadgeSize = 170f;
            foreach (string id in new[] { "rocky_canyon", "ancient_mine" })
            {
                AreaCardView card = Card(screen, id);
                Assert.That(card.LockVisible, Is.True, id);
                Rect picture = CanvasRect((RectTransform)card.transform.Find("Preview/Picture"));
                Rect badge = CanvasRect((RectTransform)card.transform.Find("Preview/LockOverlay/LockBadge"));
                float scale = picture.width / ((RectTransform)card.transform.Find("Preview/Picture")).rect.width; // the card's own focus scale
                Assert.That(badge.width / scale, Is.LessThanOrEqualTo(oldBadgeSize * 0.75f + 1f), id + ": the padlock is at least 25 % smaller than before (170 units)");
                Assert.That(badge.width * badge.height, Is.LessThanOrEqualTo(picture.width * picture.height * 0.12f), id + ": it covers little of the picture");
                Assert.That(Contains(picture, badge, 0f), Is.True, id + ": it sits inside the picture");
                Assert.That(badge.Contains(picture.center), Is.False, id + ": the middle of the picture is free");
                Assert.That(badge.center.x, Is.GreaterThan(picture.center.x + picture.width * 0.25f), id + ": it sits at the right");
                Assert.That(badge.center.y, Is.GreaterThan(picture.center.y + picture.height * 0.2f), id + ": and at the top");
            }
        }

        [UnityTest]
        public IEnumerator Cards_KeepClearOfACutOutAndTheGestureZones()
        {
            SafeAreaFitter.SafeAreaOverride = () => new Rect(Screen.height * 0.13f, Screen.height * 0.06f, Screen.width - Screen.height * 0.26f, Screen.height * 0.94f);
            yield return OpenAreaSelection(520);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            SafeAreaFitter fitter = Find<SafeAreaFitter>("SafeAreaRoot");
            yield return new WaitForSecondsRealtime(1.0f);

            yield return OnEveryTargetScreen(() =>
            {
                Rect allowed = SafeAreaFitter.ShrinkForGestures(SafeAreaFitter.SafeAreaOverride(), fitter.GestureMarginFraction * Screen.height);
                Assert.That(Contains(allowed, ScreenRect(screen.BackButton.GetComponent<RectTransform>()), -1f), Is.True, "BACK stays inside the cut-out and gesture margins");
                Assert.That(CanvasRect((RectTransform)screen.BackButton.transform).height, Is.GreaterThanOrEqualTo(MinTouchUnits), "BACK keeps 48 dp");
                Assert.That(Find<UiFitScale>("AreaPanel").CurrentScale, Is.GreaterThanOrEqualTo(0.82f), "the card row never shrinks below the scale that keeps its buttons at 48 dp");
                for (int i = 0; i < screen.CardCount; i++)
                {
                    AreaCardView card = screen.GetCard(i);
                    Rect frame = ScreenRect((RectTransform)card.transform.Find("Frame"));
                    Assert.That(Contains(allowed, frame, -1f), Is.True, $"{card.AreaId} ({frame}) stays inside the cut-out and gesture margins ({allowed})");
                    Assert.That(Contains(allowed, ScreenRect((RectTransform)VisibleButton(card).transform), -1f), Is.True, $"{card.AreaId}: its button too");
                    Rect button = CanvasRect((RectTransform)VisibleButton(card).transform);
                    Assert.That(button.height, Is.GreaterThanOrEqualTo(MinTouchUnits), $"{card.AreaId}: even with the cut-out shrinking the row, the button is at least 48 dp high ({button.height} units)");
                    Assert.That(button.width, Is.GreaterThanOrEqualTo(MinTouchUnits));
                }
            });
        }
    }
}
#endif
