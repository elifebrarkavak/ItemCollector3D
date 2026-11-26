#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
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
    /// Stage 18C: the gameplay HUD and the tutorial, polished. The tutorial card (navy + gold frame, step coin, five dots, title, hint), the notification banners (AREA COMPLETE / NEW AREA UNLOCKED / zone names),
    /// the floating gold reward text, the detector's hint plate and lamp, the same HUD in the three maps. Real input is used for the tutorial steps, DIG, COLLECT and SKIP TUTORIAL (marked "real tap"). In-memory saves only;
    /// the developer's real save file is hashed before the first and after the last test. Colour and contrast are judged on real Game View renders as well (see the stage report), not by these tests alone.
    /// </summary>
    public sealed class HudPolish18cPlayModeTests
    {
        private const string Green = "Assets/Scenes/GreenMine.unity";
        private const string Rocky = "Assets/Scenes/RockyCanyon.unity";
        private const string Ancient = "Assets/Scenes/AncientMine.unity";
        private const string PendingSave = "{\"version\":1,\"totalGold\":0,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[],\"tutorialState\":1}";
        private const string FinishedGreen = "{\"version\":1,\"totalGold\":0,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[],\"tutorialState\":2}";
        private const string RockySave = "{\"version\":1,\"totalGold\":640,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[\"green_mine\"],\"tutorialState\":2}";
        private const string AncientSave = "{\"version\":1,\"totalGold\":2000,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\",\"ancient_mine\"],\"completedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"tutorialState\":2}";

        /// <summary>48 dp on a typical Android phone (1080 px high, 2.625 px per dp) is 126 reference units.</summary>
        private const float MinTouchUnits = 126f;

        private static string realSavePath, realSaveHashBefore;
        private Scene scene;
        private float previousTimeScale;
        private Mouse mouse;
        private Touchscreen touchscreen;
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
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouse18c");
            touchscreen = InputSystem.AddDevice<Touchscreen>("IC3D_TestTouchscreen18c");
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

            if (touchscreen != null)
            {
                InputSystem.RemoveDevice(touchscreen);
                touchscreen = null;
            }

            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehaviour;
            InputSystem.settings.backgroundBehavior = previousBackground;
            SafeAreaFitter.SafeAreaOverride = null;
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
                    type.GetMethod("ShowTab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(window, null);
                    editorWindow.Focus();
                }
            }

            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------ helpers

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

        private IEnumerator LoadScene(string path, string save)
        {
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore(save);
            yield return LoadSceneWith(path);
        }

        private IEnumerator LoadScene(string path, MemoryProgressStore store)
        {
            ProgressManager.StoreFactoryOverride = () => store;
            yield return LoadSceneWith(path);
        }

        private IEnumerator LoadSceneWith(string path)
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.5f);
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

        private RectTransform Named(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.name == name)
                    {
                        return rect;
                    }
                }
            }

            Assert.Fail("Not found: " + name);
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

        private List<TreasureSpot> AllSpots()
        {
            var list = new List<TreasureSpot>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                list.AddRange(root.GetComponentsInChildren<TreasureSpot>(true));
            }

            return list;
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

        /// <summary>Puts the player where the detector reads the given proximity (0 = edge of the detection radius, 1 = inside the dig radius) for this treasure, and lets the HUD follow.</summary>
        private IEnumerator StandAtProximity(TreasureSpot spot, float proximity)
        {
            TreasureDefinition def = spot.Definition;
            float distance = proximity >= 0.999f ? def.DigRadius * 0.6f : def.DetectionRadius - proximity * (def.DetectionRadius - def.DigRadius);
            Teleport(Player, PlaceAtDistance(spot, distance));
            yield return new WaitForSecondsRealtime(0.35f);
            Teleport(Player, PlaceAtDistance(spot, distance));
            yield return new WaitForSecondsRealtime(0.9f);
            Canvas.ForceUpdateCanvases();
        }

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

        /// <summary>The rendered text of a label (after layout and auto-sizing) in screen pixels.</summary>
        private static Rect TextRect(TMP_Text text)
        {
            text.ForceMeshUpdate();
            Bounds b = text.textBounds;
            Vector3 min = text.transform.TransformPoint(b.min);
            Vector3 max = text.transform.TransformPoint(b.max);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
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

        private IEnumerator ClickUntil(Vector2 position, System.Func<bool> done)
        {
            for (int attempt = 0; attempt < 3 && !done(); attempt++)
            {
                if (attempt > 0)
                {
                    yield return RefocusSimulator();
                    Canvas.ForceUpdateCanvases();
                }

                yield return Click(position);
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

        private static void GoTo(TutorialController tutorial, TutorialController.TutorialStep step)
        {
            typeof(TutorialController).GetMethod("GoTo", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tutorial, new object[] { step });
        }

        private static GameProgressData Saved(MemoryProgressStore store) => JsonUtility.FromJson<GameProgressData>(store.Json);

        /// <summary>The hits of a pointer at a screen position, top first.</summary>
        private static List<RaycastResult> Hits(Vector2 position)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            return hits;
        }

        private TMP_Text LabelOf(Component parent, string childName)
        {
            foreach (TMP_Text text in parent.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == childName)
                {
                    return text;
                }
            }

            Assert.Fail($"{parent.name} has no label {childName}");
            return null;
        }

        /// <summary>The card of the running tutorial: inside the safe-area root, clear of every control and every other HUD piece, its texts inside it, and it takes no touch.</summary>
        private void AssertCardLayout(TutorialController tutorial, string when)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform card = tutorial.Card;
            Rect cardRect = ScreenRect(card);
            Rect safe = ScreenRect((RectTransform)tutorial.UiRoot.transform.parent);
            Assert.That(Inside(safe, cardRect), Is.True, $"{when}: the card {cardRect} stays inside the safe area {safe}");
            Assert.That(Inside(cardRect, TextRect(LabelOf(card, "Title"))), Is.True, $"{when}: the title stays inside the card");
            Assert.That(Inside(cardRect, TextRect(LabelOf(card, "Hint"))), Is.True, $"{when}: the hint stays inside the card");
            if (!string.IsNullOrEmpty(LabelOf(card, "Step").text))
            {
                Assert.That(Inside(cardRect, TextRect(LabelOf(card, "Step"))), Is.True, $"{when}: the step label stays inside the card");
            }
            Assert.That(LabelOf(card, "Hint").fontSize, Is.GreaterThanOrEqualTo(26f), $"{when}: the hint is never smaller than 26 units (about 10.5 pt on a 3x phone)");

            // the text keeps a margin at the bottom: the hint's lowest line is well above the card's bottom edge (the 18A card let it touch the edge)
            Assert.That(TextRect(LabelOf(card, "Hint")).yMin - cardRect.yMin, Is.GreaterThan(cardRect.height * 0.06f), $"{when}: room under the hint");

            var controls = new Dictionary<string, RectTransform>
            {
                ["joystick"] = (RectTransform)One<VirtualJoystick>().transform,
                ["pause"] = (RectTransform)FindButton("PauseButton").transform,
                ["gold panel"] = Named("HeaderPanel"),
                ["level plate"] = Named("LevelPlate"),
                ["detector"] = (RectTransform)One<DetectorSignalIndicator>().transform,
                ["DIG"] = One<HudController>().DigButtonRect,
                ["COLLECT"] = One<HudController>().CollectButtonRect,
                ["SKIP TUTORIAL"] = (RectTransform)tutorial.SkipButton.transform,
            };
            foreach (var pair in controls)
            {
                Assert.That(cardRect.Overlaps(ScreenRect(pair.Value)), Is.False, $"{when}: the card does not cover the {pair.Key}");
            }

            var detector = One<DetectorSignalIndicator>();
            if (detector.HintPlate != null && detector.HintPlate.activeInHierarchy)
            {
                Rect plate = ScreenRect((RectTransform)detector.HintPlate.transform);
                Assert.That(cardRect.Overlaps(plate), Is.False, $"{when}: the card does not cover the detector's hint plate");
            }

            foreach (Graphic g in card.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(g.raycastTarget, Is.False, $"{when}: {g.name} of the card takes no touch");
            }
        }

        // ================================================================ the tutorial card with real play

        [UnityTest]
        public IEnumerator TutorialCard_FiveRealSteps_ShowTheCoinDotsAndTheHalo_AndTheGoldIsPaidExactly()
        {
            phoneInsets = true;
            var store = new MemoryProgressStore(PendingSave);
            yield return LoadScene(Green, store);
            TutorialController tutorial = One<TutorialController>();
            var hunt = One<TreasureHuntController>();
            var hud = One<HudController>();
            TreasureSpot spot = Spot("green_mine.mine_01");
            yield return new WaitForSecondsRealtime(0.3f);

            // the card is built from the shared UI kit: the HUD's navy + gold panel, the UI fonts
            Transform card = tutorial.Card;
            Image bg = card.Find("Bg").GetComponent<Image>();
            Assert.That(bg.sprite.name, Is.EqualTo("ui_banner"), "the same panel as the detector and the gold panel");
            Assert.That(bg.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(LabelOf(card, "Title").font.name, Is.EqualTo(LabelOf(Named("LevelPlate"), "LevelTitle").font.name), "the title uses the UI title font");
            Assert.That(LabelOf(card, "Title").fontSharedMaterial.name, Is.EqualTo("UI_Title_Gold"));
            Assert.That(card.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(((RectTransform)card).rect.height, Is.LessThan(176f), "shorter than the 17B plate (176 units)");

            // STEP 1 (MOVE): coin 1, one dot lit, the joystick has the halo
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Move));
            Assert.That(tutorial.BadgeText, Is.EqualTo("1"));
            Assert.That(tutorial.LitPipCount, Is.EqualTo(1));
            Assert.That(LabelOf(card, "Step").text, Is.EqualTo("STEP 1 / 5"));
            AssertCardLayout(tutorial, "MOVE");
            RectTransform joystick = (RectTransform)One<VirtualJoystick>().transform;
            AssertHalo(tutorial, joystick, "MOVE");
            Assert.That(Hits(ScreenCentre(joystick))[0].gameObject.GetComponentInParent<VirtualJoystick>(), Is.Not.Null, "a touch on the joystick reaches the joystick, not the halo");

            // the real joystick drag walks the miner and moves the tutorial on (the card does not block it)
            Vector3 before = Player.position;
            yield return DragJoystick(3f);
            Assert.That(Vector3.Distance(Player.position, before), Is.GreaterThan(3f), "the miner really walked");
            yield return WaitUntil(() => tutorial.Step != TutorialController.TutorialStep.Move, 1f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Look));
            Assert.That(tutorial.BadgeText, Is.EqualTo("2"));
            Assert.That(tutorial.LitPipCount, Is.EqualTo(2));
            Assert.That(tutorial.SwipeHint.gameObject.activeSelf, Is.True);
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.False, "no control to highlight while looking around");
            AssertCardLayout(tutorial, "LOOK");
            Rect swipe = ScreenRect(tutorial.SwipeHint);
            Assert.That(Inside(ScreenRect((RectTransform)tutorial.UiRoot.transform.parent), swipe), Is.True, "the swipe hint stays in the safe area");
            Assert.That(swipe.Overlaps(ScreenRect(tutorial.Card)), Is.False);

            // STEP 2 -> 3 by a real camera drag
            yield return DragCamera(60);
            yield return WaitUntil(() => tutorial.Step != TutorialController.TutorialStep.Look, 1f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Find));
            Assert.That(tutorial.BadgeText, Is.EqualTo("3"));
            Assert.That(tutorial.LitPipCount, Is.EqualTo(3));
            yield return new WaitForSecondsRealtime(0.3f);
            AssertCardLayout(tutorial, "FIND");
            AssertHalo(tutorial, (RectTransform)One<DetectorSignalIndicator>().transform, "FIND");

            // STEP 3 -> 4: RAPID (the detector numbers are the game's own)
            float digRadius = spot.Definition.DigRadius;
            float rapidFrom = spot.Definition.DetectionRadius - DetectorSignalIndicator.RapidFromProximity * (spot.Definition.DetectionRadius - digRadius);
            Teleport(Player, PlaceAtDistance(spot, (digRadius + rapidFrom) * 0.5f));
            yield return WaitUntil(() => tutorial.Step != TutorialController.TutorialStep.Find, 4f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Dig));
            Assert.That(tutorial.BadgeText, Is.EqualTo("4"));
            Assert.That(tutorial.LitPipCount, Is.EqualTo(4));

            // STEP 4: DIG is highlighted and a real tap digs
            Button dig = FindButton("DigButton");
            Teleport(Player, PlaceAtDistance(spot, 1.5f));
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            AssertCardLayout(tutorial, "DIG");
            AssertHalo(tutorial, (RectTransform)dig.transform, "DIG");
            Assert.That(Hits(ScreenCentre((RectTransform)dig.transform))[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(dig), "a touch on DIG reaches DIG, not the halo");
            int gold = hunt.TotalGold;
            yield return ClickUntil(ScreenCentre((RectTransform)dig.transform), () => spot.State == TreasureState.Revealed || hunt.IsDigging); // real tap
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Collect));
            Assert.That(tutorial.BadgeText, Is.EqualTo("5"));
            Assert.That(tutorial.LitPipCount, Is.EqualTo(5));

            // STEP 5: COLLECT is highlighted and a real tap pays the gold
            Button collect = FindButton("CollectButton");
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            AssertCardLayout(tutorial, "COLLECT");
            AssertHalo(tutorial, (RectTransform)collect.transform, "COLLECT");
            var reward = One<FloatingRewardText>();
            yield return ClickUntil(ScreenCentre((RectTransform)collect.transform), () => spot.State == TreasureState.Collected); // real tap
            yield return WaitUntil(() => spot.State == TreasureState.Collected, 3f);
            Assert.That(hunt.TotalGold, Is.EqualTo(gold + spot.Definition.GoldValue), "the gold is paid exactly as in a normal game");

            // TUTORIAL COMPLETE: the same card, the coin shows a check mark, every dot is lit
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Complete));
            Assert.That(tutorial.CurrentTitle, Is.EqualTo("TUTORIAL COMPLETE!"));
            Assert.That(tutorial.BadgeShowsCheck, Is.True);
            Assert.That(tutorial.BadgeText, Is.Empty);
            Assert.That(tutorial.LitPipCount, Is.EqualTo(5));
            Assert.That(tutorial.SkipButton.gameObject.activeSelf, Is.False, "nothing to skip any more");
            AssertCardLayout(tutorial, "COMPLETE");

            // the gold reward text shows the real gold, and the HUD agrees
            Assert.That(reward.CurrentMessage, Does.Contain(spot.Definition.GoldValue.ToString()), "the floating text shows the gold of this treasure");
            Assert.That(One<RewardFeedbackPresenter>().LastGold, Is.EqualTo(spot.Definition.GoldValue));
            Assert.That(Regex.IsMatch(LabelOf(Named("HeaderPanel"), "GoldText").text, $@"^{hunt.TotalGold} / \d+ GOLD$"), Is.True, "the HUD gold is the real total");
            Assert.That(Saved(store).totalGold, Is.EqualTo(gold + spot.Definition.GoldValue));
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Finished));

            // the save has no field that the game did not have before this stage
            var known = new HashSet<string>(KeysOf(JsonUtility.ToJson(new GameProgressData())));
            foreach (string key in KeysOf(store.Json))
            {
                Assert.That(known, Contains.Item(key), "the save has only the fields of the game's own save format: " + key);
            }

            yield return new WaitForSecondsRealtime(TutorialController.CompleteShowSeconds + 0.5f);
            Assert.That(tutorial.IsRunning, Is.False);
            Assert.That(tutorial.UiRoot.activeInHierarchy, Is.False, "the card goes away by itself");
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.False);
        }

        private static string[] KeysOf(string json)
        {
            var keys = new List<string>();
            foreach (Match m in Regex.Matches(json, "\"(\\w+)\":"))
            {
                keys.Add(m.Groups[1].Value);
            }

            return keys.ToArray();
        }

        /// <summary>The halo sits behind the control (first child, so every HUD control stays on top), centred on it, takes no touch and stays small enough not to cover the screen.</summary>
        private void AssertHalo(TutorialController tutorial, RectTransform target, string when)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform halo = tutorial.Highlight;
            Assert.That(halo.gameObject.activeSelf, Is.True, $"{when}: the control is highlighted");
            Assert.That(halo.GetSiblingIndex(), Is.EqualTo(0), $"{when}: the halo is behind every HUD control");
            Assert.That(halo.GetComponent<Image>().raycastTarget, Is.False, $"{when}: the halo takes no touch");
            Rect targetRect = ScreenRect(target);
            Rect haloRect = ScreenRect(halo);
            Assert.That(Vector2.Distance(haloRect.center, targetRect.center), Is.LessThan(targetRect.height * 0.1f), $"{when}: the halo is centred on the control");
            Assert.That(haloRect.width, Is.LessThan(targetRect.width * 2.2f), $"{when}: the halo stays close to the control");
            Assert.That(haloRect.height * haloRect.width, Is.LessThan(Screen.width * Screen.height * 0.2f), $"{when}: the halo covers a small part of the screen");
        }

        // ================================================================ SKIP TUTORIAL

        [UnityTest]
        public IEnumerator SkipTutorial_ByARealTap_WorksOnTheButtonAndOnItsTouchPad_AndHasAFullTouchSize()
        {
            var store = new MemoryProgressStore(PendingSave);
            yield return LoadScene(Green, store);
            TutorialController tutorial = One<TutorialController>();
            var canvas = tutorial.UiRoot.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform skip = (RectTransform)tutorial.SkipButton.transform;
            Rect root = ScreenRect(skip);
            Rect face = ScreenRect((RectTransform)skip.Find("Face"));
            Assert.That(root.height / canvas.scaleFactor, Is.GreaterThanOrEqualTo(MinTouchUnits - 0.5f), "the touch area of SKIP TUTORIAL is at least 48 dp");
            Assert.That(face.height, Is.LessThan(root.height), "the visible button is smaller than its touch area");
            Assert.That(tutorial.SkipButton.gameObject.activeInHierarchy, Is.True, "SKIP TUTORIAL is on screen at STEP 1");

            // a real tap on the pad above the visible button (inside the touch area, outside the face) skips
            Vector2 padPoint = new Vector2(root.center.x, (root.yMax + face.yMax) * 0.5f);
            Assert.That(face.Contains(padPoint), Is.False);
            Assert.That(root.Contains(padPoint), Is.True);
            yield return ClickUntil(padPoint, () => !tutorial.IsRunning);
            Assert.That(tutorial.IsRunning, Is.False, "a tap on the touch pad skips the tutorial");
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Finished));
            Assert.That(tutorial.UiRoot.activeInHierarchy, Is.False);
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.False);
            Assert.That(Saved(store).totalGold, Is.EqualTo(0), "skipping changes nothing else");
            yield return Unload();

            // and on the button itself, from another step (LOOK)
            var store2 = new MemoryProgressStore(PendingSave);
            yield return LoadScene(Green, store2);
            tutorial = One<TutorialController>();
            yield return DragJoystick(3f);
            yield return WaitUntil(() => tutorial.Step == TutorialController.TutorialStep.Look, 1f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Look));
            yield return ClickUntil(ScreenCentre((RectTransform)tutorial.SkipButton.transform), () => !tutorial.IsRunning); // real tap
            Assert.That(tutorial.IsRunning, Is.False);
            Assert.That(Saved(store2).tutorialState, Is.EqualTo((int)TutorialStatus.Finished));
        }

        // ================================================================ pause, resume, modal

        [UnityTest]
        public IEnumerator PauseAndModal_HideTheWholeTutorial_TheHudTakesNoTouch_AndResumeBringsEverythingBack()
        {
            yield return LoadScene(Green, PendingSave);
            TutorialController tutorial = One<TutorialController>();
            var plateGroup = tutorial.Card.GetComponent<CanvasGroup>();
            RectTransform joystick = (RectTransform)One<VirtualJoystick>().transform;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(plateGroup.alpha, Is.EqualTo(1f));
            Assert.That(tutorial.SkipButton.gameObject.activeSelf, Is.True);

            // a celebration (modal) pauses the game: the HUD is hidden and takes no touch, the tutorial shows nothing, SKIP TUTORIAL is gone too
            GamePause.Pause();
            GamePause.SetModalOpen(true);
            yield return new WaitForSecondsRealtime(0.3f);
            Canvas.ForceUpdateCanvases();
            Assert.That(plateGroup.alpha, Is.EqualTo(0f), "nothing of the card behind a modal");
            Assert.That(tutorial.SkipButton.gameObject.activeSelf, Is.False, "SKIP TUTORIAL is not shown (and not tappable) behind a modal");
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.False);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Move), "the step does not change while paused");
            foreach (HideWhilePaused hidden in FindObjectsOf<HideWhilePaused>())
            {
                if (hidden.isActiveAndEnabled)
                {
                    Assert.That(hidden.IsHidden, Is.True, hidden.name + " is hidden");
                }
            }

            foreach (RaycastResult hit in Hits(ScreenCentre(joystick)))
            {
                Assert.That(hit.gameObject.GetComponentInParent<VirtualJoystick>(), Is.Null, "the joystick takes no touch behind a modal");
            }

            foreach (RaycastResult hit in Hits(ScreenCentre((RectTransform)tutorial.SkipButton.transform)))
            {
                Assert.That(hit.gameObject.GetComponentInParent<Button>() == tutorial.SkipButton, Is.False, "no tap reaches SKIP TUTORIAL behind a modal");
            }

            // closing it brings every control back and the real controls work again
            GamePause.SetModalOpen(false);
            GamePause.Resume();
            yield return new WaitForSecondsRealtime(0.4f);
            Canvas.ForceUpdateCanvases();
            Assert.That(plateGroup.alpha, Is.EqualTo(1f));
            Assert.That(tutorial.SkipButton.gameObject.activeSelf, Is.True);
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.True, "the joystick is highlighted again");
            Assert.That(Hits(ScreenCentre(joystick))[0].gameObject.GetComponentInParent<VirtualJoystick>(), Is.Not.Null, "the joystick takes touches again");
            Vector3 before = Player.position;
            yield return DragJoystick(3f);
            Assert.That(Vector3.Distance(Player.position, before), Is.GreaterThan(3f), "the miner walks after the pause");
            yield return WaitUntil(() => tutorial.Step == TutorialController.TutorialStep.Look, 1f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Look));
        }

        private T[] FindObjectsOf<T>() where T : Component
        {
            var list = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                list.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return list.ToArray();
        }

        // ================================================================ the three maps: same HUD, readable detector, firm reward text

        [UnityTest]
        public IEnumerator ThreeMaps_ShareOneHud_TheDetectorSaysSlowFastRapid_AndTheHintIsReadable()
        {
            var layouts = new List<Dictionary<string, string>>();
            int totalGold = 0;
            foreach ((string path, string save, string key) in new[] { (Green, FinishedGreen, "green"), (Rocky, RockySave, "rocky"), (Ancient, AncientSave, "ancient") })
            {
                yield return LoadScene(path, save);
                var indicator = One<DetectorSignalIndicator>();
                var label = LabelOf(indicator, "SignalLabel");

                // the same HUD: pieces, sizes, positions, fonts
                var layout = new Dictionary<string, string>();
                foreach (string name in new[] { "LevelPlate", "HeaderPanel", "ActionButtons", "DetectorIndicator", "MovementJoystick", "LevelTitle", "GoldText", "SignalLabel", "RewardText", "PauseButton" })
                {
                    RectTransform rect = Named(name);
                    var text = rect.GetComponent<TMP_Text>();
                    layout[name] = $"{rect.anchorMin} {rect.anchorMax} {rect.anchoredPosition} {rect.sizeDelta}" + (text != null ? $" {text.font.name} {text.fontSharedMaterial.name}" : string.Empty); // the font size is not compared: it auto-sizes to the text
                }

                layouts.Add(layout);
                if (layouts.Count > 1)
                {
                    foreach (var pair in layout)
                    {
                        Assert.That(pair.Value, Is.EqualTo(layouts[0][pair.Key]), $"{key}: {pair.Key} is the same as in Green Mine");
                    }
                }

                // the detector: SLOW, FAST, RAPID at the proximities of the game's own numbers (constants untouched)
                Assert.That(DetectorSignalIndicator.FastFromProximity, Is.EqualTo(0.4f));
                Assert.That(DetectorSignalIndicator.RapidFromProximity, Is.EqualTo(0.75f));
                Assert.That(DetectorSignalIndicator.LabelFor(DetectorSignalIndicator.SignalLevel.Slow), Is.EqualTo("SLOW"));
                Assert.That(DetectorSignalIndicator.LabelFor(DetectorSignalIndicator.SignalLevel.Fast), Is.EqualTo("FAST"));
                Assert.That(DetectorSignalIndicator.LabelFor(DetectorSignalIndicator.SignalLevel.Rapid), Is.EqualTo("RAPID"));

                foreach (TreasureSpot s in AllSpots())
                {
                    totalGold += s.Definition.GoldValue;
                }

                TreasureSpot target = null;
                float best = float.MaxValue;
                foreach (TreasureSpot s in AllSpots())
                {
                    float d = (s.transform.position - Player.position).sqrMagnitude;
                    if (s.State == TreasureState.Hidden && d < best)
                    {
                        best = d;
                        target = s;
                    }
                }

                foreach (TreasureSpot other in AllSpots())
                {
                    if (other != target)
                    {
                        other.gameObject.SetActive(false); // test only: the detector reads this one treasure, whatever else is near
                    }
                }

                foreach ((float proximity, string text, int bars) in new[] { (0.2f, "SLOW", 1), (0.55f, "FAST", 2), (0.85f, "RAPID", 3) })
                {
                    yield return StandAtProximity(target, proximity);
                    Assert.That(label.text, Is.EqualTo(text), $"{key}: the detector says {text} at proximity {proximity}");
                    Assert.That(indicator.LitBarCount, Is.EqualTo(bars), $"{key}: {bars} bars are lit for {text}");
                    Assert.That(label.rectTransform.rect.width, Is.GreaterThanOrEqualTo(TextRect(label).width / label.canvas.rootCanvas.scaleFactor - 1f), $"{key}: {text} fits its label");
                }

                // RAPID but not inside the dig range: the hint on its plate, large and inside the screen; the plate follows the detector and never grows into what is below it
                Assert.That(indicator.Hint, Is.EqualTo(DetectorSignalIndicator.GetCloserHint));
                Assert.That(indicator.HintPlate.activeInHierarchy, Is.True, $"{key}: the hint has its plate");
                Assert.That(indicator.HintText.fontSize, Is.GreaterThanOrEqualTo(38f), $"{key}: GET CLOSER TO DIG is large");
                Rect plateRect = ScreenRect((RectTransform)indicator.HintPlate.transform);
                Rect detectorRect = ScreenRect((RectTransform)indicator.transform);
                Assert.That(Inside(plateRect, TextRect(indicator.HintText)), Is.True, $"{key}: the hint text stays on its plate");
                Assert.That(plateRect.Overlaps(detectorRect), Is.False, $"{key}: the plate hangs under the detector");
                Assert.That(plateRect.center.x, Is.EqualTo(detectorRect.center.x).Within(2f), $"{key}: centred under the detector");
                Rect safe = ScreenRect((RectTransform)indicator.transform.parent);
                Assert.That(Inside(safe, plateRect), Is.True, $"{key}: the hint plate is inside the safe area");
                Assert.That(plateRect.Overlaps(ScreenRect(Named("HeaderPanel"))), Is.False);
                Assert.That(plateRect.Overlaps(ScreenRect(Named("LevelPlate"))), Is.False);
                Assert.That(plateRect.height / label.canvas.rootCanvas.scaleFactor, Is.LessThan(70f), $"{key}: the plate is compact (62 units)");

                // the lamp blinks with the pulses: its glow lights up at some moment (and rests at 0 between pulses)
                float seenGlow = 0f;
                int pulses = indicator.PulseCount;
                float end = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < end && seenGlow < 0.2f)
                {
                    seenGlow = Mathf.Max(seenGlow, indicator.BeaconGlow.color.a);
                    yield return null;
                }

                Assert.That(indicator.PulseCount, Is.GreaterThan(pulses), $"{key}: the detector pulses");
                Assert.That(seenGlow, Is.GreaterThan(0.2f), $"{key}: the lamp glow lights up with a pulse");

                // inside the dig range the hint goes away
                yield return StandAtProximity(target, 1f);
                Assert.That(indicator.Hint, Is.Empty, $"{key}: no hint inside the dig range");
                Assert.That(indicator.HintPlate.activeSelf, Is.False, $"{key}: and no empty plate");

                // the floating reward text: the UI font with a firm dark outline and a soft dark glow (readable on a bright path, wood, crystal and a dark cave)
                var reward = One<FloatingRewardText>();
                var rewardText = reward.GetComponent<TMP_Text>();
                Material mat = rewardText.fontSharedMaterial;
                Material title = AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/Fonts/UI_Title_Gold.mat");
                Assert.That(mat.name, Is.EqualTo("UI_Reward_Gold"), $"{key}: the reward text has its own material");
                Assert.That(rewardText.font.name, Is.EqualTo(LabelOf(Named("LevelPlate"), "LevelTitle").font.name));
                Assert.That(mat.GetFloat("_OutlineWidth"), Is.GreaterThanOrEqualTo(title.GetFloat("_OutlineWidth") * 1.3f), $"{key}: a firmer outline than the titles");
                Assert.That(mat.IsKeywordEnabled("UNDERLAY_ON"), Is.True, $"{key}: with a soft shadow glow");
                Assert.That(mat.GetColor("_UnderlayColor").a, Is.GreaterThanOrEqualTo(0.5f));
                Assert.That(mat.GetColor("_OutlineColor").r + mat.GetColor("_OutlineColor").g + mat.GetColor("_OutlineColor").b, Is.LessThan(0.6f), $"{key}: the outline is dark");
                Assert.That(rewardText.fontSize * reward.ScaleFor(1f), Is.LessThanOrEqualTo(100f), $"{key}: the text size is not exaggerated");
                reward.Show("+10 GOLD", new Color(1f, 0.85f, 0.25f), 0.3f);
                yield return new WaitForSecondsRealtime(0.4f);
                Canvas.ForceUpdateCanvases();
                Assert.That(reward.CurrentMessage, Is.EqualTo("+10 GOLD"));
                Assert.That(Inside(safe, TextRect(rewardText)), Is.True, $"{key}: the gold text does not leave the screen");

                yield return Unload();
            }

            // the economy is untouched: the three maps pay 2705 and the goal is 2500
            Assert.That(totalGold, Is.EqualTo(2705), "the three maps can still pay 2705 gold");
            Assert.That(FinalAchievements.Goal, Is.EqualTo(2500));
        }

        // ================================================================ banners

        [UnityTest]
        public IEnumerator Banners_AreOneDesign_InAllMaps_FitLongNames_AndNeverOverlapEachOther()
        {
            foreach ((string path, string save, string key) in new[] { (Green, FinishedGreen, "green"), (Rocky, RockySave, "rocky"), (Ancient, AncientSave, "ancient") })
            {
                yield return LoadScene(path, save);
                var presenter = One<ProgressNotificationPresenter>();
                NotificationBanner progress = presenter.GetComponentInChildren<NotificationBanner>(true);
                var notice = Object.FindAnyObjectByType<ZoneEntryNotice>(FindObjectsInactive.Include);
                var banners = new List<(string name, NotificationBanner banner)> { ("progress", progress) };
                if (path != Green)
                {
                    Assert.That(notice, Is.Not.Null, key + " has the zone-name banner");
                    banners.Add(("zone", notice.Banner));
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/Fonts/UI_Title_Gold.mat");
                Rect safe = ScreenRect((RectTransform)presenter.transform.parent);
                foreach ((string name, NotificationBanner banner) in banners)
                {
                    Transform t = banner.transform;
                    Assert.That(t.Find("Background").GetComponent<Image>().sprite.name, Is.EqualTo("ui_banner"), $"{key}/{name}: the same panel as the HUD");
                    Assert.That(t.Find("Shadow"), Is.Not.Null, $"{key}/{name}: a soft shadow like the other panels");
                    Assert.That(t.Find("Edge"), Is.Null, $"{key}/{name}: no flat brown rectangle with a hard edge");
                    var title = t.Find("Title").GetComponent<TMP_Text>();
                    Assert.That(title.fontSharedMaterial.name, Is.EqualTo(material.name), $"{key}/{name}: the title font of the UI");
                    Assert.That(title.font.name, Is.EqualTo(LabelOf(Named("LevelPlate"), "LevelTitle").font.name));
                    Assert.That(banner.GetComponent<CanvasGroup>().blocksRaycasts, Is.False, $"{key}/{name}: a banner takes no touch");
                }

                // the real notifications of the game, in order, with the right words; each one inside the safe area with its text inside the panel
                if (path == Green || path == Rocky)
                {
                    ProgressManager manager = Object.FindAnyObjectByType<ProgressManager>();
                    manager.Service.RegisterCollected("note.18c", path == Green ? 500 : 600); // Green: 500 = complete + Rocky unlocks; Rocky: 640 + 600 crosses 1200 = complete + Ancient unlocks
                    string area = path == Green ? "GREEN MINE" : "ROCKY CANYON";
                    string next = path == Green ? "ROCKY CANYON" : "ANCIENT MINE";
                    yield return WaitUntil(() => progress.IsShowing, 3f);
                    Assert.That(progress.CurrentTitle, Is.EqualTo("AREA COMPLETE!"));
                    Assert.That(progress.CurrentSubtitle, Is.EqualTo(area));
                    yield return new WaitForSecondsRealtime(0.5f);
                    AssertBannerFits(progress, safe, key + "/AREA COMPLETE");
                    yield return WaitUntil(() => !progress.IsShowing, 4f);
                    yield return WaitUntil(() => progress.IsShowing, 3f);
                    Assert.That(progress.CurrentTitle, Is.EqualTo("NEW AREA UNLOCKED!"));
                    Assert.That(progress.CurrentSubtitle, Is.EqualTo(next));
                    yield return new WaitForSecondsRealtime(0.5f);
                    AssertBannerFits(progress, safe, key + "/NEW AREA UNLOCKED");
                    yield return WaitUntil(() => !progress.IsShowing, 4f);
                }

                // a very long name still stays inside the panel (the subtitle shrinks to fit)
                progress.Show("NEW AREA UNLOCKED!", "THE VERY LONG NAME OF A FAR AWAY ANCIENT MINE", new Color(0.55f, 0.9f, 0.55f));
                yield return new WaitForSecondsRealtime(0.5f);
                AssertBannerFits(progress, safe, key + "/long name");
                yield return WaitUntil(() => !progress.IsShowing, 4f);

                if (path != Green)
                {
                    // the zone name: the longest names of the maps fit on one line; shown together with the progress banner (which the real notice never allows) the two still do not overlap
                    foreach (string name in new[] { "FORGOTTEN WORKSHOP", "CANYON OVERLOOK", "DESERT OUTPOST", "ANCIENT GATE", "HIDDEN RAVINE" })
                    {
                        notice.Banner.Show(name, string.Empty, new Color(0.98f, 0.86f, 0.55f));
                        yield return new WaitForSecondsRealtime(0.4f);
                        AssertBannerFits(notice.Banner, safe, key + "/zone " + name);
                    }

                    progress.Show("AREA COMPLETE!", "ROCKY CANYON", new Color(0.95f, 0.71f, 0.2f));
                    notice.Banner.Show("CANYON OVERLOOK", string.Empty, new Color(0.98f, 0.86f, 0.55f));
                    yield return new WaitForSecondsRealtime(0.45f);
                    Canvas.ForceUpdateCanvases();
                    Rect zoneRect = ScreenRect((RectTransform)notice.Banner.transform);
                    Rect progressRect = ScreenRect((RectTransform)progress.transform);
                    Assert.That(zoneRect.Overlaps(progressRect), Is.False, $"{key}: the zone banner and the progress banner do not overlap");
                    var indicator = One<DetectorSignalIndicator>();
                    Assert.That(zoneRect.Overlaps(ScreenRect((RectTransform)indicator.transform)), Is.False);
                    yield return StandAtProximity(AllSpots()[0], 0.85f);
                    if (indicator.HintPlate.activeInHierarchy)
                    {
                        Assert.That(zoneRect.Overlaps(ScreenRect((RectTransform)indicator.HintPlate.transform)), Is.False, $"{key}: the zone banner is below the detector's hint plate");
                    }

                    yield return WaitUntil(() => !progress.IsShowing && !notice.Banner.IsShowing, 5f);
                }
                else
                {
                    // Green Mine: the tutorial card and the progress banner never overlap either
                    yield return Unload();
                    yield return LoadScene(Green, PendingSave);
                    var tutorial = One<TutorialController>();
                    NotificationBanner green = One<ProgressNotificationPresenter>().GetComponentInChildren<NotificationBanner>(true);
                    green.Show("AREA COMPLETE!", "GREEN MINE", new Color(0.95f, 0.71f, 0.2f));
                    yield return new WaitForSecondsRealtime(0.45f);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(ScreenRect(tutorial.Card).Overlaps(ScreenRect((RectTransform)green.transform)), Is.False, "the tutorial card and the progress banner do not overlap");
                    yield return WaitUntil(() => !green.IsShowing, 4f);
                }

                yield return Unload();
            }
        }

        private void AssertBannerFits(NotificationBanner banner, Rect safe, string when)
        {
            Canvas.ForceUpdateCanvases();
            Rect panel = ScreenRect((RectTransform)banner.transform.Find("Background"));
            Assert.That(Inside(safe, panel), Is.True, $"{when}: the banner {panel} is inside the safe area {safe}");
            foreach (string name in new[] { "Title", "Subtitle" })
            {
                Transform part = banner.transform.Find(name);
                if (part == null || !part.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var text = part.GetComponent<TMP_Text>();
                if (string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                Rect rendered = TextRect(text);
                // the banner pops in with a scale animation: compare in the banner's own units
                Assert.That(Inside(panel, rendered), Is.True, $"{when}: the {name} '{text.text}' {rendered} stays inside the panel {panel}");
            }
        }

        // ================================================================ aspect ratios, iPhone safe area (last: it resizes the Game View)

        [UnityTest]
        public IEnumerator FiveAspectRatiosAndTheIphone_TheTutorialAndTheHudNeverOverflowOrOverlap()
        {
            gameView = new TestGameView();
            yield return LoadScene(Green, PendingSave);
            TutorialController tutorial = One<TutorialController>();
            var hud = One<HudController>();
            var indicator = One<DetectorSignalIndicator>();
            yield return StandAtProximity(Spot("green_mine.mine_01"), 0.85f); // RAPID, not in range: the detector's hint plate is shown

            foreach ((int w, int h, bool phone, string name) in new[]
            {
                (1600, 1200, false, "4:3"), (1920, 1080, false, "16:9"), (2340, 1080, false, "19.5:9"), (2400, 1080, false, "20:9"), (2520, 1080, false, "21:9"), (2796, 1290, true, "iPhone 15 Pro Max"),
            })
            {
                phoneInsets = phone;
                gameView.SetResolution(w, h);
                yield return null;
                yield return null;
                yield return null;
                Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(w, h)), name + ": the Game View has this size");
                Canvas.ForceUpdateCanvases();
                Rect safe = ScreenRect((RectTransform)tutorial.UiRoot.transform.parent);
                var canvas = tutorial.UiRoot.GetComponentInParent<Canvas>().rootCanvas;
                if (phone)
                {
                    Assert.That(safe.xMin, Is.GreaterThanOrEqualTo(w * 0.0633f - 1f), "iPhone: the safe-area root keeps the left inset");
                    Assert.That(safe.xMax, Is.LessThanOrEqualTo(w * (1f - 0.0633f) + 1f), "iPhone: and the right inset");
                    Assert.That(safe.yMin, Is.GreaterThanOrEqualTo(h * 0.0488f - 1f), "iPhone: and the bottom inset (home indicator)");
                }

                foreach (TutorialController.TutorialStep step in new[]
                {
                    TutorialController.TutorialStep.Move, TutorialController.TutorialStep.Look, TutorialController.TutorialStep.Find, TutorialController.TutorialStep.Dig,
                    TutorialController.TutorialStep.Collect, TutorialController.TutorialStep.Complete,
                })
                {
                    GoTo(tutorial, step);
                    hud.SetDigState(step == TutorialController.TutorialStep.Dig);
                    hud.SetCollectState(step == TutorialController.TutorialStep.Collect);
                    yield return null;
                    yield return null;
                    yield return new WaitForSecondsRealtime(0.25f);
                    AssertCardLayout(tutorial, $"{name} / {step}");
                    Rect skipRect = ScreenRect((RectTransform)tutorial.SkipButton.transform);
                    if (step != TutorialController.TutorialStep.Complete)
                    {
                        Assert.That(tutorial.SkipButton.gameObject.activeInHierarchy, Is.True, $"{name} / {step}: SKIP TUTORIAL is visible");
                        Assert.That(Inside(safe, skipRect), Is.True, $"{name} / {step}: SKIP TUTORIAL stays in the safe area");
                        Assert.That(skipRect.height / canvas.scaleFactor, Is.GreaterThanOrEqualTo(MinTouchUnits - 0.5f), $"{name} / {step}: SKIP TUTORIAL keeps its 48 dp touch size");
                        Assert.That(skipRect.Overlaps(ScreenRect((RectTransform)One<VirtualJoystick>().transform)), Is.False, $"{name} / {step}: SKIP TUTORIAL is clear of the joystick");
                        Assert.That(skipRect.Overlaps(ScreenRect(hud.DigButtonRect)), Is.False);
                        Assert.That(skipRect.Overlaps(ScreenRect(hud.CollectButtonRect)), Is.False);
                    }

                    if (step == TutorialController.TutorialStep.Look)
                    {
                        Rect swipe = ScreenRect(tutorial.SwipeHint);
                        Assert.That(Inside(safe, swipe), Is.True, $"{name}: the swipe hint stays in the safe area");
                        Assert.That(swipe.Overlaps(ScreenRect(tutorial.Card)) || swipe.Overlaps(skipRect), Is.False);
                    }

                    if (step == TutorialController.TutorialStep.Dig || step == TutorialController.TutorialStep.Collect || step == TutorialController.TutorialStep.Move)
                    {
                        RectTransform action = step == TutorialController.TutorialStep.Dig ? hud.DigButtonRect : step == TutorialController.TutorialStep.Collect ? hud.CollectButtonRect : (RectTransform)One<VirtualJoystick>().transform;
                        Assert.That(Inside(safe, ScreenRect(action)), Is.True, $"{name} / {step}: the control to use is inside the safe area");
                        Rect actionRect = ScreenRect(action);
                        Assert.That(actionRect.height / canvas.scaleFactor, Is.GreaterThanOrEqualTo(MinTouchUnits), $"{name} / {step}: the control keeps its 48 dp size");
                    }
                }

                // the HUD pieces and the detector texts at this ratio
                foreach (TMP_Text text in new[] { LabelOf(Named("LevelPlate"), "LevelTitle"), LabelOf(Named("HeaderPanel"), "GoldText"), LabelOf(indicator, "SignalLabel"), indicator.HintText })
                {
                    Rect parentRect = ScreenRect((RectTransform)text.transform.parent);
                    Assert.That(Inside(parentRect, TextRect(text)), Is.True, $"{name}: '{text.text}' stays inside its panel");
                }

                Assert.That(indicator.HintPlate.activeInHierarchy, Is.True, $"{name}: the detector's hint is on screen");
                Assert.That(Inside(safe, ScreenRect((RectTransform)indicator.HintPlate.transform)), Is.True, $"{name}: the hint plate is inside the safe area");
                Assert.That(Inside(safe, ScreenRect((RectTransform)indicator.transform)), Is.True, $"{name}: the detector is inside the safe area");
                Assert.That(Inside(safe, ScreenRect(Named("HeaderPanel"))), Is.True, $"{name}: the gold panel is inside the safe area");
            }
        }
    }
}
#endif
