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
            if (totalGold > 0)
            {
                ProgressService.Create(catalog, store).RegisterCollected("seed_treasure", totalGold);
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

            // (total gold, green mine, rocky canyon, ancient mine); only Green Mine has a scene: the other two can at most be COMING SOON
            var table = new (int gold, AreaAvailability green, AreaAvailability rocky, AreaAvailability ancient)[]
            {
                (0, AreaAvailability.Playable, AreaAvailability.Locked, AreaAvailability.Locked),
                (499, AreaAvailability.Playable, AreaAvailability.Locked, AreaAvailability.Locked),
                (500, AreaAvailability.Playable, AreaAvailability.ComingSoon, AreaAvailability.Locked),
                (1199, AreaAvailability.Playable, AreaAvailability.ComingSoon, AreaAvailability.Locked),
                (1200, AreaAvailability.Playable, AreaAvailability.ComingSoon, AreaAvailability.ComingSoon),
                (2500, AreaAvailability.Playable, AreaAvailability.ComingSoon, AreaAvailability.ComingSoon),
            };
            foreach (var row in table)
            {
                var memory = new MemoryProgressStore();
                ProgressService service = ProgressService.Create(areas, memory);
                if (row.gold > 0)
                {
                    service.RegisterCollected("t", row.gold);
                }

                Assert.That(AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GameplayScene).Availability, Is.EqualTo(row.green), $"green_mine at {row.gold}");
                Assert.That(AreaSelectController.Evaluate(service, "rocky_canyon", "").Availability, Is.EqualTo(row.rocky), $"rocky_canyon at {row.gold}");
                Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", "").Availability, Is.EqualTo(row.ancient), $"ancient_mine at {row.gold}");

                // the state also survives a restart: the same save loaded by a new service gives the same answer
                ProgressService reloaded = ProgressService.Create(areas, memory);
                Assert.That(AreaSelectController.Evaluate(reloaded, "rocky_canyon", "").Availability, Is.EqualTo(row.rocky), $"rocky_canyon after reload at {row.gold}");
                Assert.That(AreaSelectController.Evaluate(reloaded, "ancient_mine", "").Availability, Is.EqualTo(row.ancient), $"ancient_mine after reload at {row.gold}");
            }
        }

        [Test]
        public void CardState_ShowsTheRightNumbersAndWords()
        {
            AreaCatalog areas = AssetDatabase.LoadAssetAtPath<AreaCatalog>(CatalogPath);
            ProgressService service = ProgressService.Create(areas, new MemoryProgressStore());
            service.RegisterCollected("t", 120);

            AreaCardState green = AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GameplayScene);
            Assert.That(green.GoldText, Is.EqualTo("GOAL: 500 GOLD"), "Green Mine shows its goal");
            Assert.That(green.CaptionText, Is.EqualTo("120 / 500 COLLECTED"));
            Assert.That(green.Progress01, Is.EqualTo(0.24f).Within(1e-4f));

            AreaCardState rocky = AreaSelectController.Evaluate(service, "rocky_canyon", "");
            Assert.That(rocky.IsLocked, Is.True);
            Assert.That(rocky.GoldText, Is.EqualTo("380 GOLD TO UNLOCK"), "a locked area shows the gold that is LEFT (500 - 120)");
            Assert.That(rocky.CaptionText, Is.EqualTo("TOTAL GOLD: 120 / 500"));
            Assert.That(rocky.RemainingGold, Is.EqualTo(380));
            Assert.That(rocky.StatusText, Is.EqualTo("LOCKED"));

            AreaCardState ancient = AreaSelectController.Evaluate(service, "ancient_mine", "");
            Assert.That(ancient.GoldText, Is.EqualTo("1080 GOLD TO UNLOCK"));
            Assert.That(ancient.CaptionText, Is.EqualTo("TOTAL GOLD: 120 / 1200"));

            service.RegisterCollected("u", 400); // 520 total: Green Mine complete, Rocky Canyon open
            green = AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GameplayScene);
            Assert.That(green.GoldText, Is.EqualTo("GOAL: 500 GOLD"));
            Assert.That(green.CaptionText, Is.EqualTo("GOAL COMPLETE"), "never more than the goal is counted");
            rocky = AreaSelectController.Evaluate(service, "rocky_canyon", "");
            Assert.That(rocky.Availability, Is.EqualTo(AreaAvailability.ComingSoon));
            Assert.That(rocky.StatusText, Is.EqualTo("COMING SOON"));
            Assert.That(rocky.GoldText, Is.EqualTo("GOAL: 1200 GOLD"), "an open area shows its goal");
            Assert.That(rocky.CaptionText, Is.EqualTo("520 / 1200 COLLECTED"));
            Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", "").GoldText, Is.EqualTo("680 GOLD TO UNLOCK"), "1200 - 520 left for Ancient Mine");
        }

        [Test]
        public void GoalAndRemainingGold_AreComputedFromTheCatalogAndTheService_NotHardCoded()
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

            Assert.That(AreaSelectController.Evaluate(service, "green_mine", SceneNavigator.GameplayScene).GoldText, Is.EqualTo("GOAL: 300 GOLD"));
            Assert.That(AreaSelectController.Evaluate(service, "rocky_canyon", "").GoldText, Is.EqualTo("180 GOLD TO UNLOCK"), "300 - 120");
            Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", "").GoldText, Is.EqualTo("680 GOLD TO UNLOCK"), "800 - 120");

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
                Assert.That(rocky.GoldText, Is.EqualTo(500 - total + " GOLD TO UNLOCK"));
                Assert.That(AreaSelectController.Evaluate(real, "ancient_mine", "").RemainingGold, Is.EqualTo(1200 - total));
            }

            Assert.That(AreaSelectController.Evaluate(real, "rocky_canyon", "").GoldText, Is.EqualTo("1 GOLD TO UNLOCK"));
            real.RegisterCollected("open", 1); // 500 total
            Assert.That(AreaSelectController.Evaluate(real, "rocky_canyon", "").IsLocked, Is.False, "at 500 gold nothing is left to unlock");
            Assert.That(AreaSelectController.Evaluate(real, "rocky_canyon", "").GoldText, Is.EqualTo("GOAL: 1200 GOLD"));
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
        public IEnumerator GreenMine_Play_LoadsTheGameplayScene_AndKeepsTheChosenMiner()
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
            Assert.That(requested, Is.EqualTo(SceneNavigator.GameplayScene), "GREEN MINE enters the existing GameplayTest scene");
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "the chosen miner is untouched");

            requested = null;
            Assert.That(screen.TryEnter("green_mine"), Is.EqualTo(AreaEnterResult.Entered));
            Assert.That(requested, Is.EqualTo(SceneNavigator.GameplayScene));
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
            Assert.That(green.GoldLabel.text, Is.EqualTo("GOAL: 500 GOLD"));
            Assert.That(green.CaptionLabel.text, Is.EqualTo("0 / 500 COLLECTED"));

            foreach ((AreaCardView card, string text, string caption) in new[] { (rocky, "500 GOLD TO UNLOCK", "TOTAL GOLD: 0 / 500"), (ancient, "1200 GOLD TO UNLOCK", "TOTAL GOLD: 0 / 1200") })
            {
                Assert.That(card.State.Availability, Is.EqualTo(AreaAvailability.Locked), card.AreaId);
                Assert.That(card.LockVisible, Is.True, card.AreaId + " shows its padlock");
                Assert.That(card.ComingSoonVisible, Is.False, "locked is not COMING SOON");
                Assert.That(card.LockedButton.gameObject.activeSelf, Is.True);
                Assert.That(card.PlayButton.gameObject.activeSelf, Is.False);
                Assert.That(card.ComingSoonButton.gameObject.activeSelf, Is.False);
                Assert.That(card.GoldLabel.text, Is.EqualTo(text), card.AreaId + " shows the gold it needs");
                Assert.That(card.CaptionLabel.text, Is.EqualTo(caption));
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
        public IEnumerator Gold499_KeepsRockyCanyonLocked_Gold500_OpensItAsComingSoon()
        {
            yield return OpenAreaSelection(499);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView rocky = Card(screen, "rocky_canyon");
            Assert.That(rocky.State.Availability, Is.EqualTo(AreaAvailability.Locked), "499 gold is not enough");
            Assert.That(rocky.GoldLabel.text, Is.EqualTo("1 GOLD TO UNLOCK"), "one gold short");
            Assert.That(rocky.CaptionLabel.text, Is.EqualTo("TOTAL GOLD: 499 / 500"));
            Assert.That(screen.TryEnter("rocky_canyon"), Is.EqualTo(AreaEnterResult.Locked));

            // the 500th gold is collected (in the gameplay scene), the player comes back to the menu: the screen shows it unlocked
            Find<Button>("AreaBackButton").onClick.Invoke();
            UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.RegisterCollected("one_more", 1);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(rocky.State.Availability, Is.EqualTo(AreaAvailability.ComingSoon), "500 gold opens Rocky Canyon");
            Assert.That(rocky.LockVisible, Is.False);
            Assert.That(rocky.ComingSoonVisible, Is.True);
            Assert.That(Card(screen, "ancient_mine").State.Availability, Is.EqualTo(AreaAvailability.Locked), "Ancient Mine still needs 1200");
        }

        [UnityTest]
        public IEnumerator Gold1199_KeepsAncientMineLocked_Gold1200_OpensItAsComingSoon()
        {
            yield return OpenAreaSelection(1199);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView ancient = Card(screen, "ancient_mine");
            Assert.That(Card(screen, "rocky_canyon").State.Availability, Is.EqualTo(AreaAvailability.ComingSoon));
            Assert.That(ancient.State.Availability, Is.EqualTo(AreaAvailability.Locked), "1199 gold is not enough");
            Assert.That(ancient.GoldLabel.text, Is.EqualTo("1 GOLD TO UNLOCK"), "one gold short");

            Find<Button>("AreaBackButton").onClick.Invoke();
            UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.RegisterCollected("one_more", 1);
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(ancient.State.Availability, Is.EqualTo(AreaAvailability.ComingSoon), "1200 gold opens Ancient Mine");
            Assert.That(ancient.GoldLabel.text, Is.EqualTo("GOAL: 2500 GOLD"));
            Assert.That(ancient.CaptionLabel.text, Is.EqualTo("1200 / 2500 COLLECTED"));
        }

        [UnityTest]
        public IEnumerator ComingSoon_LooksDifferentFromLocked_AndCannotBeEntered()
        {
            yield return OpenAreaSelection(500);
            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView rocky = Card(screen, "rocky_canyon");
            AreaCardView ancient = Card(screen, "ancient_mine");

            // Rocky Canyon: open but no map yet -> full colour, UNLOCKED chip, COMING SOON banner and button, no padlock
            Assert.That(rocky.State.Availability, Is.EqualTo(AreaAvailability.ComingSoon));
            Assert.That(rocky.ComingSoonVisible, Is.True, "COMING SOON banner");
            Assert.That(rocky.UnlockedVisible, Is.True, "UNLOCKED chip");
            Assert.That(rocky.LockVisible, Is.False, "no padlock: it is open");
            Assert.That(rocky.ComingSoonButton.gameObject.activeSelf, Is.True);
            Assert.That(rocky.ComingSoonButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("COMING SOON"));
            Assert.That(rocky.transform.Find("Frame").GetComponent<Image>().color, Is.EqualTo(Color.white), "not faded: it is unlocked");
            Assert.That(rocky.CompleteVisible, Is.False, "an area without a map is never shown as complete");

            // Ancient Mine: locked -> padlock, no banner
            Assert.That(ancient.LockVisible, Is.True);
            Assert.That(ancient.ComingSoonVisible, Is.False);
            Assert.That(ancient.UnlockedVisible, Is.False);

            // neither can be entered, by button or by code
            rocky.ComingSoonButton.onClick.Invoke();
            ancient.LockedButton.onClick.Invoke();
            Assert.That(screen.TryEnter("rocky_canyon"), Is.EqualTo(AreaEnterResult.ComingSoon));
            Assert.That(screen.TryEnter("ancient_mine"), Is.EqualTo(AreaEnterResult.Locked));
            Assert.That(screen.TryEnter("no_such_area"), Is.EqualTo(AreaEnterResult.UnknownArea));
            Assert.That(requested, Is.Null, "no scene was loaded");
            Assert.That(rocky.IsDenyAnimationPlaying, Is.True, "a refused tap gives visible feedback");
            Assert.That(ancient.IsDenyAnimationPlaying, Is.True);

            // even with 2500 gold the two areas without a scene stay COMING SOON and never load anything
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
            Assert.That(green.GoldLabel.text, Is.EqualTo("GOAL: 500 GOLD"));
            Assert.That(green.CaptionLabel.text, Is.EqualTo("120 / 500 COLLECTED"));
            Assert.That(Card(screen, "rocky_canyon").GoldLabel.text, Is.EqualTo("380 GOLD TO UNLOCK"));
            Assert.That(green.TargetFill, Is.EqualTo(0.24f).Within(1e-4f));
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(green.DisplayedFill, Is.EqualTo(0.24f).Within(0.01f), "the bar fills up to the progress");
            Assert.That(Card(screen, "rocky_canyon").TargetFill, Is.EqualTo(0.24f).Within(1e-4f), "locked: progress towards the 500 gold unlock");
            Assert.That(Card(screen, "ancient_mine").TargetFill, Is.EqualTo(0.1f).Within(1e-4f), "locked: progress towards the 1200 gold unlock");

            // collect more gold, leave and open again
            Find<Button>("AreaBackButton").onClick.Invoke();
            UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.RegisterCollected("more", 330); // 450 total
            Find<Button>("PlayButton").onClick.Invoke();
            yield return null;
            Assert.That(green.CaptionLabel.text, Is.EqualTo("450 / 500 COLLECTED"), "the numbers are re-read every time the screen opens");
            Assert.That(Card(screen, "rocky_canyon").GoldLabel.text, Is.EqualTo("50 GOLD TO UNLOCK"));
            Assert.That(green.TargetFill, Is.EqualTo(0.9f).Within(1e-4f));

            // opening and closing the screen wrote nothing to the save
            Find<Button>("AreaBackButton").onClick.Invoke();
            Find<Button>("PlayButton").onClick.Invoke();
            Find<Button>("AreaBackButton").onClick.Invoke();
            Assert.That(store.WriteCount, Is.EqualTo(writes + 1), "only the one RegisterCollected wrote (opening / closing the screen is read-only)");
            Assert.That(saved, Is.Not.EqualTo(store.Json));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ProgressManager>().Service.TotalGold, Is.EqualTo(450));
        }

        [UnityTest]
        public IEnumerator SelectionFeedback_FocusedCardLiftsAndAClosedCardShakesOnTap()
        {
            yield return OpenAreaSelection(0);
            AreaSelectController screen = Find<AreaSelectController>("AreaPanel");
            AreaCardView green = Card(screen, "green_mine");
            AreaCardView rocky = Card(screen, "rocky_canyon");
            yield return new WaitForSecondsRealtime(1.0f);

            Assert.That(screen.FocusedIndex, Is.EqualTo(0), "the playable area is focused when the screen opens");
            Assert.That(green.IsFocused, Is.True);
            Assert.That(rocky.IsFocused, Is.False);
            Assert.That(green.transform.localScale.x, Is.GreaterThan(rocky.transform.localScale.x), "the focused card is a little bigger");

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
