#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
    /// The Green Mine tutorial (stage 17B): it starts only for a NEW GAME save (Pending) in Green Mine, teaches MOVE, LOOK, FIND (detector), DIG and COLLECT through what the player really does, never locks
    /// the game, can be skipped at any time, is recorded as finished (never shown again until the next NEW GAME), is not shown for older saves, stays inside the safe area and out of the way of the controls.
    /// Real input is used wherever it is cheap: a finger dragging the joystick, a mouse drag for the camera, real taps on DIG, COLLECT and SKIP TUTORIAL (marked "real tap" below); the Settings
    /// dialog buttons of the NEW GAME test are invoked directly (NOT a real tap). In-memory saves only; the developer's real save file is hashed before and after.
    /// </summary>
    public sealed class TutorialPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";
        private const string PendingSave = "{\"version\":1,\"totalGold\":0,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[],\"tutorialState\":1}";
        private const string OldSave = "{\"version\":1,\"totalGold\":90,\"collectedTreasureIds\":[\"green_mine.gold_coin_01\"],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[]}";
        private const string FinishedSave = "{\"version\":1,\"totalGold\":0,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[],\"tutorialState\":2}";

        private static string realSavePath, realSaveHashBefore;
        private Scene scene;
        private float previousTimeScale;
        private Mouse mouse;
        private Touchscreen touchscreen;
        private InputSettings.EditorInputBehaviorInPlayMode previousBehaviour;
        private InputSettings.BackgroundBehavior previousBackground;

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
            SafeAreaFitter.SafeAreaOverride = () => new Rect(0f, 0f, Screen.width, Screen.height);
            previousBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; // stage 18C: the UI input module drops pointer events while the Editor is unfocused (test run only, restored below)
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouseTutorial");
            touchscreen = InputSystem.AddDevice<Touchscreen>("IC3D_TestTouchscreenTutorial");
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
            GamePause.Resume();
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

        // ------------------------------------------------------------------ helpers

        private IEnumerator LoadScene(MemoryProgressStore store)
        {
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            Physics.SyncTransforms();
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
            foreach (TreasureSpot spot in One<TreasureHuntController>().transform.root.GetComponentsInChildren<TreasureSpot>(true))
            {
                if (spot.SpotId == id)
                {
                    return spot;
                }
            }

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

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
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

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        /// <summary>A finger on the joystick pushed forward for a while (a real touch drag through the EventSystem), then released.</summary>
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

        /// <summary>A real camera drag: the right mouse button held while the mouse moves sideways.</summary>
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

        private TutorialController Tutorial => One<TutorialController>();

        private static GameProgressData Saved(MemoryProgressStore store) => JsonUtility.FromJson<GameProgressData>(store.Json);

        // ================================================================ the save (plain C#)

        [Test]
        public void TheSave_StartNewGameMakesItPending_OldSavesAreNotOffered_AndTheStateSticksAndClamps()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>("Assets/Data/Areas/AreaCatalog.asset");

            // a game that existed before the tutorial: the field is missing, the tutorial is never offered
            var old = new MemoryProgressStore(OldSave);
            ProgressService oldService = ProgressService.Create(catalog, old);
            Assert.That(oldService.Tutorial, Is.EqualTo(TutorialStatus.NotOffered));
            Assert.That(oldService.TotalGold, Is.EqualTo(90));
            Assert.That(oldService.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded), "an old save is loaded as it is, not 'repaired'");

            // NEW GAME makes it pending, on the same write that resets the progress
            Assert.That(oldService.StartNewGame(), Is.True);
            Assert.That(oldService.Tutorial, Is.EqualTo(TutorialStatus.Pending));
            Assert.That(Saved(old).tutorialState, Is.EqualTo((int)TutorialStatus.Pending));
            Assert.That(Saved(old).totalGold, Is.EqualTo(0));

            // finished / skipped is remembered across a restart, and the gold is untouched
            oldService.RegisterCollected("green_mine.gold_coin_01", 10);
            Assert.That(oldService.SetTutorial(TutorialStatus.Finished), Is.True);
            ProgressService reopened = ProgressService.Create(catalog, new MemoryProgressStore(old.Json));
            Assert.That(reopened.Tutorial, Is.EqualTo(TutorialStatus.Finished));
            Assert.That(reopened.TotalGold, Is.EqualTo(10));
            Assert.That(reopened.IsCollected("green_mine.gold_coin_01"), Is.True);

            // a second NEW GAME makes it available again
            Assert.That(reopened.StartNewGame(), Is.True);
            Assert.That(reopened.Tutorial, Is.EqualTo(TutorialStatus.Pending));

            // a nonsense value is clamped, silently
            ProgressService odd = ProgressService.Create(catalog, new MemoryProgressStore("{\"version\":1,\"totalGold\":0,\"collectedTreasureIds\":[],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[],\"tutorialState\":9}"));
            Assert.That(odd.Tutorial, Is.EqualTo(TutorialStatus.Finished));
            Assert.That(odd.LoadStatus, Is.EqualTo(ProgressLoadStatus.Loaded));
        }

        // ================================================================ start rules

        [UnityTest]
        public IEnumerator Pending_StartsTheTutorialInGreenMine_AtStepOne_WithTheJoystickHighlighted()
        {
            yield return LoadScene(new MemoryProgressStore(PendingSave));
            TutorialController tutorial = Tutorial;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tutorial.IsRunning, Is.True);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Move));
            Assert.That(tutorial.UiRoot.activeInHierarchy, Is.True);
            Assert.That(tutorial.CurrentTitle, Is.EqualTo("USE THE JOYSTICK TO MOVE"));
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.True, "the joystick is highlighted");
            Rect joystick = ScreenRect((RectTransform)One<VirtualJoystick>().transform);
            Assert.That(joystick.Contains(ScreenRect(tutorial.Highlight).center), Is.True, "the halo sits on the joystick");
            Assert.That(tutorial.SkipButton.gameObject.activeInHierarchy, Is.True, "SKIP TUTORIAL is on screen from the start");
            Assert.That(TutorialController.Titles[1], Is.EqualTo("USE THE JOYSTICK TO MOVE"));
            Assert.That(TutorialController.Titles[2], Is.EqualTo("SWIPE TO LOOK AROUND"));
            Assert.That(TutorialController.Titles[3], Is.EqualTo("FOLLOW THE DETECTOR SIGNAL"));
            Assert.That(TutorialController.Titles[4], Is.EqualTo("TAP DIG TO REVEAL THE TREASURE"));
            Assert.That(TutorialController.Titles[5], Is.EqualTo("TAP COLLECT TO EARN GOLD"));
            Assert.That(TutorialController.Titles[6], Is.EqualTo("TUTORIAL COMPLETE!"));
        }

        [UnityTest]
        public IEnumerator OldSave_AndFinishedSave_NeverStartTheTutorial_AndTheGameIsNormal()
        {
            yield return LoadScene(new MemoryProgressStore(OldSave));
            Assert.That(Tutorial.IsRunning, Is.False, "an existing game does not get the tutorial");
            Assert.That(Tutorial.UiRoot.activeInHierarchy, Is.False);
            Assert.That(One<TreasureHuntController>().TotalGold, Is.EqualTo(90), "gold is untouched");
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;

            yield return LoadScene(new MemoryProgressStore(FinishedSave));
            Assert.That(Tutorial.IsRunning, Is.False, "a finished (or skipped) tutorial is not shown again");
            Assert.That(Tutorial.UiRoot.activeInHierarchy, Is.False);
        }

        [Test]
        public void OnlyGreenMine_HasTheTutorial()
        {
            string guid = AssetDatabase.AssetPathToGUID("Assets/Scripts/UI/TutorialController.cs");
            Assert.That(File.ReadAllText("Assets/Scenes/GreenMine.unity"), Does.Contain(guid), "Green Mine hosts the tutorial");
            Assert.That(File.ReadAllText("Assets/Scenes/RockyCanyon.unity"), Does.Not.Contain(guid), "Rocky Canyon has none");
            Assert.That(File.ReadAllText("Assets/Scenes/AncientMine.unity"), Does.Not.Contain(guid), "nor has Ancient Mine");
        }

        // ================================================================ the five steps with real play

        [UnityTest]
        public IEnumerator TheFiveSteps_AreFinishedByRealPlay_AndTheFirstCollectPaysNormalGold_AndIsRecorded()
        {
            var store = new MemoryProgressStore(PendingSave);
            yield return LoadScene(store);
            TutorialController tutorial = Tutorial;
            var hunt = One<TreasureHuntController>();
            var detector = One<TreasureDetector>();
            TreasureSpot spot = Spot("green_mine.mine_01");
            var steps = new List<TutorialController.TutorialStep>();
            tutorial.StepChanged += s => steps.Add(s);
            yield return new WaitForSecondsRealtime(0.3f);

            // STEP 1: a touch on the joystick is NOT enough (a tap without moving), pushing it and really walking is
            Vector2 centre = ScreenCentre((RectTransform)One<VirtualJoystick>().transform);
            yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Began, centre);
            yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Ended, centre);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Move), "touching the joystick without moving does not count");
            Vector3 before = Player.position;
            yield return DragJoystick(3f);
            Assert.That(Vector3.Distance(Player.position, before), Is.GreaterThan(3f), "the miner really walked");
            yield return WaitUntil(() => tutorial.Step != TutorialController.TutorialStep.Move, 1f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Look), "STEP 2 after real movement");
            Assert.That(tutorial.SwipeHint.gameObject.activeSelf, Is.True, "the swipe hint on the right side");
            Assert.That(tutorial.CurrentTitle, Is.EqualTo("SWIPE TO LOOK AROUND"));

            // STEP 2: no camera movement, no progress; a real drag turns the camera
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Look));
            float yaw = One<ThirdPersonCameraController>().Yaw;
            yield return DragCamera(60);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, One<ThirdPersonCameraController>().Yaw)), Is.GreaterThan(TutorialController.LookDegrees * 0.8f), "the camera really turned");
            yield return WaitUntil(() => tutorial.Step != TutorialController.TutorialStep.Look, 1f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Find), "STEP 3 after a real camera drag");
            Assert.That(tutorial.SwipeHint.gameObject.activeSelf, Is.False);

            // STEP 3: far from every treasure the arrow points at the nearest hidden one; it only moves on when the detector reaches RAPID (and after it had been shown for a moment)
            float digRadius = spot.Definition.DigRadius;
            float rapidFrom = spot.Definition.DetectionRadius - DetectorSignalIndicator.RapidFromProximity * (spot.Definition.DetectionRadius - digRadius);
            Teleport(Player, PlaceAtDistance(spot, rapidFrom + 6f));
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Find), "a weak signal does not finish the step");
            Assert.That(tutorial.Arrow.parent.gameObject.activeSelf, Is.True, "the arrow towards the nearest treasure");
            TreasureSpot nearest = null;
            float best = float.MaxValue;
            foreach (TreasureSpot s in TreasureSpot.Active)
            {
                Vector3 d = s.transform.position - Player.position;
                d.y = 0f;
                if (s.State == TreasureState.Hidden && d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    nearest = s;
                }
            }

            Assert.That(tutorial.GuidedTreasure, Is.EqualTo(nearest), "the arrow guides to the nearest hidden treasure");
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.True, "the detector is highlighted");
            Teleport(Player, PlaceAtDistance(spot, (digRadius + rapidFrom) * 0.5f));
            yield return WaitUntil(() => tutorial.Step != TutorialController.TutorialStep.Find, 4f);
            Assert.That(detector.HasSignal && detector.Proximity >= DetectorSignalIndicator.RapidFromProximity, Is.True, "the detector is at RAPID");
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Dig), "STEP 4 once the detector reached RAPID");
            Assert.That(tutorial.CurrentTitle, Is.EqualTo("TAP DIG TO REVEAL THE TREASURE"));

            // STEP 4: still outside the dig range DIG is not there and the step waits; then a real tap on the real DIG button digs
            Button dig = FindButton("DigButton");
            Assert.That(dig.gameObject.activeInHierarchy, Is.False, "RAPID but not yet in range: no DIG button, and the tutorial does not pretend there is");
            Teleport(Player, PlaceAtDistance(spot, 1.5f));
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Canvas.ForceUpdateCanvases();
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Dig));
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.True, "DIG is highlighted");
            Assert.That(ScreenRect((RectTransform)dig.transform).Contains(ScreenRect(tutorial.Highlight).center), Is.True, "the halo sits on the DIG button");
            int gold = hunt.TotalGold;
            yield return Click(ScreenCentre((RectTransform)dig.transform)); // real tap
            Assert.That(hunt.IsDigging || spot.State != TreasureState.Hidden, Is.True, "the normal dig started");
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed));
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Collect), "STEP 5 once the dig has finished (the animation was not cut)");

            // STEP 5: a real tap on the real COLLECT button pays the normal gold and completes the tutorial
            Button collect = FindButton("CollectButton");
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Canvas.ForceUpdateCanvases();
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.True, "COLLECT is highlighted");
            Assert.That(tutorial.CurrentTitle, Is.EqualTo("TAP COLLECT TO EARN GOLD"));
            yield return Click(ScreenCentre((RectTransform)collect.transform)); // real tap
            yield return WaitUntil(() => spot.State == TreasureState.Collected, 3f);
            Assert.That(hunt.TotalGold, Is.EqualTo(gold + spot.Definition.GoldValue), "the gold is paid exactly as in a normal game");
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Complete));
            Assert.That(tutorial.CurrentTitle, Is.EqualTo("TUTORIAL COMPLETE!"));
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Finished), "completion is saved");
            Assert.That(Saved(store).collectedTreasureIds, Contains.Item(spot.SpotId));
            Assert.That(Saved(store).totalGold, Is.EqualTo(gold + spot.Definition.GoldValue));
            yield return new WaitForSecondsRealtime(TutorialController.CompleteShowSeconds + 0.5f);
            Assert.That(tutorial.IsRunning, Is.False);
            Assert.That(tutorial.UiRoot.activeInHierarchy, Is.False, "the notice goes away by itself");
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.False);
            Assert.That(steps, Is.EqualTo(new[]
            {
                TutorialController.TutorialStep.Look, TutorialController.TutorialStep.Find, TutorialController.TutorialStep.Dig,
                TutorialController.TutorialStep.Collect, TutorialController.TutorialStep.Complete,
            }), "the five steps in order");
        }

        [UnityTest]
        public IEnumerator DiggingAndCollectingEarly_MovesTheTutorialOn_AndNeverLocksThePlayer()
        {
            var store = new MemoryProgressStore(PendingSave);
            yield return LoadScene(store);
            TutorialController tutorial = Tutorial;
            var hunt = One<TreasureHuntController>();
            TreasureSpot spot = Spot("green_mine.mine_01");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Move));

            // the player ignores steps 1 - 3 and digs: the tutorial jumps to COLLECT, then completes with the collect
            Button dig = FindButton("DigButton");
            Teleport(Player, PlaceAtDistance(spot, 1.5f));
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Canvas.ForceUpdateCanvases();
            yield return Click(ScreenCentre((RectTransform)dig.transform));
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Collect));
            Button collect = FindButton("CollectButton");
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Canvas.ForceUpdateCanvases();
            int gold = hunt.TotalGold;
            yield return Click(ScreenCentre((RectTransform)collect.transform));
            yield return WaitUntil(() => spot.State == TreasureState.Collected, 3f);
            Assert.That(hunt.TotalGold, Is.EqualTo(gold + spot.Definition.GoldValue));
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Complete));
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Finished));
        }

        // ================================================================ SKIP, restart, NEW GAME

        [UnityTest]
        public IEnumerator SkipTutorial_ByARealTap_ClosesItForGood_KeepsEverything_AndTheGameKeepsWorking()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            GameSettings.Current.SetSfxVolume(0.4f);
            var store = new MemoryProgressStore(PendingSave);
            yield return LoadScene(store);
            TutorialController tutorial = Tutorial;
            yield return new WaitForSecondsRealtime(0.3f);
            tutorial.GetType(); // (still running)
            Vector3 before = Player.position;
            yield return DragJoystick(2f); // into step 2
            Assert.That(tutorial.Step, Is.Not.EqualTo(TutorialController.TutorialStep.Move));
            Canvas.ForceUpdateCanvases();
            bool skipped = false;
            tutorial.Finished += s => skipped = s;
            yield return Click(ScreenCentre((RectTransform)tutorial.SkipButton.transform)); // real tap
            Assert.That(skipped, Is.True, "SKIP TUTORIAL reported");
            Assert.That(tutorial.IsRunning, Is.False);
            Assert.That(tutorial.UiRoot.activeInHierarchy, Is.False);
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.False);
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Finished));
            Assert.That(Saved(store).totalGold, Is.EqualTo(0), "gold untouched");
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.4f).Within(1e-4f));

            // the scene goes on: the controls still work and the normal reward is paid
            Vector3 mid = Player.position;
            yield return DragJoystick(1.5f);
            Assert.That(Vector3.Distance(Player.position, mid), Is.GreaterThan(1.5f), "the joystick still moves the miner");
            TreasureSpot spot = Spot("green_mine.mine_01");
            var hunt = One<TreasureHuntController>();
            Button dig = FindButton("DigButton");
            Teleport(Player, PlaceAtDistance(spot, 1.5f));
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Canvas.ForceUpdateCanvases();
            yield return Click(ScreenCentre((RectTransform)dig.transform));
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            Button collect = FindButton("CollectButton");
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Canvas.ForceUpdateCanvases();
            yield return Click(ScreenCentre((RectTransform)collect.transform));
            yield return WaitUntil(() => spot.State == TreasureState.Collected, 3f);
            Assert.That(hunt.TotalGold, Is.EqualTo(spot.Definition.GoldValue), "DIG and COLLECT work as before");
            Assert.That(tutorial.IsRunning, Is.False, "and the tutorial did not come back");

            // reopening the game with the same save: no tutorial
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return LoadScene(new MemoryProgressStore(store.Json));
            Assert.That(Tutorial.IsRunning, Is.False, "a skipped tutorial is not shown again");
            Assert.That(One<TreasureHuntController>().TotalGold, Is.EqualTo(spot.Definition.GoldValue));
        }

        [UnityTest]
        public IEnumerator ClosingTheGameMidTutorial_StartsItAgainFromStepOne_AndKeepsTheProgress()
        {
            var store = new MemoryProgressStore(PendingSave);
            yield return LoadScene(store);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return DragJoystick(2f);
            Assert.That(Tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Look));
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Pending), "still pending: it was neither finished nor skipped");
            yield return SceneManager.UnloadSceneAsync(scene); // the game is closed
            scene = default;
            yield return LoadScene(new MemoryProgressStore(store.Json));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(Tutorial.IsRunning, Is.True);
            Assert.That(Tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Move), "it starts again from the first step");
            Assert.That(One<TreasureHuntController>().TotalGold, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator NewGame_MakesTheTutorialAvailableAgain_AndCancelDoesNot()
        {
            var store = new MemoryProgressStore(FinishedSave);
            ProgressManager.StoreFactoryOverride = () => store;
            const string menu = "Assets/Scenes/MainMenu.unity";
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(menu, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            scene = SceneManager.GetSceneByPath(menu);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
            SettingsPanelController panel = null;
            foreach (SettingsPanelController p in Resources.FindObjectsOfTypeAll<SettingsPanelController>())
            {
                if (p.gameObject.scene == scene)
                {
                    panel = p;
                }
            }

            panel.Show();
            yield return null;
            string before = store.Json;
            panel.NewGameButton.onClick.Invoke(); // (direct calls, not real taps)
            yield return null;
            panel.CancelNewGameButton.onClick.Invoke();
            yield return null;
            Assert.That(store.Json, Is.EqualTo(before), "CANCEL changes nothing");
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Finished));

            panel.NewGameButton.onClick.Invoke();
            yield return null;
            panel.ConfirmNewGameButton.onClick.Invoke();
            yield return null;
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Pending), "START NEW GAME makes the tutorial available again");
            Assert.That(Saved(store).totalGold, Is.EqualTo(0));
            var intro = Object.FindAnyObjectByType<IntroSlidesController>();
            Assert.That(intro.IsOpen, Is.True, "the 17A introduction still opens first");
            intro.Skip(); // -> Character Selection
            yield return null;
            Assert.That(Saved(store).tutorialState, Is.EqualTo((int)TutorialStatus.Pending), "leaving the introduction does not start or finish the tutorial");
        }

        // ================================================================ layout, pause, safe area

        [UnityTest]
        public IEnumerator OnAnIphone_TheTutorialStaysInsideTheSafeArea_AndNeverCoversATouchControl()
        {
            SafeAreaFitter.SafeAreaOverride = () => new Rect(Screen.width * 0.0633f, Screen.height * 0.0488f, Screen.width * (1f - 2f * 0.0633f), Screen.height * (1f - 0.0488f));
            Rect safe = new Rect(Screen.width * 0.0633f, Screen.height * 0.0488f, Screen.width * (1f - 2f * 0.0633f), Screen.height * (1f - 0.0488f));
            yield return LoadScene(new MemoryProgressStore(PendingSave));
            TutorialController tutorial = Tutorial;
            yield return new WaitForSecondsRealtime(0.4f);
            Canvas.ForceUpdateCanvases();

            Transform plate = tutorial.UiRoot.transform.Find("Plate");
            Rect plateRect = ScreenRect((RectTransform)plate);
            Rect skip = ScreenRect((RectTransform)tutorial.SkipButton.transform);
            Assert.That(safe.Contains(plateRect.min) && safe.Contains(plateRect.max), Is.True, $"plate {plateRect} inside the safe area {safe}");
            Assert.That(safe.Contains(skip.min) && safe.Contains(skip.max), Is.True, $"SKIP TUTORIAL {skip} inside the safe area");
            Assert.That(skip.height, Is.GreaterThanOrEqualTo(Screen.height * 0.075f), "a comfortable touch size");

            var controls = new Dictionary<string, Rect>
            {
                ["joystick"] = ScreenRect((RectTransform)One<VirtualJoystick>().transform),
                ["pause"] = ScreenRect((RectTransform)FindButton("PauseButton").transform),
            };
            foreach (var pair in controls)
            {
                Assert.That(plateRect.Overlaps(pair.Value), Is.False, $"the plate does not cover the {pair.Key}");
                Assert.That(skip.Overlaps(pair.Value), Is.False, $"SKIP TUTORIAL does not cover the {pair.Key}");
            }

            // the DIG / COLLECT areas (shown or not) are free of the plate, the skip button and the swipe hint
            var hud = One<HudController>();
            foreach (RectTransform action in new[] { hud.DigButtonRect, hud.CollectButtonRect })
            {
                Rect a = ScreenRect(action);
                Assert.That(plateRect.Overlaps(a), Is.False, "the plate does not cover " + action.name);
                Assert.That(skip.Overlaps(a), Is.False, "SKIP TUTORIAL does not cover " + action.name);
            }

            // only SKIP TUTORIAL takes touches: a raycast through the middle of the plate hits nothing of the tutorial
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = plateRect.center }, hits);
            foreach (RaycastResult h in hits)
            {
                Assert.That(h.gameObject.transform.IsChildOf(tutorial.UiRoot.transform), Is.False, "the plate takes no touch: " + h.gameObject.name);
            }

            foreach (Graphic g in tutorial.UiRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (!g.transform.IsChildOf(tutorial.SkipButton.transform))
                {
                    Assert.That(g.raycastTarget, Is.False, g.name + " must not block touches");
                }
            }

            // the swipe hint (step 2) stays in the safe area, on the right, clear of the action buttons and the plate
            yield return DragJoystick(2f);
            yield return new WaitForSecondsRealtime(0.3f);
            Canvas.ForceUpdateCanvases();
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Look));
            Rect swipe = ScreenRect(tutorial.SwipeHint);
            Assert.That(safe.Contains(swipe.min) && safe.Contains(swipe.max), Is.True, $"swipe hint {swipe} inside the safe area");
            Assert.That(swipe.center.x, Is.GreaterThan(Screen.width * 0.5f), "on the right, where the camera drag works");
            Assert.That(swipe.Overlaps(plateRect), Is.False);
            Assert.That(swipe.Overlaps(skip), Is.False);
            Assert.That(swipe.Overlaps(ScreenRect(hud.DigButtonRect)) || swipe.Overlaps(ScreenRect(hud.CollectButtonRect)), Is.False, "clear of DIG / COLLECT");
        }

        [UnityTest]
        public IEnumerator ThePause_HidesTheTutorial_AndResumeShowsItAgain()
        {
            yield return LoadScene(new MemoryProgressStore(PendingSave));
            TutorialController tutorial = Tutorial;
            yield return new WaitForSecondsRealtime(0.3f);
            var group = tutorial.UiRoot.transform.Find("Plate").GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(1f));
            GamePause.Pause();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(group.alpha, Is.EqualTo(0f), "nothing of the tutorial behind the pause menu");
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.False);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialController.TutorialStep.Move), "the step does not change while paused");
            GamePause.Resume();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(tutorial.Highlight.gameObject.activeSelf, Is.True);
        }
    }
}
#endif
