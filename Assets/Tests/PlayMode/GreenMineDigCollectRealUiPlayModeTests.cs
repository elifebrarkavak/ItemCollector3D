#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
using NUnit.Framework;
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
    /// DIG / COLLECT through the real UI: a virtual mouse presses the on-screen buttons (EventSystem + GraphicRaycaster + Button), never
    /// <c>onClick.Invoke()</c>. An in-memory progress store keeps the developer's save file untouched.
    /// </summary>
    public sealed class GreenMineDigCollectRealUiPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";

        private Scene scene;
        private float previousTimeScale;
        private Mouse mouse;
        private Touchscreen touchscreen;
        private enum Pointer { Mouse, Touch, TouchWhileJoystickHeld }
        private InputSettings.EditorInputBehaviorInPlayMode previousBehaviour;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            SafeAreaFitter.SafeAreaOverride = () => new Rect(0f, 0f, Screen.width, Screen.height); // independent of the simulator's device frame
            previousBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse = InputSystem.AddDevice<Mouse>("IC3D_TestMouse");
            touchscreen = InputSystem.AddDevice<Touchscreen>("IC3D_TestTouchscreen");
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
        }

        private IEnumerator LoadScene(IProgressStore store)
        {
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, "Could not load " + ScenePath);
            yield return load;
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            Physics.SyncTransforms();
        }

        private T[] All<T>() where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return result.ToArray();
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

        private Button FindButton(string name)
        {
            foreach (Button button in All<Button>())
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            Assert.Fail("Button not found: " + name);
            return null;
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        private static Vector2 ScreenCentre(Button button)
        {
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners); // Screen Space - Overlay: world corners are screen pixels
            return (corners[0] + corners[2]) * 0.5f;
        }

        /// <summary>What the pointer would hit at this screen position (topmost first).</summary>
        private static string HitsAt(Vector2 position)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, results);
            var names = new List<string>();
            foreach (RaycastResult r in results)
            {
                names.Add(r.gameObject.name);
            }

            return names.Count == 0 ? "(nothing)" : string.Join(" > ", names);
        }

        private static GameObject TopHit(Vector2 position)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        private IEnumerator TouchPhaseEvent(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = position, pressure = phase == UnityEngine.InputSystem.TouchPhase.Ended ? 0f : 1f });
            yield return null;
            yield return null;
        }

        /// <summary>One tap (a finger, id 2, down and up) or one mouse click, through the Input System like a real device.</summary>
        private IEnumerator Tap(Pointer kind, Vector2 position)
        {
            if (kind == Pointer.Mouse)
            {
                yield return Press(position);
                yield break;
            }

            yield return TouchPhaseEvent(2, UnityEngine.InputSystem.TouchPhase.Began, position);
            yield return TouchPhaseEvent(2, UnityEngine.InputSystem.TouchPhase.Ended, position);
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

        private IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        private TreasureSpot Spot(string id)
        {
            foreach (TreasureSpot spot in All<TreasureSpot>())
            {
                if (spot.SpotId == id)
                {
                    return spot;
                }
            }

            Assert.Fail("Treasure spot not found: " + id);
            return null;
        }

        [UnityTest]
        public IEnumerator Dig_And_Collect_WithAMouse_PaysOnce_AndIsSaved() => Flow(Pointer.Mouse);

        [UnityTest]
        public IEnumerator Dig_And_Collect_WithATouch_PaysOnce_AndIsSaved() => Flow(Pointer.Touch);

        [UnityTest]
        public IEnumerator Dig_And_Collect_WithASecondFinger_WhileTheJoystickIsHeld() => Flow(Pointer.TouchWhileJoystickHeld);

        // the shape of a developer's save in the middle of Green Mine (six treasures collected, 90 gold), copied into memory: the real file is never read
        private const string MidGameSave = "{\"version\":1,\"totalGold\":90,\"collectedTreasureIds\":[\"green_mine.gold_coin_01\",\"green_mine.meadow_01\",\"green_mine.riverbank_01\",\"green_mine.rocky_01\",\"green_mine.rocky_03\",\"green_mine.forest_01\"],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[]}";

        [UnityTest]
        public IEnumerator Dig_And_Collect_WithAMidGameSave_AddsToTheExistingGold() => Flow(Pointer.Touch, new MemoryProgressStore(MidGameSave), 90);

        private IEnumerator Flow(Pointer kind) => Flow(kind, new MemoryProgressStore(), 0);

        private IEnumerator Flow(Pointer kind, MemoryProgressStore store, int startGold)
        {
            yield return LoadScene(store);
            var hunt = All<TreasureHuntController>()[0];
            TreasureSpot spot = Spot("green_mine.mine_01"); // the treasure next to the mine entrance (the one in the bug report)
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden));
            int gold = hunt.TotalGold;
            Assert.That(gold, Is.EqualTo(startGold), "the save the test started with");

            Teleport(Player, spot.transform.position + new Vector3(0f, 1.1f, -1.6f));
            yield return WaitUntil(() => FindButton("DigButton").gameObject.activeInHierarchy && FindButton("DigButton").interactable, 2f);
            yield return new WaitForSecondsRealtime(1.5f);
            Canvas.ForceUpdateCanvases();

            Button dig = FindButton("DigButton");
            var chain = new System.Text.StringBuilder();
            for (Transform t = dig.transform; t != null; t = t.parent)
            {
                var cg = t.GetComponent<CanvasGroup>();
                var gr = t.GetComponent<GraphicRaycaster>();
                chain.Append($"{t.name}[active={t.gameObject.activeSelf}{(cg != null ? $" cg(a={cg.alpha:F2},blocks={cg.blocksRaycasts},inter={cg.interactable},ignoreParent={cg.ignoreParentGroups})" : "")}{(gr != null ? $" raycaster(enabled={gr.enabled})" : "")}] < ");
            }

            Debug.Log("DIG chain: " + chain + $" eventSystem={EventSystem.current}, graphic raycast target={dig.targetGraphic?.raycastTarget}");
            Vector2 digAt = ScreenCentre(dig);
            Debug.Log($"DIG button at {digAt} on a {Screen.width}x{Screen.height} screen; pointer would hit: {HitsAt(digAt)}");
            Assert.That(new Rect(0f, 0f, Screen.width, Screen.height).Contains(digAt), Is.True, "the DIG button is on the screen");
            Assert.That(dig.gameObject.activeInHierarchy && dig.interactable, Is.True, "DIG is offered inside the dig range");
            Assert.That(TopHit(digAt), Is.Not.Null.And.Matches<GameObject>(g => g.transform.IsChildOf(dig.transform)), "nothing covers the DIG button: " + HitsAt(digAt));

            if (kind == Pointer.TouchWhileJoystickHeld)
            {
                VirtualJoystick joystick = All<VirtualJoystick>()[0];
                var jc = new Vector3[4];
                ((RectTransform)joystick.transform).GetWorldCorners(jc);
                yield return TouchPhaseEvent(1, UnityEngine.InputSystem.TouchPhase.Began, (jc[0] + jc[2]) * 0.5f); // a thumb resting on the joystick (no deflection)
            }

            yield return Tap(kind, digAt);
            yield return WaitUntil(() => spot.State != TreasureState.Hidden, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "pressing DIG reveals the treasure");

            Button collect = FindButton("CollectButton");
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            Canvas.ForceUpdateCanvases();
            Vector2 collectAt = ScreenCentre(collect);
            Debug.Log($"COLLECT button at {collectAt}; pointer would hit: {HitsAt(collectAt)}");
            Assert.That(collect.gameObject.activeInHierarchy, Is.True, "COLLECT replaces DIG once the treasure is revealed");

            yield return Tap(kind, collectAt);
            yield return WaitUntil(() => spot.State == TreasureState.Collected, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), "pressing COLLECT collects it");
            Assert.That(hunt.TotalGold, Is.EqualTo(gold + spot.Definition.GoldValue), "the gold goes up by the treasure's value");
            Assert.That(JsonUtility.FromJson<GameProgressData>(store.Json).collectedTreasureIds, Contains.Item(spot.SpotId), "the collection is saved (in memory)");

            // a second press where COLLECT used to be pays nothing
            yield return Tap(kind, collectAt);
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(gold + spot.Definition.GoldValue), "no double payment");
        }

        /// <summary>A free standing place on open ground at the given horizontal distance from the spot.</summary>
        private Vector3 PlaceAtDistance(TreasureSpot spot, float distance)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                Vector3 p = spot.transform.position + new Vector3(Mathf.Cos(a) * distance, 0f, Mathf.Sin(a) * distance);
                if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
                    && !Physics.CheckCapsule(hit.point + Vector3.up * 0.6f, hit.point + Vector3.up * 1.5f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    return hit.point + Vector3.up * 1.1f;
                }
            }

            Assert.Fail($"No free standing place {distance} m from {spot.SpotId}");
            return default;
        }

        [UnityTest]
        public IEnumerator RapidSignal_IsNotDigReady_TheHudSaysGetCloser_UntilTheDigRangeIsReached()
        {
            yield return LoadScene(new MemoryProgressStore());
            TreasureSpot spot = Spot("green_mine.mine_01");
            var indicator = All<DetectorSignalIndicator>()[0];
            var detector = All<TreasureDetector>()[0];
            Button dig = FindButton("DigButton");
            float digRadius = spot.Definition.DigRadius;

            // RAPID starts at proximity 0.75, well outside the dig radius: it means "close", not "dig now"
            float rapidFrom = spot.Definition.DetectionRadius - DetectorSignalIndicator.RapidFromProximity * (spot.Definition.DetectionRadius - digRadius);
            Assert.That(rapidFrom, Is.GreaterThan(digRadius + 1f), "the RAPID band reaches further than the dig range");

            Teleport(Player, PlaceAtDistance(spot, (digRadius + rapidFrom) * 0.5f));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(indicator.Level, Is.EqualTo(DetectorSignalIndicator.SignalLevel.Rapid), "RAPID is showing");
            Assert.That(dig.gameObject.activeSelf, Is.False, "but DIG is not offered yet");
            Assert.That(indicator.Hint, Is.EqualTo(DetectorSignalIndicator.GetCloserHint), "so the HUD says what to do");

            Teleport(Player, PlaceAtDistance(spot, digRadius - 0.9f));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(dig.gameObject.activeSelf && dig.interactable, Is.True, "inside the dig range DIG is offered");
            Assert.That(indicator.Hint, Is.Empty, "and the hint goes away");

            Teleport(Player, PlaceAtDistance(spot, rapidFrom + 4f));
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(indicator.Level, Is.Not.EqualTo(DetectorSignalIndicator.SignalLevel.Rapid));
            Assert.That(indicator.Hint, Is.Empty, "no hint while the signal is still weak");
            Assert.That(detector.HasSignal, Is.True);
        }

        [UnityTest]
        public IEnumerator Dig_SurvivesAStepAtTheEdgeOfTheRange_ButLeavingExplainsWhy()
        {
            yield return LoadScene(new MemoryProgressStore());
            TreasureSpot spot = Spot("green_mine.mine_01");
            var indicator = All<DetectorSignalIndicator>()[0];
            var hunt = All<TreasureHuntController>()[0];
            float digRadius = spot.Definition.DigRadius;
            Button dig = FindButton("DigButton");

            // DIG pressed right at the edge of the range, then the character drifts a little further out: the dig must not be thrown away
            Teleport(Player, PlaceAtDistance(spot, digRadius - 0.1f));
            yield return new WaitForSecondsRealtime(1.5f);
            Canvas.ForceUpdateCanvases();
            Assert.That(dig.gameObject.activeSelf, Is.True);
            yield return Tap(Pointer.Mouse, ScreenCentre(dig));
            yield return WaitUntil(() => hunt.IsDigging, 1f);
            Assert.That(hunt.IsDigging, Is.True, "the dig started");
            Teleport(Player, PlaceAtDistance(spot, digRadius + 0.35f));
            yield return WaitUntil(() => spot.State != TreasureState.Hidden, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "a small step out of the range does not cancel the dig");

            // a second treasure: walking clearly away mid-dig cancels it, and the HUD explains why
            TreasureSpot other = Spot("green_mine.rocky_02");
            Teleport(Player, PlaceAtDistance(other, 1.5f));
            yield return new WaitForSecondsRealtime(1.0f);
            Canvas.ForceUpdateCanvases();
            Assert.That(dig.gameObject.activeSelf, Is.True);
            yield return Tap(Pointer.Mouse, ScreenCentre(dig));
            yield return WaitUntil(() => hunt.IsDigging, 1f);
            Assert.That(hunt.IsDigging, Is.True);
            Teleport(Player, PlaceAtDistance(other, other.Definition.DigRadius + 3f));
            yield return null;
            yield return null;
            Assert.That(hunt.IsDigging, Is.False, "walking away cancels the dig");
            Assert.That(other.State, Is.EqualTo(TreasureState.Hidden));
            Assert.That(indicator.Hint, Is.EqualTo(DetectorSignalIndicator.DigCancelledHint), "the HUD says the player was too far");
        }
    }
}
#endif
