#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>
    /// Stage 18D: the four celebration screens (LEVEL COMPLETE of Green Mine and Rocky Canyon, GAME COMPLETE, 100% COLLECTION) after their final layout. Each screen is opened the way the game opens it after a restart (an OWED
    /// celebration in an in-memory save, shown a moment after the map is entered), so no treasure has to be collected and no gold is paid. Checks: the words and numbers, the layout at five screen shapes and on the iPhone 15 Pro Max
    /// (every text inside its panel and away from the rim, the title away from the top edge, even gaps, 48 dp buttons, nothing behind the panel takes a touch), real taps on every button and the Android Back key.
    /// Real taps and <c>Button.onClick.Invoke()</c> are different things: only the taps in the "real tap" tests prove the touch path. In-memory saves only; the developer's real save file is hashed before and after.
    /// </summary>
    public sealed class Celebration18dPlayModeTests
    {
        private const string GreenPath = "Assets/Scenes/GreenMine.unity";
        private const string RockyPath = "Assets/Scenes/RockyCanyon.unity";
        private const string AncientPath = "Assets/Scenes/AncientMine.unity";

        /// <summary>48 dp on a typical Android phone (1080 px high, 2.625 px per dp) is 126 reference units.</summary>
        private const float MinTouchUnits = 126f;

        private static string realSavePath, realSaveHashBefore;
        private readonly List<string> requestedScenes = new();
        private MemoryProgressStore store;
        private Scene scene;
        private float previousTimeScale;
        private Mouse mouse;
        private Touchscreen touchscreen;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode previousBehaviour;
        private InputSettings.BackgroundBehavior previousBackground;
        private TestGameView gameView;
        private bool phoneInsets;

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
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            phoneInsets = false;
            SafeAreaFitter.SafeAreaOverride = () => phoneInsets
                ? new Rect(Screen.width * 0.0633f, Screen.height * 0.0488f, Screen.width * (1f - 2f * 0.0633f), Screen.height * (1f - 0.0488f))
                : new Rect(0f, 0f, Screen.width, Screen.height);
            previousBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; // the UI input module drops pointer events while the Editor is unfocused (test run only, not a game setting)
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouse18d");
            touchscreen = InputSystem.AddDevice<Touchscreen>("IC3D_TestTouchscreen18d");
            keyboard = InputSystem.AddDevice<Keyboard>("IC3D_TestKeyboard18d");
            requestedScenes.Clear();
            SceneNavigator.LoadHook = name => requestedScenes.Add(name);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
            GamePause.SetModalOpen(false);
            GamePause.Resume();
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

        // ------------------------------------------------------------------ saves and scenes

        private static List<string> Ids(params string[] areas)
        {
            var list = new List<string>();
            foreach (string area in areas)
            {
                list.AddRange(TreasureCatalog.IdsOf(area));
            }

            return list;
        }

        private static string Save(List<string> collected, int gold, string[] unlocked, string[] completed, string[] pendingAreas = null, string[] pendingAchievements = null, string[] celebratedAchievements = null)
        {
            var data = new GameProgressData { totalGold = gold, tutorialState = 2 };
            data.collectedTreasureIds.AddRange(collected);
            data.unlockedAreaIds.AddRange(unlocked);
            data.completedAreaIds.AddRange(completed);
            if (pendingAreas != null) { data.pendingCelebrationAreaIds.AddRange(pendingAreas); }
            if (pendingAchievements != null) { data.pendingAchievementIds.AddRange(pendingAchievements); }
            if (celebratedAchievements != null) { data.celebratedAchievementIds.AddRange(celebratedAchievements); }
            return JsonUtility.ToJson(data);
        }

        private static readonly string[] AllAreas = { "green_mine", "rocky_canyon", "ancient_mine" };

        private static string GreenSave() => Save(Ids("green_mine"), 625, new[] { "green_mine", "rocky_canyon" }, new[] { "green_mine" }, pendingAreas: new[] { "green_mine" });

        private static string RockySave() => Save(Ids("green_mine", "rocky_canyon"), 1465, AllAreas, new[] { "green_mine", "rocky_canyon" }, pendingAreas: new[] { "rocky_canyon" });

        private static string GameSave()
        {
            List<string> ids = Ids("green_mine", "rocky_canyon");
            ids.AddRange(Ids("ancient_mine").GetRange(0, 19));
            return Save(ids, 2640, AllAreas, AllAreas, pendingAchievements: new[] { "game_complete" });
        }

        private static string CollectionSave() => Save(Ids(AllAreas), 2705, AllAreas, AllAreas, pendingAchievements: new[] { "collection_complete" }, celebratedAchievements: new[] { "game_complete" });

        private static string BothSave() => Save(Ids(AllAreas), 2705, AllAreas, AllAreas, pendingAchievements: new[] { "game_complete", "collection_complete" });

        private IEnumerator LoadScene(string path, string save)
        {
            store = new MemoryProgressStore(save);
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            Physics.SyncTransforms();
        }

        private IEnumerator Unload()
        {
            GamePause.SetModalOpen(false);
            GamePause.Resume();
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            scene = default;
            yield return null;
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

        /// <summary>Loads a map whose save owes a LEVEL COMPLETE and waits until the panel is open and its entrance is over.</summary>
        private IEnumerator OpenLevel(string path, string save)
        {
            yield return LoadScene(path, save);
            LevelCompleteController level = One<LevelCompleteController>();
            yield return WaitUntil(() => level.IsOpen, 10f);
            Assert.That(level.IsOpen, Is.True, "LEVEL COMPLETE opens for the owed celebration of " + path);
            yield return new WaitForSecondsRealtime(1.4f);
        }

        /// <summary>Loads the Ancient Mine with an owed final celebration and waits until its panel is open and its entrance is over.</summary>
        private IEnumerator OpenFinal(string save)
        {
            yield return LoadScene(AncientPath, save);
            FinalCelebrationController finals = One<FinalCelebrationController>();
            yield return WaitUntil(() => finals.IsOpen, 10f);
            Assert.That(finals.IsOpen, Is.True, "the final celebration opens");
            yield return new WaitForSecondsRealtime(1.5f);
        }

        // ------------------------------------------------------------------ geometry and input

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Vector2 ScreenCentre(RectTransform rect) => ScreenRect(rect).center;

        private static bool Inside(Rect outer, Rect inner, float tolerance = 0.75f)
        {
            return inner.xMin >= outer.xMin - tolerance && inner.xMax <= outer.xMax + tolerance && inner.yMin >= outer.yMin - tolerance && inner.yMax <= outer.yMax + tolerance;
        }

        private static Rect TextRect(TMP_Text text)
        {
            text.ForceMeshUpdate();
            Bounds b = text.textBounds;
            Vector3 min = text.transform.TransformPoint(b.min);
            Vector3 max = text.transform.TransformPoint(b.max);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
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

        private IEnumerator ClickUntil(Button button, System.Func<bool> done)
        {
            for (int attempt = 0; attempt < 3 && !done(); attempt++)
            {
                if (attempt > 0)
                {
                    yield return RefocusSimulator();
                    Canvas.ForceUpdateCanvases();
                }

                yield return Click(ScreenCentre((RectTransform)button.transform));
                yield return WaitUntil(done, 1.5f);
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

        private static List<RaycastResult> Hits(Vector2 position)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            return hits;
        }

        private static GameProgressData Saved(MemoryProgressStore store) => JsonUtility.FromJson<GameProgressData>(store.Json);

        private static string Words(TMP_Text text) => text != null ? text.text : string.Empty;

        // ================================================================ 1. the four screens open, with their own words and numbers, and the save is as it was

        [UnityTest]
        public IEnumerator FourScreens_Open_WithTheirOwnWordsAndNumbers_AndTheSaveAndTheGoldStayAsTheyWere()
        {
            // LEVEL COMPLETE, Green Mine
            yield return OpenLevel(GreenPath, GreenSave());
            LevelCompleteController level = One<LevelCompleteController>();
            Assert.That(level.TitleString, Is.EqualTo("LEVEL COMPLETE!"));
            Assert.That(level.AreaString, Is.EqualTo("GREEN MINE COMPLETED"));
            Assert.That(level.CountString, Is.EqualTo("15 / 15 TREASURES COLLECTED"));
            Assert.That(level.GoldString, Does.StartWith("625 GOLD"));
            Assert.That(level.TotalString, Is.EqualTo("YOUR TOTAL GOLD: 625"));
            Assert.That(level.NextString, Is.EqualTo("GO TO ROCKY CANYON"));
            Assert.That(Words(level.StayButton.transform.Find("Label").GetComponent<TMP_Text>()), Is.EqualTo("STAY HERE"));
            Assert.That(Words(level.AreaSelectionButton.transform.Find("Label").GetComponent<TMP_Text>()), Is.EqualTo("AREA SELECTION"));
            Assert.That(Saved(store).totalGold, Is.EqualTo(625), "opening the panel pays nothing");
            Assert.That(GamePause.IsPaused && GamePause.ModalOpen, Is.True, "the game is paused behind the panel");
            yield return Unload();

            // LEVEL COMPLETE, Rocky Canyon
            yield return OpenLevel(RockyPath, RockySave());
            level = One<LevelCompleteController>();
            Assert.That(level.TitleString, Is.EqualTo("LEVEL COMPLETE!"));
            Assert.That(level.AreaString, Is.EqualTo("ROCKY CANYON COMPLETED"));
            Assert.That(level.CountString, Is.EqualTo("20 / 20 TREASURES COLLECTED"));
            Assert.That(level.GoldString, Does.StartWith("840 GOLD"));
            Assert.That(level.TotalString, Is.EqualTo("YOUR TOTAL GOLD: 1465"));
            Assert.That(level.NextString, Is.EqualTo("GO TO ANCIENT MINE"));
            Assert.That(Saved(store).totalGold, Is.EqualTo(1465));
            yield return Unload();

            // GAME COMPLETE: the real total, and the 2500 goal as its own line
            yield return OpenFinal(GameSave());
            FinalCelebrationController finals = One<FinalCelebrationController>();
            Assert.That(finals.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete));
            Assert.That(finals.ActiveSkin, Is.SameAs(finals.Game));
            Assert.That(Words(finals.Game.title), Is.EqualTo("GAME COMPLETE!"));
            Assert.That(Words(finals.Game.subtitle), Is.EqualTo("YOU ARE A MASTER TREASURE HUNTER!"));
            Assert.That(Words(finals.Game.goldLabel), Is.EqualTo("TOTAL GOLD: 2640"), "the player's real gold");
            Assert.That(Words(finals.Game.goalLabel), Is.EqualTo("2500 GOLD GOAL REACHED"), "the goal is its own line");
            Assert.That(Words(finals.Game.goalLabel), Is.Not.EqualTo(Words(finals.Game.goldLabel)));
            Assert.That(Words(finals.Game.treasuresLabel), Is.EqualTo("54 / 60 TREASURES COLLECTED"));
            Assert.That(Words(finals.Game.areasLabel), Is.EqualTo("ALL 3 AREAS UNLOCKED"));
            Assert.That(Words(finals.Game.messageLabel), Is.EqualTo("SOME TREASURES ARE STILL HIDDEN - KEEP DIGGING!"));
            Assert.That(Words(finals.Game.continueButton.transform.Find("Label").GetComponent<TMP_Text>()), Is.EqualTo("CONTINUE EXPLORING"));
            Assert.That(Saved(store).totalGold, Is.EqualTo(2640));
            Assert.That(Saved(store).collectedTreasureIds.Count, Is.EqualTo(54));
            yield return Unload();

            // 100% COLLECTION: 60 / 60, one row per map, the gold on its own line
            yield return OpenFinal(CollectionSave());
            finals = One<FinalCelebrationController>();
            Assert.That(finals.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection));
            Assert.That(finals.ActiveSkin, Is.SameAs(finals.Collection));
            Assert.That(Words(finals.Collection.title), Is.EqualTo("100% COLLECTION COMPLETE!"));
            Assert.That(Words(finals.Collection.subtitle), Is.EqualTo("YOU FOUND EVERY HIDDEN TREASURE!"));
            Assert.That(Words(finals.Collection.treasuresLabel), Is.EqualTo("60 / 60"));
            Assert.That(Words(finals.Collection.goldLabel), Is.EqualTo("TOTAL GOLD: 2705"));
            Assert.That(Words(finals.Collection.areaRows[0]), Does.Contain("GREEN MINE").And.Contain("15 / 15"));
            Assert.That(Words(finals.Collection.areaRows[1]), Does.Contain("ROCKY CANYON").And.Contain("20 / 20"));
            Assert.That(Words(finals.Collection.areaRows[2]), Does.Contain("ANCIENT MINE").And.Contain("25 / 25"));
            Assert.That(Saved(store).totalGold, Is.EqualTo(2705), "no gold is paid by the collection");
            Assert.That(Saved(store).collectedTreasureIds.Count, Is.EqualTo(60));
        }

        // ================================================================ 2. the layout: five screen shapes and the iPhone

        private static readonly (int w, int h, bool phone, string name)[] Shapes =
        {
            (1600, 1200, false, "4:3"), (1920, 1080, false, "16:9"), (2340, 1080, false, "19.5:9"), (2400, 1080, false, "20:9"), (2520, 1080, false, "21:9"), (2796, 1290, true, "iPhone 15 Pro Max"),
        };

        /// <summary>
        /// One celebration screen at the current screen size: inside the safe area, the title away from the top edge, every text inside its panel and away from the rim, the three buttons in even steps with a
        /// full touch size, and nothing behind the panel takes a touch.
        /// </summary>
        private void AssertScreen(string name, Component owner, RectTransform window, Button primary, Button left, Button right, GameObject panelRoot)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = owner.GetComponentInParent<Canvas>().rootCanvas;
            float scale = canvas.scaleFactor;
            Rect safe = ScreenRect((RectTransform)owner.transform.parent);
            Rect win = ScreenRect(window);
            RectTransform bannerRect = (RectTransform)window.Find("TitleBanner");
            Rect banner = ScreenRect(bannerRect);

            // the title keeps its distance to the top edge (18A: 14 units), the window to the bottom edge, both inside the safe area
            Assert.That((safe.yMax - banner.yMax) / scale, Is.GreaterThanOrEqualTo(36f), $"{name}: the title banner keeps at least 36 units to the top of the safe area");
            Assert.That((win.yMin - safe.yMin) / scale, Is.GreaterThanOrEqualTo(30f), $"{name}: the window keeps at least 30 units to the bottom of the safe area");
            Assert.That(Inside(safe, banner) && Inside(safe, win), Is.True, $"{name}: the panel is inside the safe area");
            Assert.That(Mathf.Abs((safe.yMax - banner.yMax) - (win.yMin - safe.yMin)) / scale, Is.LessThan(60f), $"{name}: the space above and below the panel is balanced");

            // the title and every other text inside the panel, away from the rim
            Rect hull = Rect.MinMaxRect(win.xMin, win.yMin, win.xMax, banner.yMax);
            foreach (TMP_Text text in window.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                Rect rendered = TextRect(text);
                Assert.That(Inside(hull, rendered), Is.True, $"{name}: '{text.text}' stays inside the panel");
                bool inBanner = text.transform.IsChildOf(bannerRect);
                bool inButton = text.GetComponentInParent<Button>() != null;
                if (inBanner)
                {
                    Assert.That(Inside(banner, rendered), Is.True, $"{name}: the title '{text.text}' stays inside its banner");
                }
                else if (!inButton)
                {
                    Assert.That((win.xMax - rendered.xMax) / scale, Is.GreaterThanOrEqualTo(55f), $"{name}: '{text.text}' keeps its distance to the right rim");
                    Assert.That((rendered.xMin - win.xMin) / scale, Is.GreaterThanOrEqualTo(55f), $"{name}: '{text.text}' keeps its distance to the left rim");
                }
            }

            // the buttons: full touch size, inside the panel and the safe area, centred, even gaps
            Rect p = ScreenRect((RectTransform)primary.transform), l = ScreenRect((RectTransform)left.transform), r = ScreenRect((RectTransform)right.transform);
            foreach ((Button b, Rect rect) in new[] { (primary, p), (left, l), (right, r) })
            {
                Assert.That(rect.height / scale, Is.GreaterThanOrEqualTo(MinTouchUnits - 0.5f), $"{name}: {b.name} is at least 48 dp high");
                Assert.That(rect.width / scale, Is.GreaterThanOrEqualTo(MinTouchUnits), $"{name}: {b.name} is at least 48 dp wide");
                Assert.That(Inside(win, rect) && Inside(safe, rect), Is.True, $"{name}: {b.name} is inside the panel and the safe area");
            }

            Assert.That(p.center.x, Is.EqualTo(win.center.x).Within(2f), $"{name}: the main button is centred");
            Assert.That((p.center.x - (l.center.x + r.center.x) * 0.5f), Is.EqualTo(0f).Within(2f), $"{name}: the two other buttons sit symmetrically under it");
            Assert.That(l.center.y, Is.EqualTo(r.center.y).Within(1f), $"{name}: the two other buttons are on one line");
            Assert.That((r.xMin - l.xMax) / scale, Is.InRange(24f, 28f), $"{name}: 26 units between STAY / AREA SELECTION (or AREA SELECTION / MAIN MENU)");
            Assert.That((p.yMin - Mathf.Max(l.yMax, r.yMax)) / scale, Is.InRange(24f, 28f), $"{name}: 26 units between the main button and the two others");
            Assert.That(p.width, Is.GreaterThan(l.width * 1.8f), $"{name}: the main button is much wider than the others");
            Assert.That(p.height, Is.GreaterThan(l.height), $"{name}: and taller");

            // nothing behind the panel takes a touch: a pointer on the HUD's centre-left (the joystick) or bottom right (DIG / COLLECT) hits the panel's dim, never a game control
            foreach (Vector2 probe in new[] { new Vector2(Screen.width * 0.12f, Screen.height * 0.2f), new Vector2(Screen.width * 0.85f, Screen.height * 0.2f) })
            {
                List<RaycastResult> hits = Hits(probe);
                Assert.That(hits.Count, Is.GreaterThan(0), $"{name}: the dim takes the touch");
                Assert.That(hits[0].gameObject.transform.IsChildOf(panelRoot.transform), Is.True, $"{name}: the touch at {probe} is taken by the panel ({hits[0].gameObject.name}), not by a game control");
                Assert.That(hits[0].gameObject.GetComponentInParent<VirtualJoystick>(), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator Layout_AtFiveScreenShapesAndTheIphone_EveryTextStaysInsideItsPanel_TheTitleIsAwayFromTheEdge_AndTheButtonsAreEvenlySpaced()
        {
            gameView = new TestGameView();
            foreach ((string path, string save, string key) in new[] { (GreenPath, GreenSave(), "LEVEL COMPLETE Green Mine"), (RockyPath, RockySave(), "LEVEL COMPLETE Rocky Canyon") })
            {
                yield return OpenLevel(path, save);
                LevelCompleteController level = One<LevelCompleteController>();
                foreach ((int w, int h, bool phone, string shape) in Shapes)
                {
                    phoneInsets = phone;
                    gameView.SetResolution(w, h);
                    yield return null;
                    yield return null;
                    yield return new WaitForSecondsRealtime(0.25f);
                    Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(w, h)));
                    AssertScreen($"{key} @ {shape}", level, level.Window, level.NextButton, level.StayButton, level.AreaSelectionButton, level.PanelRoot);
                }

                yield return Unload();
            }

            foreach ((string save, bool collection, string key) in new[] { (GameSave(), false, "GAME COMPLETE"), (CollectionSave(), true, "100% COLLECTION") })
            {
                yield return OpenFinal(save);
                FinalCelebrationController finals = One<FinalCelebrationController>();
                FinalCelebrationController.Skin skin = collection ? finals.Collection : finals.Game;
                foreach ((int w, int h, bool phone, string shape) in Shapes)
                {
                    phoneInsets = phone;
                    gameView.SetResolution(w, h);
                    yield return null;
                    yield return null;
                    yield return new WaitForSecondsRealtime(0.25f);
                    AssertScreen($"{key} @ {shape}", finals, skin.window, skin.continueButton, skin.areaSelectionButton, skin.mainMenuButton, finals.PanelRoot);
                }

                if (collection)
                {
                    // the three area rows: the counts stand in one column of their own, clear of the names
                    float firstColumn = float.NaN;
                    for (int i = 0; i < skin.areaRows.Length; i++)
                    {
                        TMP_Text row = skin.areaRows[i];
                        row.ForceMeshUpdate();
                        string name = row.text.Substring(0, row.text.IndexOf("<pos"));
                        TMP_TextInfo info = row.textInfo;
                        float nameEnd = row.transform.TransformPoint(info.characterInfo[name.Length - 1].topRight).x;
                        float countStart = row.transform.TransformPoint(new Vector3(info.characterInfo[name.Length].origin, 0f, 0f)).x; // the glyph's origin: '1' and '2' have different side bearings, the column is where the text starts
                        float scale = row.canvas.rootCanvas.scaleFactor;
                        Assert.That((countStart - nameEnd) / scale, Is.GreaterThanOrEqualTo(40f), $"row {i}: the count keeps its distance to the name");
                        if (i == 0)
                        {
                            firstColumn = countStart;
                        }

                        Assert.That(countStart, Is.EqualTo(firstColumn).Within(1.5f), $"row {i}: the counts line up in one column");
                    }

                    // the gold line is a line of its own, below a divider, and the badge shows 60 / 60 inside its coin
                    RectTransform gold = (RectTransform)skin.goldLabel.transform;
                    Assert.That(ScreenRect(gold).yMax, Is.LessThan(ScreenRect((RectTransform)skin.window.Find("Divider")).yMin), "the gold sits under the divider, apart from the three rows");
                    Rect coin = ScreenRect((RectTransform)skin.window.Find("BadgeGroup/Badge"));
                    Assert.That(Inside(coin, TextRect(skin.treasuresLabel)), Is.True, "60 / 60 stays inside the gold coin");
                }

                yield return Unload();
            }
        }

        // ================================================================ 3. real taps: LEVEL COMPLETE

        [UnityTest]
        public IEnumerator RealTaps_LevelComplete_StayHere_GoToNextArea_AndAreaSelection_DoWhatTheyDid()
        {
            // STAY HERE (a real tap): the panel closes, the game goes on, the answer is saved, nothing is paid
            yield return OpenLevel(GreenPath, GreenSave());
            LevelCompleteController level = One<LevelCompleteController>();
            Assert.That(level.StayButton.interactable && level.StayButton.gameObject.activeInHierarchy, Is.True);
            yield return ClickUntil(level.StayButton, () => !level.IsOpen); // real tap
            Assert.That(level.IsOpen, Is.False, "STAY HERE closes the panel");
            Assert.That(GamePause.IsPaused, Is.False, "and the game goes on");
            Assert.That(Saved(store).celebratedAreaIds, Contains.Item("green_mine"), "the answer is saved");
            Assert.That(Saved(store).totalGold, Is.EqualTo(625), "no gold is paid");
            Assert.That(requestedScenes, Is.Empty);
            yield return Unload();

            // GO TO ANCIENT MINE (a real tap): that scene is asked for
            yield return OpenLevel(RockyPath, RockySave());
            level = One<LevelCompleteController>();
            Assert.That(level.NextButton.interactable, Is.True, "the next area is open");
            yield return ClickUntil(level.NextButton, () => requestedScenes.Count > 0); // real tap
            Assert.That(requestedScenes, Is.EqualTo(new[] { SceneNavigator.SceneForArea("ancient_mine") }));
            Assert.That(Saved(store).totalGold, Is.EqualTo(1465));
            yield return Unload();
            requestedScenes.Clear();

            // AREA SELECTION (a real tap): the Main Menu with the Area Selection open
            yield return OpenLevel(RockyPath, RockySave());
            level = One<LevelCompleteController>();
            yield return ClickUntil(level.AreaSelectionButton, () => requestedScenes.Count > 0); // real tap
            Assert.That(requestedScenes.Count, Is.EqualTo(1));
            Assert.That(SceneNavigator.AreaSelectionRequested, Is.True, "the Area Selection is asked for");
        }

        // ================================================================ 4. real taps and Back: GAME COMPLETE and 100% COLLECTION

        [UnityTest]
        public IEnumerator RealTaps_Finals_InOrder_ContinueExploring_Back_AreaSelection_AndMainMenu_DoWhatTheyDid()
        {
            // both owed: GAME COMPLETE first, then (after a real tap on CONTINUE EXPLORING) 100% COLLECTION on the same pause
            yield return OpenFinal(BothSave());
            FinalCelebrationController finals = One<FinalCelebrationController>();
            Assert.That(finals.CurrentAchievement, Is.EqualTo(FinalAchievements.GameComplete), "GAME COMPLETE comes first");
            yield return ClickUntil(finals.Game.continueButton, () => finals.CurrentAchievement == FinalAchievements.Collection); // real tap
            Assert.That(finals.CurrentAchievement, Is.EqualTo(FinalAchievements.Collection), "then 100% COLLECTION");
            Assert.That(finals.ActiveSkin, Is.SameAs(finals.Collection));
            Assert.That(finals.IsOpen && GamePause.IsPaused, Is.True, "the game stays paused between the two");
            Assert.That(Saved(store).celebratedAchievementIds, Is.EqualTo(new[] { FinalAchievements.GameComplete }));
            Assert.That(Saved(store).totalGold, Is.EqualTo(2705), "no gold is paid again");

            // the Android Back key is CONTINUE EXPLORING: the panel closes, the game goes on, the player can keep playing
            yield return new WaitForSecondsRealtime(0.8f);
            yield return PressBack();
            Assert.That(finals.IsOpen, Is.False, "Back closes the panel");
            Assert.That(GamePause.IsPaused, Is.False, "and does not open the Pause Menu");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Saved(store).celebratedAchievementIds, Is.EquivalentTo(new[] { FinalAchievements.GameComplete, FinalAchievements.Collection }));
            Assert.That(Saved(store).pendingAchievementIds, Is.Empty);
            Assert.That(requestedScenes, Is.Empty);
            yield return Unload();

            // MAIN MENU (a real tap)
            yield return OpenFinal(CollectionSave());
            finals = One<FinalCelebrationController>();
            yield return ClickUntil(finals.Collection.mainMenuButton, () => requestedScenes.Count > 0); // real tap
            Assert.That(requestedScenes.Count, Is.EqualTo(1), "MAIN MENU asks for the Main Menu");
            Assert.That(SceneNavigator.AreaSelectionRequested, Is.False, "and not for the Area Selection");
            Assert.That(Saved(store).celebratedAchievementIds, Contains.Item(FinalAchievements.Collection));
            yield return Unload();
            requestedScenes.Clear();

            // AREA SELECTION (a real tap) on GAME COMPLETE
            yield return OpenFinal(GameSave());
            finals = One<FinalCelebrationController>();
            yield return ClickUntil(finals.Game.areaSelectionButton, () => requestedScenes.Count > 0); // real tap
            Assert.That(requestedScenes.Count, Is.EqualTo(1));
            Assert.That(SceneNavigator.AreaSelectionRequested, Is.True, "the Area Selection is asked for");
            Assert.That(Saved(store).totalGold, Is.EqualTo(2640));
        }
    }
}
#endif
