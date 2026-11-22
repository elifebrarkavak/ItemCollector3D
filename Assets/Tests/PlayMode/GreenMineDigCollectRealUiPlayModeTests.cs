#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
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
                if (Physics.Raycast(new Vector3(p.x, spot.transform.position.y + 3f, p.z), Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
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

        // ================================================================ the fifteen treasures (stage 11)

        private static readonly string[] NewTreasureIds =
        {
            "green_mine.mine_room_01", "green_mine.mine_room_02", "green_mine.mine_tunnel_01", "green_mine.forest_03",
            "green_mine.rocky_04", "green_mine.camp_01", "green_mine.bridge_01",
        };

        // the shape of the developer's save at the start of this stage (seven treasures collected, 190 gold), copied into memory: the real file is never read
        private const string SaveWith190Gold = "{\"version\":1,\"totalGold\":190,\"collectedTreasureIds\":[\"green_mine.gold_coin_01\",\"green_mine.meadow_01\",\"green_mine.riverbank_01\",\"green_mine.rocky_01\",\"green_mine.rocky_03\",\"green_mine.forest_01\",\"green_mine.mine_01\"],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[]}";

        // 450 gold: the three Epic treasures, two Rare ones and two Uncommon ones: one Rare treasure (50) from the 500 goal
        private const string SaveWith450Gold = "{\"version\":1,\"totalGold\":450,\"collectedTreasureIds\":[\"green_mine.mine_01\",\"green_mine.mine_room_01\",\"green_mine.mine_room_02\",\"green_mine.rocky_02\",\"green_mine.mine_tunnel_01\",\"green_mine.forest_02\",\"green_mine.riverbank_01\"],\"unlockedAreaIds\":[\"green_mine\"],\"completedAreaIds\":[]}";

        private IEnumerator DigAndCollectWithTheButtons(TreasureSpot spot, TreasureHuntController hunt)
        {
            Button dig = FindButton("DigButton");
            Teleport(Player, PlaceAtDistance(spot, 1.5f));
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.25f);
            Canvas.ForceUpdateCanvases();
            Assert.That(dig.gameObject.activeInHierarchy, Is.True, "DIG is offered at " + spot.SpotId);
            Assert.That(spot.transform.GetChild(0).gameObject.activeSelf, Is.False, spot.SpotId + " is still invisible before the dig");

            int gold = hunt.TotalGold;
            yield return Tap(Pointer.Mouse, ScreenCentre(dig));
            yield return WaitUntil(() => spot.State != TreasureState.Hidden, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "DIG reveals " + spot.SpotId);
            Assert.That(spot.transform.GetChild(0).gameObject.activeSelf, Is.True, spot.SpotId + " shows up after the dig");

            Button collect = FindButton("CollectButton");
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.25f);
            Canvas.ForceUpdateCanvases();
            yield return Tap(Pointer.Mouse, ScreenCentre(collect));
            yield return WaitUntil(() => spot.State == TreasureState.Collected, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), "COLLECT collects " + spot.SpotId);
            Assert.That(hunt.TotalGold, Is.EqualTo(gold + spot.Definition.GoldValue), "the reward of " + spot.SpotId);
        }

        [UnityTest]
        public IEnumerator TreasurePoints_AreFarEnoughApart_ThatTheirDigAndCollectAreasNeverOverlap()
        {
            yield return LoadScene(new MemoryProgressStore());
            TreasureSpot[] spots = All<TreasureSpot>();
            Assert.That(spots.Length, Is.EqualTo(15));
            for (int i = 0; i < spots.Length; i++)
            {
                for (int j = i + 1; j < spots.Length; j++)
                {
                    float d = Vector3.Distance(spots[i].transform.position, spots[j].transform.position);
                    // two treasures never share a dig area; the new ones are even placed so that DIG of one never shows while COLLECT of another is on offer
                    // (the original eight keep their places, rocky_01 and rocky_02 among them, 5.46 m apart)
                    bool isNew = System.Array.IndexOf(NewTreasureIds, spots[i].SpotId) >= 0 || System.Array.IndexOf(NewTreasureIds, spots[j].SpotId) >= 0;
                    float needed = isNew ? spots[i].Definition.DigRadius + spots[j].Definition.CollectRadius : spots[i].Definition.DigRadius + spots[j].Definition.DigRadius;
                    Assert.That(d, Is.GreaterThan(needed), $"{spots[i].SpotId} and {spots[j].SpotId} are {d:F2} m apart");
                }
            }

            // the two Epic treasures of the treasure room sit inside the room volume, the tunnel treasure inside the tunnel
            var room = new Bounds(new Vector3(6.3f, 2.5f, 31.5f), new Vector3(8f, 5f, 10f));
            var tunnel = new Bounds(new Vector3(6.3f, 2.3f, 20.5f), new Vector3(4.6f, 4.6f, 12f));
            Assert.That(room.Contains(Spot("green_mine.mine_room_01").transform.position + Vector3.up), Is.True);
            Assert.That(room.Contains(Spot("green_mine.mine_room_02").transform.position + Vector3.up), Is.True);
            Assert.That(tunnel.Contains(Spot("green_mine.mine_tunnel_01").transform.position + Vector3.up), Is.True);

            // standing on a treasure point never collides with the mine's colliders (walls, crystals, props)
            foreach (string id in new[] { "green_mine.mine_room_01", "green_mine.mine_room_02", "green_mine.mine_tunnel_01" })
            {
                Vector3 p = Spot(id).transform.position;
                Assert.That(Physics.CheckCapsule(p + Vector3.up * 0.6f, p + Vector3.up * 1.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore), Is.False, id + " is on open floor");
            }
        }

        [UnityTest]
        public IEnumerator EveryNewTreasure_IsDug_AndCollected_ThroughTheRealButtons_Once()
        {
            var store = new MemoryProgressStore();
            yield return LoadScene(store);
            var hunt = All<TreasureHuntController>()[0];
            yield return new WaitForSecondsRealtime(1.5f); // the HUD slides in
            Assert.That(hunt.TotalGold, Is.EqualTo(0));

            int expected = 0;
            foreach (string id in NewTreasureIds)
            {
                TreasureSpot spot = Spot(id);
                yield return DigAndCollectWithTheButtons(spot, hunt);
                expected += spot.Definition.GoldValue;
                Assert.That(JsonUtility.FromJson<GameProgressData>(store.Json).collectedTreasureIds, Contains.Item(id), id + " is saved");
            }

            Assert.That(expected, Is.EqualTo(370));
            Assert.That(hunt.TotalGold, Is.EqualTo(370), "the seven new treasures are worth 370 gold");
            Assert.That(JsonUtility.FromJson<GameProgressData>(store.Json).totalGold, Is.EqualTo(370));

            // collecting again pays nothing
            Teleport(Player, PlaceAtDistance(Spot("green_mine.mine_room_01"), 1.5f));
            yield return new WaitForSecondsRealtime(0.5f);
            Button collect = FindButton("CollectButton");
            Assert.That(collect.gameObject.activeInHierarchy, Is.False, "nothing left to collect");
            collect.onClick.Invoke();
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(370), "no double payment");
        }

        [UnityTest]
        public IEnumerator ExistingSave_KeepsItsGold_NewRewardsAddUp_AndNothingPaysTwice_AfterAReload()
        {
            var store = new MemoryProgressStore(SaveWith190Gold);
            yield return LoadScene(store);
            var hunt = All<TreasureHuntController>()[0];
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(hunt.TotalGold, Is.EqualTo(190), "the gold of the existing save is kept");
            TreasureSpot oldSpot = Spot("green_mine.mine_01");
            Assert.That(oldSpot.State, Is.EqualTo(TreasureState.Collected), "what was collected before stays collected");

            // standing on an already collected treasure offers nothing
            Teleport(Player, PlaceAtDistance(oldSpot, 1.5f));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(FindButton("DigButton").gameObject.activeInHierarchy, Is.False, "no DIG on a treasure that was already collected");
            Assert.That(FindButton("CollectButton").gameObject.activeInHierarchy, Is.False, "and no second reward");
            Assert.That(hunt.TotalGold, Is.EqualTo(190));

            TreasureSpot room = Spot("green_mine.mine_room_01");
            yield return DigAndCollectWithTheButtons(room, hunt);
            Assert.That(hunt.TotalGold, Is.EqualTo(290), "190 + the Epic treasure (100)");

            // load the level again with the same save
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return null;
            yield return LoadScene(store);
            hunt = All<TreasureHuntController>()[0];
            Assert.That(hunt.TotalGold, Is.EqualTo(290), "the new reward was saved");
            Assert.That(Spot("green_mine.mine_room_01").State, Is.EqualTo(TreasureState.Collected), "and its treasure stays collected");
            Assert.That(Spot("green_mine.mine_01").State, Is.EqualTo(TreasureState.Collected));
            Assert.That(Spot("green_mine.mine_room_02").State, Is.EqualTo(TreasureState.Hidden), "while the others are still buried");
            Assert.That(JsonUtility.FromJson<GameProgressData>(store.Json).collectedTreasureIds, Does.Contain("green_mine.gold_coin_01"), "the old save's ids are still there");
        }

        [UnityTest]
        public IEnumerator RockyCanyon_Unlocks_AndGreenMineCompletes_AtExactly500Gold()
        {
            var store = new MemoryProgressStore(SaveWith450Gold);
            yield return LoadScene(store);
            var hunt = All<TreasureHuntController>()[0];
            ProgressService service = All<ProgressManager>()[0].Service;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(hunt.TotalGold, Is.EqualTo(450));
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False, "Rocky Canyon is locked below 500 gold");
            Assert.That(service.IsAreaCompleted("green_mine"), Is.False);

            string unlocked = null;
            service.AreaUnlocked += id => unlocked = id;

            yield return DigAndCollectWithTheButtons(Spot("green_mine.camp_01"), hunt); // +10: 460
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False, "460 gold is not enough");

            yield return DigAndCollectWithTheButtons(Spot("green_mine.forest_03"), hunt); // +50: 510
            Assert.That(hunt.TotalGold, Is.EqualTo(510));
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.True, "500 gold unlocks Rocky Canyon by the existing rules");
            Assert.That(unlocked, Is.EqualTo("rocky_canyon"));
            Assert.That(service.IsAreaCompleted("green_mine"), Is.True, "and reaching the goal completes Green Mine");
            Assert.That(JsonUtility.FromJson<GameProgressData>(store.Json).unlockedAreaIds, Does.Contain("rocky_canyon"), "saved");
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False, "the next area needs 1200 gold");
        }

        private sealed class WalkStats
        {
            public int Frames;
            public float MinY = float.MaxValue, MaxY = float.MinValue;
        }

        private IEnumerator WalkTo(Transform player, ThirdPersonPlayerController controller, Camera camera, Vector2 target, float stopAt, string label, WalkStats stats)
        {
            float legTime = 0f;
            float legStart = Vector2.Distance(new Vector2(player.position.x, player.position.z), target);
            while (true)
            {
                Vector3 delta = new Vector3(target.x - player.position.x, 0f, target.y - player.position.z);
                if (delta.magnitude < stopAt)
                {
                    break;
                }

                legTime += Time.deltaTime;
                Assert.That(legTime, Is.LessThan(legStart / 3.0f + 4f), $"stuck on the way to {label} at {player.position}");
                Vector3 f = camera.transform.forward;
                f.y = 0f;
                f.Normalize();
                Vector3 r = new(f.z, 0f, -f.x);
                Vector3 d = delta.normalized;
                controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
                yield return null;
                stats.Frames++;
                stats.MinY = Mathf.Min(stats.MinY, player.position.y);
                stats.MaxY = Mathf.Max(stats.MaxY, player.position.y);
            }

            controller.ClearMoveInput();
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator BothMiners_WalkInto_TheTunnel_AndTheTreasureRoom_AndReachEveryUndergroundTreasure()
        {
            foreach (string miner in new[] { CharacterSelection.MaleId, CharacterSelection.FemaleId })
            {
                CharacterSelection.Select(miner);
                yield return LoadScene(new MemoryProgressStore());
                Time.timeScale = 2f;
                Transform player = Player;
                var controller = player.GetComponent<ThirdPersonPlayerController>();
                Camera camera = Camera.main;
                Button dig = FindButton("DigButton");
                var stats = new WalkStats();
                Teleport(player, new Vector3(6.3f, 1.3f, 10.5f));
                yield return new WaitForSecondsRealtime(1.2f);

                // outside -> mouth -> tunnel: the tunnel treasure
                yield return WalkTo(player, controller, camera, new Vector2(6.3f, 17.0f), 0.9f, "the tunnel", stats);
                TreasureSpot tunnelSpot = Spot("green_mine.mine_tunnel_01");
                yield return WalkTo(player, controller, camera, new Vector2(tunnelSpot.transform.position.x, tunnelSpot.transform.position.z), 1.5f, tunnelSpot.SpotId, stats);
                yield return WaitUntil(() => dig.gameObject.activeInHierarchy, 1f);
                Assert.That(tunnelSpot.HorizontalDistanceTo(player.position), Is.LessThanOrEqualTo(tunnelSpot.Definition.DigRadius), miner + " stands in dig range of the tunnel treasure");
                Assert.That(dig.gameObject.activeInHierarchy, Is.True, miner + ": DIG is offered in the tunnel");

                // tunnel -> treasure room: both Epic treasures
                yield return WalkTo(player, controller, camera, new Vector2(6.3f, 25.0f), 0.9f, "the end of the tunnel", stats);
                yield return WalkTo(player, controller, camera, new Vector2(7.2f, 27.3f), 0.9f, "the room door", stats);
                TreasureSpot roomB = Spot("green_mine.mine_room_02");
                yield return WalkTo(player, controller, camera, new Vector2(roomB.transform.position.x, roomB.transform.position.z), 1.5f, roomB.SpotId, stats);
                yield return WaitUntil(() => dig.gameObject.activeInHierarchy, 1f);
                Assert.That(roomB.HorizontalDistanceTo(player.position), Is.LessThanOrEqualTo(roomB.Definition.DigRadius), miner + " stands in dig range of the second Epic treasure");
                Assert.That(dig.gameObject.activeInHierarchy, Is.True);

                yield return WalkTo(player, controller, camera, new Vector2(5.8f, 28.6f), 0.9f, "the middle of the room", stats);
                TreasureSpot roomA = Spot("green_mine.mine_room_01");
                yield return WalkTo(player, controller, camera, new Vector2(roomA.transform.position.x, roomA.transform.position.z), 1.5f, roomA.SpotId, stats);
                yield return WaitUntil(() => dig.gameObject.activeInHierarchy, 1f);
                Assert.That(roomA.HorizontalDistanceTo(player.position), Is.LessThanOrEqualTo(roomA.Definition.DigRadius), miner + " stands in dig range of the first Epic treasure");
                Assert.That(dig.gameObject.activeInHierarchy, Is.True);

                // and out again
                yield return WalkTo(player, controller, camera, new Vector2(6.3f, 26.0f), 0.9f, "the room door (back)", stats);
                yield return WalkTo(player, controller, camera, new Vector2(6.3f, 12.0f), 0.9f, "the apron", stats);
                Assert.That(stats.MinY, Is.GreaterThan(0.9f), miner + " never sinks into the floor");
                Assert.That(stats.MaxY, Is.LessThan(1.7f), miner + " never climbs onto a rock, crystal or crate");
                Assert.That(stats.Frames, Is.GreaterThan(150));
                Debug.Log($"GreenMine underground treasure walk ({miner}): {stats.Frames} frames, player y {stats.MinY:F2}..{stats.MaxY:F2}.");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }
    }
}
#endif
