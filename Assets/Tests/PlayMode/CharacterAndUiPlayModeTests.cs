#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
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
    /// <summary>
    /// Character selection (arrows, SELECT, BACK, persistence, the Player visual) and the mobile layout rules of the redesigned menus:
    /// touch targets of about 48 dp, no overlapping controls, evenly spaced settings rows.
    /// </summary>
    public sealed class CharacterAndUiPlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayPath = "Assets/Scenes/GameplayTest.unity";
        private const string CatalogPath = "Assets/Data/Characters/CharacterCatalog.asset";

        /// <summary>
        /// 48 dp on a typical Android phone (1080 px high, 2.625 px per dp) is 126 reference units of the 1920x1080 canvas scaler.
        /// This is the real minimum (stage 10 removed the old 120 tolerance).
        /// </summary>
        private const float MinTouchUnits = 126f;

        private Scene loadedScene;
        private MemoryCharacterStore characterStore;
        private TestGameView gameView;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            characterStore = new MemoryCharacterStore();
            CharacterSelection.StoreFactoryOverride = () => characterStore;
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            if (gameView != null)
            {
                yield return gameView.Restore(); // also when an assertion failed in the test
                gameView = null;
            }

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
        }

        private IEnumerator LoadScene(string path)
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(path);
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

        private static Rect CanvasRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var canvasTransform = (RectTransform)canvas.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = canvasTransform.InverseTransformPoint(corners[0]);
            Vector3 max = canvasTransform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Overlaps(Rect a, Rect b, float tolerance = 0.5f)
        {
            return a.xMin < b.xMax - tolerance && a.xMax > b.xMin + tolerance && a.yMin < b.yMax - tolerance && a.yMax > b.yMin + tolerance;
        }

        private static void AssertTouchSizeAndNoOverlap(string screen, IList<Selectable> controls)
        {
            var rects = new List<Rect>();
            foreach (Selectable control in controls)
            {
                Rect r = CanvasRect((RectTransform)control.transform);
                Assert.That(r.width, Is.GreaterThanOrEqualTo(MinTouchUnits), $"{screen}: '{control.name}' is too narrow for a finger (about 48 dp).");
                Assert.That(r.height, Is.GreaterThanOrEqualTo(MinTouchUnits), $"{screen}: '{control.name}' is too short for a finger (about 48 dp).");
                rects.Add(r);
            }

            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                {
                    Assert.That(Overlaps(rects[i], rects[j]), Is.False, $"{screen}: '{controls[i].name}' overlaps '{controls[j].name}'.");
                }
            }
        }

        // ------------------------------------------------------------------ catalog + persistence

        [Test]
        public void Catalog_HasBothMiners_WithTheSameRig_AndNoGameplayComponents()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog[0].id, Is.EqualTo(CharacterSelection.MaleId));
            Assert.That(catalog[1].id, Is.EqualTo(CharacterSelection.FemaleId));
            for (int i = 0; i < catalog.Count; i++)
            {
                GameObject prefab = catalog[i].prefab;
                Assert.That(prefab, Is.Not.Null, catalog[i].id);
                Assert.That(prefab.GetComponent<MinerAnimator>(), Is.Not.Null, "same procedural rig for both miners");
                Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0), "the model must not add colliders");
                Assert.That(prefab.GetComponentInChildren<CharacterController>(true), Is.Null, "abilities live on the Player, not on the model");
                Assert.That(prefab.GetComponentInChildren<Rigidbody>(true), Is.Null);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length, Is.EqualTo(1), "only the MinerAnimator: no gameplay scripts");
            }
        }

        [Test]
        public void Selection_IsSaved_AndSurvivesARestart_BadValuesFallBack()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            Assert.That(CharacterSelection.ResolveId(catalog), Is.EqualTo(CharacterSelection.MaleId), "default miner");

            CharacterSelection.Select(CharacterSelection.FemaleId);
            Assert.That(characterStore.SaveCount, Is.GreaterThan(0), "written through to the store");

            CharacterSelection.ResetCurrent(); // "restart the game": the cached value is gone, the store stays
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(CharacterSelection.ResolveId(catalog), Is.EqualTo(CharacterSelection.FemaleId));

            characterStore.SetString(CharacterSelection.Key, "deleted_hero");
            CharacterSelection.ResetCurrent();
            Assert.That(CharacterSelection.ResolveId(catalog), Is.EqualTo(CharacterSelection.MaleId), "unknown id falls back to the first miner");
            CharacterSelection.Select(null);
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo("deleted_hero"), "an empty id is ignored");
        }

        // ------------------------------------------------------------------ character screen

        [UnityTest]
        public IEnumerator CharacterScreen_Arrows_Select_Back_AndStage()
        {
            yield return LoadScene(MainMenuPath);
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
            MenuCharacterStage stage = Object.FindAnyObjectByType<MenuCharacterStage>();
            CharacterSelectController screen = Find<CharacterSelectController>("CharacterPanel");
            Transform buttons = Find<Transform>("Buttons");
            Assert.That(stage.ShownIndex, Is.EqualTo(0), "the saved (default) miner is on the stage");

            Find<Button>("CharacterButton").onClick.Invoke();
            Assert.That(menu.IsCharacterSelectOpen, Is.True);
            Assert.That(buttons.gameObject.activeSelf, Is.False, "the menu buttons give way to the selection screen");
            Assert.That(stage.CurrentMode, Is.EqualTo(MenuCharacterStage.Mode.Select));
            // not Find<TMP_Text>("Name"): the intro slides (17A) hold three more labels called "Name", and FindObjectsOfTypeAll has no fixed order
            TMP_Text characterName = screen.transform.Find("NamePlate/Name").GetComponent<TMP_Text>();
            Assert.That(characterName.text, Is.EqualTo("BARNABY"));

            Find<Button>("RightArrow").onClick.Invoke();
            Assert.That(screen.ViewingId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(stage.ShownIndex, Is.EqualTo(1));
            Assert.That(characterName.text, Is.EqualTo("RUBY"));
            Find<Button>("RightArrow").onClick.Invoke(); // wraps around
            Assert.That(screen.ViewingId, Is.EqualTo(CharacterSelection.MaleId));
            Find<Button>("LeftArrow").onClick.Invoke(); // and back the other way
            Assert.That(screen.ViewingId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.MaleId), "browsing never saves");

            // BACK leaves without changing the saved miner and puts the saved miner back on the stage
            Find<Button>("CharacterBackButton").onClick.Invoke();
            Assert.That(menu.IsCharacterSelectOpen, Is.False);
            Assert.That(buttons.gameObject.activeSelf, Is.True);
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.MaleId));
            Assert.That(stage.ShownIndex, Is.EqualTo(0));
            Assert.That(stage.CurrentMode, Is.EqualTo(MenuCharacterStage.Mode.Menu));

            // SELECT saves
            Find<Button>("CharacterButton").onClick.Invoke();
            Find<Button>("RightArrow").onClick.Invoke();
            Find<Button>("SelectButton").onClick.Invoke();
            Assert.That(menu.IsCharacterSelectOpen, Is.False);
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(characterStore.TryGetString(CharacterSelection.Key, out string stored) && stored == CharacterSelection.FemaleId, Is.True, "persisted");
            Assert.That(stage.ShownIndex, Is.EqualTo(1), "the menu now shows the chosen miner");

            // reopening shows the chosen miner as the selected one (SELECTED, no second "equipped" tag: see ResponsiveUiPlayModeTests)
            Find<Button>("CharacterButton").onClick.Invoke();
            Assert.That(screen.ViewingId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(Find<Button>("SelectButton").transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("SELECTED"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gameplay_PlayerWearsTheSelectedMiner_WithoutChangingTheGameplaySystems([Values(CharacterSelection.MaleId, CharacterSelection.FemaleId)] string id)
        {
            CharacterSelection.Select(id);
            yield return LoadScene(GameplayPath);
            ThirdPersonPlayerController controller = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
            PlayerCharacterVisual visual = controller.GetComponent<PlayerCharacterVisual>();
            Assert.That(visual, Is.Not.Null, "the Player prefab carries the visual layer");
            Assert.That(visual.Model, Is.Not.Null);
            Assert.That(visual.AppliedId, Is.EqualTo(id));
            Assert.That(visual.Model.transform.parent.name, Is.EqualTo("Visual"), "the model lives under the existing Visual child");
            Assert.That(visual.Model.transform.parent.parent, Is.EqualTo(controller.transform));
            Assert.That(visual.Model.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0));
            Assert.That(controller.transform.Find("Visual").GetComponent<MeshRenderer>().enabled, Is.False, "the capsule placeholder is hidden");

            CharacterController cc = controller.GetComponent<CharacterController>();
            Assert.That(cc.height, Is.EqualTo(2f).Within(1e-4f), "collider untouched");
            Assert.That(cc.radius, Is.EqualTo(0.5f).Within(1e-4f));

            // the miner stands on the ground: its lowest renderer point is the bottom of the CharacterController
            var bounds = new Bounds(visual.Model.transform.position, Vector3.zero);
            foreach (Renderer r in visual.Model.GetComponentsInChildren<Renderer>()) { bounds.Encapsulate(r.bounds); }
            float groundY = controller.transform.position.y - 1f;
            Assert.That(bounds.min.y, Is.EqualTo(groundY).Within(0.12f), "feet at the bottom of the capsule");
            Assert.That(bounds.size.y, Is.InRange(1.8f, 2.4f), "about as tall as the 2 m collider");
        }

        // ------------------------------------------------------------------ layout rules

        [UnityTest]
        public IEnumerator MainMenu_ButtonsAreFingerSized_NotOverlapping_AndAligned()
        {
            yield return LoadScene(MainMenuPath);
            yield return new WaitForSecondsRealtime(1.2f); // the buttons slide in: measure them at rest
            var controls = new List<Selectable>();
            foreach (string name in new[] { "PlayButton", "CharacterButton", "SettingsButton", "ExitButton" })
            {
                controls.Add(Find<Button>(name));
            }

            AssertTouchSizeAndNoOverlap("Main Menu", controls);

            // one tidy column: same left edge and width, PLAY is the biggest and the others share a size
            Rect play = CanvasRect((RectTransform)controls[0].transform);
            for (int i = 1; i < controls.Count; i++)
            {
                Rect r = CanvasRect((RectTransform)controls[i].transform);
                // PLAY breathes (idle pulse up to 2.5%), so allow that much difference
                Assert.That(r.xMin, Is.EqualTo(play.xMin).Within(10f), controls[i].name + " is aligned with PLAY");
                // stage 18B: EXIT is the left half of the bottom row (NEW GAME takes the other half when a game is saved), every other button keeps the column width
                float expectedWidth = controls[i].name == "ExitButton" ? (play.width - 20f) * 0.5f : play.width;
                Assert.That(r.width, Is.EqualTo(expectedWidth).Within(14f), controls[i].name + " has its share of the column");
                Assert.That(r.height, Is.LessThan(play.height), "PLAY is the biggest button");
                Assert.That(r.height, Is.EqualTo(CanvasRect((RectTransform)controls[1].transform).height).Within(1f), "the secondary buttons are equal");
            }

            // the title stays clear of the button column
            Rect title = CanvasRect(Find<TMP_Text>("Title").rectTransform);
            Assert.That(title.yMin, Is.GreaterThan(play.yMax), "the title sits above the buttons");
        }

        /// <summary>
        /// The phones the game targets (landscape only): 16:9 is the design reference, the others are the wider phones. The canvas scaler matches
        /// width and height equally (0.5), so on a wider screen the canvas gets SHORTER in reference units and a fixed-size panel takes a larger share of it.
        /// </summary>
        private static readonly (string name, int width, int height)[] TargetScreens =
        {
            ("16:9", 1920, 1080), ("18:9", 2160, 1080), ("19.5:9", 2340, 1080), ("20:9", 2400, 1080), ("21:9", 2520, 1080), ("2.6:1", 2808, 1080),
        };

        /// <summary>The panel, its title banner and the close button must keep at least this share of the screen height free at every edge.</summary>
        private const float ScreenEdgeMarginShare = 0.03f;

        [UnityTest]
        public IEnumerator Settings_IsLarge_RowsAreEvenlySpacedAndAligned_AndControlsFitFingers()
        {
            yield return LoadScene(MainMenuPath);
            Find<Button>("SettingsButton").onClick.Invoke();
            SettingsPanelController panel = Find<SettingsPanelController>("SettingsPanel");
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f); // let the pop animation finish

            // Measured on every target screen with the Game View really set to that resolution (not on whatever size it happens to have:
            // batch mode has no Game View and a 4:3 Screen, the open Editor a free-aspect one). Everything is restored afterwards.
            gameView = new TestGameView(); // restored by TearDown
            foreach ((string name, int width, int height) screen in TargetScreens)
            {
                gameView.SetResolution(screen.width, screen.height);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(screen.width, screen.height)),
                    $"the Game View did not switch to {screen.name} ({screen.width}x{screen.height}); the layout check would measure the wrong screen");

                try
                {
                    AssertSettingsLayout(panel);
                }
                catch (AssertionException e)
                {
                    throw new AssertionException($"On the {screen.name} screen ({screen.width}x{screen.height}): {e.Message}", e);
                }
            }
        }

        private void AssertSettingsLayout(SettingsPanelController panel)
        {
            Rect canvas = CanvasRect((RectTransform)panel.GetComponentInParent<Canvas>().rootCanvas.transform);
            Transform windowTransform = panel.transform.Find("Window");
            Assert.That(windowTransform, Is.Not.Null, "the settings panel has a Window");
            Rect window = CanvasRect((RectTransform)windowTransform);

            // The "about 60-70% of the screen height" rule (60-75% allowed). The canvas scaler EXPANDS (stage 10), so the canvas is 1080 units high on
            // every screen 16:9 or wider and the fixed-size window covers the same share of the height everywhere: no wider-screen allowance any more.
            float share = window.height / canvas.height;
            Assert.That(share, Is.InRange(0.6f, 0.75f), "the panel covers about 60-70% of the screen height");
            Assert.That(window.width, Is.EqualTo(1300f).Within(1f), "a wider screen adds space around the panel, it does not stretch it");
            Assert.That(window.width / canvas.width, Is.LessThanOrEqualTo(0.8f));

            // nothing is cut off: the window, the banner that overhangs its top edge and the close button that overhangs its corner stay on screen
            float margin = canvas.height * ScreenEdgeMarginShare;
            var onScreen = Rect.MinMaxRect(canvas.xMin + margin, canvas.yMin + margin, canvas.xMax - margin, canvas.yMax - margin);
            foreach (string partName in new[] { "TitleBanner", "CloseButton", "BackButton" })
            {
                Transform part = windowTransform.Find(partName);
                Assert.That(part, Is.Not.Null, partName + " is part of the settings window");
                Rect partRect = CanvasRect((RectTransform)part);
                Assert.That(onScreen.Contains(partRect.min) && onScreen.Contains(partRect.max), Is.True, $"{partName} stays on screen with a margin");
            }

            Assert.That(onScreen.Contains(window.min) && onScreen.Contains(window.max), Is.True, "the window stays on screen with a margin");

            var rows = new List<Rect>();
            var sliderRects = new List<Rect>();
            var labelLefts = new List<float>();
            foreach (string rowName in new[] { "MusicRow", "SfxRow", "CameraRow" })
            {
                Transform row = Find<Transform>(rowName);
                rows.Add(CanvasRect((RectTransform)row));
                sliderRects.Add(CanvasRect((RectTransform)row.Find("Slider")));
                labelLefts.Add(CanvasRect((RectTransform)row.Find("Label")).xMin);
                Assert.That(row.Find("Icon"), Is.Not.Null, rowName + " has an icon");
                Assert.That(row.Find("Value"), Is.Not.Null, rowName + " shows its value");
            }

            // three equal rows, equal gaps, nothing overlapping
            float gap1 = rows[0].yMin - rows[1].yMax;
            float gap2 = rows[1].yMin - rows[2].yMax;
            Assert.That(rows[1].height, Is.EqualTo(rows[0].height).Within(0.5f));
            Assert.That(rows[2].height, Is.EqualTo(rows[0].height).Within(0.5f));
            Assert.That(gap1, Is.GreaterThanOrEqualTo(0f), "rows must not touch each other");
            Assert.That(gap2, Is.EqualTo(gap1).Within(0.5f), "even spacing");
            for (int i = 1; i < 3; i++)
            {
                Assert.That(sliderRects[i].xMin, Is.EqualTo(sliderRects[0].xMin).Within(0.5f), "sliders are aligned");
                Assert.That(sliderRects[i].width, Is.EqualTo(sliderRects[0].width).Within(0.5f));
                Assert.That(labelLefts[i], Is.EqualTo(labelLefts[0]).Within(0.5f), "labels are aligned");
            }

            // everything lives inside the window; the label never runs into the slider
            foreach (Rect row in rows) { Assert.That(window.Contains(new Vector2(row.xMin, row.center.y)) && window.Contains(new Vector2(row.xMax, row.center.y)), Is.True); }
            foreach (string rowName in new[] { "MusicRow", "SfxRow", "CameraRow" })
            {
                Transform row = Find<Transform>(rowName);
                TMP_Text label = row.Find("Label").GetComponent<TMP_Text>();
                float textRight = CanvasRect(label.rectTransform).xMin + label.preferredWidth;
                Assert.That(textRight, Is.LessThan(CanvasRect((RectTransform)row.Find("Slider")).xMin), rowName + ": label text must not touch the slider");
                float valueLeft = CanvasRect(row.Find("Value").GetComponent<TMP_Text>().rectTransform).xMax - row.Find("Value").GetComponent<TMP_Text>().preferredWidth;
                Assert.That(valueLeft, Is.GreaterThan(CanvasRect((RectTransform)row.Find("Slider")).xMax), rowName + ": value must not touch the slider");
            }

            var controls = new List<Selectable> { Find<Button>("CloseButton"), Find<Button>("BackButton") };
            foreach (string rowName in new[] { "MusicRow", "SfxRow", "CameraRow" }) { controls.Add(Find<Transform>(rowName).Find("Slider").GetComponent<Slider>()); }
            AssertTouchSizeAndNoOverlap("Settings", controls);
        }

        [UnityTest]
        public IEnumerator Settings_CloseButton_ClosesLikeBack_AndSlidersStillPersist()
        {
            var store = new MemorySettingsStore();
            GameSettings.StoreFactoryOverride = () => store;
            GameSettings.ResetCurrent();
            yield return LoadScene(MainMenuPath);
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
            Find<Button>("SettingsButton").onClick.Invoke();
            SettingsPanelController panel = Find<SettingsPanelController>("SettingsPanel");
            panel.CameraSlider.value = 1.5f;
            Assert.That(new GameSettings(store).CameraSensitivity, Is.EqualTo(1.5f).Within(1e-4f), "the redesigned slider still writes through to PlayerPrefs");
            Find<Button>("CloseButton").onClick.Invoke();
            Assert.That(menu.IsSettingsOpen, Is.False);
            Assert.That(Find<Transform>("Buttons").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator Pause_Panel_ButtonsFitFingers_AndTheBannerClearsTheHud()
        {
            yield return LoadScene(GameplayPath);
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            Find<Button>("PauseButton").onClick.Invoke();
            Assert.That(pause.IsPaused, Is.True);
            yield return null;
            yield return new WaitForSecondsRealtime(0.45f);

            AssertTouchSizeAndNoOverlap("Pause", new List<Selectable> { Find<Button>("ResumeButton"), Find<Button>("SettingsButton"), Find<Button>("MainMenuButton") });

            Rect resume = CanvasRect(Find<RectTransform>("ResumeButton"));
            Rect settings = CanvasRect(Find<RectTransform>("SettingsButton"));
            Assert.That(resume.height, Is.GreaterThan(settings.height), "RESUME is the primary (largest) button");

            // the PAUSED banner must not sit on top of the HUD header
            Rect header = CanvasRect(Find<RectTransform>("HeaderPanel"));
            Transform window = Find<Transform>("PausePanel").Find("Window");
            Rect banner = CanvasRect((RectTransform)window.Find("TitleBanner"));
            Assert.That(Overlaps(header, banner), Is.False, "PAUSED banner vs HUD header");
        }

        [UnityTest]
        public IEnumerator PauseButton_IsFingerSized_AndStaysClearOfTheHud()
        {
            yield return LoadScene(GameplayPath);
            Button pauseButton = Find<Button>("PauseButton");
            Rect pause = CanvasRect((RectTransform)pauseButton.transform);
            Assert.That(pause.width, Is.GreaterThanOrEqualTo(MinTouchUnits));
            Assert.That(pause.height, Is.GreaterThanOrEqualTo(MinTouchUnits));
            Assert.That(Overlaps(pause, CanvasRect(Find<RectTransform>("HeaderPanel"))), Is.False, "PAUSE does not cover the gold header");
            Assert.That(Overlaps(pause, CanvasRect(Find<RectTransform>("MovementJoystick"))), Is.False, "PAUSE does not sit on the joystick");
        }
    }
}
#endif
