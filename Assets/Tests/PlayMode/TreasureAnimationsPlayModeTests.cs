#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Stage 12B animations in the real Green Mine: the miners' dig swing (Barnaby and Ruby), the treasure rising out of the ground, and the collect
    /// animation, for a normal and an Epic treasure. Checks timing against the dig sound / reward events, the exact return to the authored pose after a
    /// finished, cancelled or paused animation, and that gold is still paid once. In-memory stores keep the developer's save untouched.
    /// </summary>
    public sealed class TreasureAnimationsPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";
        private static readonly string[] Miners = { CharacterSelection.MaleId, CharacterSelection.FemaleId };

        private Scene scene;
        private float previousTimeScale;
        private MemoryProgressStore store;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.Current.ResetToDefaults();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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

        // ------------------------------------------------------------------ helpers

        private IEnumerator LoadScene()
        {
            store = new MemoryProgressStore();
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

            Assert.Fail("Not found in GreenMine: " + typeof(T).Name);
            return null;
        }

        private Button FindButton(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == name)
                    {
                        return button;
                    }
                }
            }

            Assert.Fail("Button not found: " + name);
            return null;
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

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
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

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        private IEnumerator StandAt(TreasureSpot spot, float distance = 1.5f)
        {
            Teleport(Player, PlaceAtDistance(spot, distance));
            Button dig = FindButton("DigButton");
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        private static void SetDigDuration(TreasureHuntController hunt, float seconds)
        {
            typeof(TreasureHuntController).GetField("digDuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hunt, seconds);
        }

        private static float ArmSwing(MinerAnimator miner) => Quaternion.Angle(miner.RightArm.localRotation, miner.RightArmRest);

        private static void AssertMinerAtRest(MinerAnimator miner, string who)
        {
            Assert.That(miner.DigWeight, Is.EqualTo(0f), who + ": no dig pose left");
            Assert.That(ArmSwing(miner), Is.LessThan(4f), who + ": right arm back to its normal pose (the idle sway is under 3 degrees)");
            Assert.That(Quaternion.Angle(miner.LeftArm.localRotation, miner.LeftArmRest), Is.LessThan(4f), who + ": left arm too");
            Assert.That((miner.Pickaxe.localPosition - miner.PickRestPosition).sqrMagnitude, Is.LessThan(1e-8f), who + ": the pickaxe is back in the hand");
            Assert.That(Quaternion.Angle(miner.Pickaxe.localRotation, miner.PickRestRotation), Is.LessThan(0.01f), who + ": and the right way round");
        }

        // ================================================================ the miners

        [UnityTest]
        public IEnumerator Dig_SwingsBothMinersArms_InSyncWithTheDigSound_LeavesTheCameraAlone_AndEndsAtRest()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return LoadScene();
                var digger = One<PlayerDigAnimator>();
                var sound = One<DigSoundPlayer>();
                var hunt = One<TreasureHuntController>();
                MinerAnimator miner = digger.Visual.GetComponentInChildren<MinerAnimator>(true);
                Assert.That(miner, Is.Not.Null, id + ": the miner model carries the animator");
                Assert.That(miner.Pickaxe, Is.Not.Null, id + ": and holds a pickaxe");
                TreasureSpot spot = Spot("green_mine.camp_01");
                Button dig = FindButton("DigButton");
                yield return StandAt(spot);
                Camera camera = Camera.main;
                Vector3 playerPosition = Player.position;
                Quaternion playerRotation = Player.rotation;
                float cameraDistance = Vector3.Distance(camera.transform.position, Player.position);
                SetDigDuration(hunt, 0.45f);
                AssertMinerAtRest(miner, id + " before the dig");

                var impactPose = new List<float>();
                float maxSwing = 0f, maxCameraDrift = 0f;
                int lastCount = sound.PlayCount;
                dig.onClick.Invoke();
                float end = Time.realtimeSinceStartup + 2f;
                while (spot.State == TreasureState.Hidden && Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    maxSwing = Mathf.Max(maxSwing, ArmSwing(miner));
                    maxCameraDrift = Mathf.Max(maxCameraDrift, Mathf.Abs(Vector3.Distance(camera.transform.position, Player.position) - cameraDistance));
                    if (sound.PlayCount != lastCount)
                    {
                        impactPose.Add(miner.DigAmount); // the pose at the very frame the dig sound starts
                        lastCount = sound.PlayCount;
                    }
                }

                Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), id);
                Assert.That(impactPose.Count, Is.EqualTo(digger.Strokes), id + ": one sound per stroke");
                foreach (float pose in impactPose)
                {
                    Assert.That(pose, Is.GreaterThanOrEqualTo(0.8f), id + ": the pick is landing when the dig sound plays (pose " + pose.ToString("F2") + ")");
                }

                Assert.That(maxSwing, Is.GreaterThan(90f), id + ": the arms visibly swing (largest swing " + maxSwing.ToString("F0") + " degrees)");
                Assert.That(Vector3.Distance(Player.position, playerPosition), Is.LessThan(0.02f), id + ": the Player itself never moves");
                Assert.That(Quaternion.Angle(Player.rotation, playerRotation), Is.LessThan(0.5f), id + ": nor turns");
                Assert.That(maxCameraDrift, Is.LessThan(0.1f), id + ": the camera keeps its distance while digging");

                yield return new WaitForSecondsRealtime(0.2f);
                AssertMinerAtRest(miner, id + " after the dig");
                Assert.That(digger.IsAtRest(), Is.True, id + ": the body is at rest too");
                Debug.Log($"Dig swing ({id}): largest arm swing {maxSwing:F0} deg, pose at the {impactPose.Count} sound impacts: {string.Join(", ", impactPose.ConvertAll(p => p.ToString("F2")))}, camera drift {maxCameraDrift:F3} m");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator AfterADig_TheJoystickStillMovesBothMiners()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return LoadScene();
                var controller = Player.GetComponent<ThirdPersonPlayerController>();
                var hunt = One<TreasureHuntController>();
                TreasureSpot spot = Spot("green_mine.camp_01");
                yield return StandAt(spot);
                SetDigDuration(hunt, 0.45f);
                FindButton("DigButton").onClick.Invoke();
                yield return WaitUntil(() => spot.State == TreasureState.Revealed, 2f);
                yield return new WaitForSecondsRealtime(0.2f);

                Vector3 start = Player.position;
                controller.SetMoveInput(new Vector2(0f, 1f));
                yield return new WaitForSecondsRealtime(0.8f);
                controller.ClearMoveInput();
                Vector3 moved = Player.position - start;
                moved.y = 0f;
                Assert.That(moved.magnitude, Is.GreaterThan(1.2f), id + ": the joystick still walks the miner after a dig");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CancelledDig_EndsTheSwingAtOnce_AndNothingIsRevealed()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return LoadScene();
                var digger = One<PlayerDigAnimator>();
                var sound = One<DigSoundPlayer>();
                var hunt = One<TreasureHuntController>();
                MinerAnimator miner = digger.Visual.GetComponentInChildren<MinerAnimator>(true);
                TreasureSpot spot = Spot("green_mine.camp_01");
                yield return StandAt(spot);
                SetDigDuration(hunt, 1.5f);
                FindButton("DigButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(digger.IsPlaying, Is.True, id);
                Assert.That(miner.DigWeight, Is.GreaterThan(0.5f), id + ": mid-swing");

                Teleport(Player, PlaceAtDistance(spot, spot.Definition.DigRadius + 3f)); // the player walks away
                yield return null;
                yield return null;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(digger.IsPlaying, Is.False, id + ": the dig motion ended");
                Assert.That(digger.CancelCount, Is.EqualTo(1), id);
                AssertMinerAtRest(miner, id + " after the cancel");
                Assert.That(digger.IsAtRest(), Is.True, id);
                Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden), id + ": nothing was revealed");
                Assert.That(sound.RevealCount, Is.EqualTo(0), id + ": and no discovery sound");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTheDigSwing_AndResumeFinishesIt()
        {
            yield return LoadScene();
            var digger = One<PlayerDigAnimator>();
            var hunt = One<TreasureHuntController>();
            MinerAnimator miner = digger.Visual.GetComponentInChildren<MinerAnimator>(true);
            TreasureSpot spot = Spot("green_mine.camp_01");
            yield return StandAt(spot);
            SetDigDuration(hunt, 1.5f);
            FindButton("DigButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(digger.IsPlaying, Is.True);

            GamePause.Pause();
            yield return null;
            float frozenAmount = miner.DigAmount, frozenSwing = ArmSwing(miner);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(miner.DigAmount, Is.EqualTo(frozenAmount).Within(1e-4f), "the swing stands still while paused");
            Assert.That(ArmSwing(miner), Is.EqualTo(frozenSwing).Within(0.01f));
            Assert.That(digger.IsPlaying, Is.True, "the dig is still running");
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden), "and the treasure waits");

            GamePause.Resume();
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "the dig finishes after Resume");
            yield return new WaitForSecondsRealtime(0.2f);
            AssertMinerAtRest(miner, "after Resume");
        }

        // ================================================================ the treasure

        private sealed class RevealResult
        {
            public float Depth, Height, Spin, Seconds;
            public bool Hovering, SparklesPlaying;
            public Vector3 StartLocal, RestLocal;
        }

        private IEnumerator DigAndMeasureReveal(string id, RevealResult result)
        {
            TreasureSpot spot = Spot(id);
            var hunt = One<TreasureHuntController>();
            SetDigDuration(hunt, 0.45f);
            yield return StandAt(spot);
            Transform visual = spot.VisualRoot.transform;
            Vector3 authored = visual.localPosition;
            FindButton("DigButton").onClick.Invoke();
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), id);
            TreasureVisualAnimator animator = spot.VisualAnimator;
            Assert.That(animator, Is.Not.Null, id + ": the spot animates its visual");
            Assert.That(spot.VisualRoot.activeSelf, Is.True, id + ": visible at once (the rise is the animation)");
            Assert.That(animator.IsRevealing, Is.True, id + ": rising");
            float started = Time.realtimeSinceStartup;
            result.RestLocal = authored;
            result.StartLocal = visual.localPosition;
            while (animator.IsRevealing && Time.realtimeSinceStartup - started < 2f)
            {
                yield return null;
            }

            result.Seconds = Time.realtimeSinceStartup - started;
            result.Depth = animator.MaxDepthBelowRest;
            result.Height = animator.MaxHeightAboveRest;
            result.Spin = animator.MaxSpinDegrees;
            result.Hovering = animator.IsHovering;
            result.SparklesPlaying = animator.Sparkles != null && animator.Sparkles.isPlaying;
        }

        [UnityTest]
        public IEnumerator NormalAndEpicTreasure_RiseOutOfTheGround_AndLookDifferent()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return LoadScene();

                var normal = new RevealResult();
                yield return DigAndMeasureReveal("green_mine.camp_01", normal);
                TreasureSpot normalSpot = Spot("green_mine.camp_01");
                Assert.That(normal.StartLocal.y, Is.LessThan(normal.RestLocal.y - 0.3f), $"{id}: a normal treasure starts below the ground line");
                Assert.That(normal.Depth, Is.GreaterThan(0.4f), id);
                Assert.That(normal.Seconds, Is.InRange(0.2f, 0.75f), $"{id}: short and fluid ({normal.Seconds:F2} s)");
                Assert.That(normal.Hovering, Is.False, id + ": a normal treasure just rests");
                Assert.That(normalSpot.VisualAnimator.IsAnimating, Is.False, id);
                Assert.That((normalSpot.VisualRoot.transform.localPosition - normal.RestLocal).sqrMagnitude, Is.LessThan(1e-8f), id + ": it ends exactly where it was authored");

                var epic = new RevealResult();
                yield return DigAndMeasureReveal("green_mine.mine_room_01", epic);
                TreasureSpot epicSpot = Spot("green_mine.mine_room_01");
                Assert.That(epicSpot.IsEpic, Is.True);
                Assert.That(epic.Depth, Is.GreaterThan(normal.Depth + 0.15f), $"{id}: an Epic one starts deeper and rises further");
                Assert.That(epic.Height, Is.GreaterThanOrEqualTo(TreasureVisualAnimator.EpicHoverHeight * 0.9f), $"{id}: and climbs above its resting pose");
                Assert.That(epic.Spin, Is.GreaterThan(normal.Spin * 2f), $"{id}: and spins while it comes up");
                Assert.That(epic.Seconds, Is.GreaterThan(normal.Seconds + 0.15f), $"{id}: and takes longer");
                Assert.That(epic.Hovering, Is.True, $"{id}: then it hovers until it is collected");
                Assert.That(epicSpot.VisualRoot.transform.localPosition.y, Is.GreaterThan(epic.RestLocal.y + 0.05f), id + ": floating above the ground");
                Debug.Log($"Reveal ({id}): normal depth {normal.Depth:F2} m in {normal.Seconds:F2} s, spin {normal.Spin:F0} deg; Epic depth {epic.Depth:F2} m, peak {epic.Height:F2} m, spin {epic.Spin:F0} deg in {epic.Seconds:F2} s, sparkles playing {epic.SparklesPlaying}");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        private IEnumerator CollectAndWatch(string id, System.Action<TreasureSpot, float, float, float, float, float> check)
        {
            TreasureSpot spot = Spot(id);
            var hunt = One<TreasureHuntController>();
            var rewards = One<RewardFeedbackPresenter>();
            var collectSound = One<CollectSoundPlayer>();
            Button collect = FindButton("CollectButton");
            Vector3 rest = spot.VisualRoot.transform.localPosition;      // the authored pose, taken while the treasure is still buried
            Vector3 restScale = spot.VisualRoot.transform.localScale;
            yield return DigAndMeasureReveal(id, new RevealResult());
            yield return new WaitForSecondsRealtime(0.15f);
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);

            int goldBefore = hunt.TotalGold, rewardsBefore = rewards.RewardsShown, soundsBefore = collectSound.PlayCount;
            Transform visual = spot.VisualRoot.transform;
            collect.onClick.Invoke();
            // the reward does not wait for the animation: it all happens in this frame
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), id);
            Assert.That(hunt.TotalGold, Is.EqualTo(goldBefore + spot.Definition.GoldValue), id + ": gold paid at once");
            Assert.That(rewards.RewardsShown, Is.EqualTo(rewardsBefore + 1), id + ": the gold notification shows at once");
            Assert.That(collectSound.PlayCount, Is.EqualTo(soundsBefore + 1), id + ": and so does the sound");
            Assert.That(spot.VisualRoot.activeSelf, Is.True, id + ": while the treasure itself plays its collect animation");
            Assert.That(spot.VisualAnimator.IsCollecting, Is.True, id);

            float started = Time.realtimeSinceStartup;
            while (spot.VisualAnimator.IsCollecting && Time.realtimeSinceStartup - started < 2f)
            {
                yield return null;
            }

            float seconds = Time.realtimeSinceStartup - started;
            TreasureVisualAnimator animator = spot.VisualAnimator;
            Assert.That(spot.VisualRoot.activeSelf, Is.False, id + ": gone when it ends");
            Assert.That((visual.localPosition - rest).sqrMagnitude, Is.LessThan(1e-8f), id + ": and its authored pose is back (a later reload / restore sees the normal pose)");
            Assert.That((visual.localScale - restScale).sqrMagnitude, Is.LessThan(1e-8f), id);
            Assert.That(animator.CollectCount, Is.EqualTo(1), id);
            check(spot, seconds, animator.MaxHeightAboveRest, animator.MaxScaleFactor, animator.MaxSpinDegrees, 0f);

            // a second collect (or DIG) gives nothing and plays nothing
            int gold = hunt.TotalGold;
            collect.onClick.Invoke();
            FindButton("DigButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(hunt.TotalGold, Is.EqualTo(gold), id + ": no second reward");
            Assert.That(rewards.RewardsShown, Is.EqualTo(rewardsBefore + 1), id);
            Assert.That(animator.CollectCount, Is.EqualTo(1), id + ": the animation never restarts");
            Assert.That(JsonUtility.FromJson<GameProgressData>(store.Json).collectedTreasureIds, Contains.Item(spot.SpotId), id + ": saved (in memory)");
            Assert.That(JsonUtility.FromJson<GameProgressData>(store.Json).totalGold, Is.EqualTo(gold), id);
        }

        [UnityTest]
        public IEnumerator Collect_PlaysAShortAnimation_WithoutDelayingTheReward_AndPaysOnce_ForNormalAndEpic()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return LoadScene();
                float normalSeconds = 0f, normalRise = 0f, normalSpin = 0f, normalScale = 0f;
                yield return CollectAndWatch("green_mine.camp_01", (spot, seconds, rise, scale, spin, _) =>
                {
                    normalSeconds = seconds; normalRise = rise; normalScale = scale; normalSpin = spin;
                });
                Assert.That(normalSeconds, Is.InRange(0.25f, 0.8f), $"{id}: a short animation ({normalSeconds:F2} s)");
                Assert.That(normalRise, Is.GreaterThan(0.5f), $"{id}: it floats up");
                Assert.That(normalScale, Is.GreaterThan(1.05f), $"{id}: with a little swell");

                float epicSeconds = 0f, epicRise = 0f, epicSpin = 0f, epicScale = 0f;
                yield return CollectAndWatch("green_mine.mine_room_01", (spot, seconds, rise, scale, spin, _) =>
                {
                    epicSeconds = seconds; epicRise = rise; epicScale = scale; epicSpin = spin;
                });
                Assert.That(epicSeconds, Is.GreaterThan(normalSeconds + 0.2f), $"{id}: Epic lasts longer");
                Assert.That(epicRise, Is.GreaterThan(normalRise + 0.5f), $"{id}: flies higher");
                Assert.That(epicScale, Is.GreaterThan(normalScale + 0.1f), $"{id}: swells more");
                Assert.That(epicSpin, Is.GreaterThan(normalSpin + 200f), $"{id}: and spins more");
                Debug.Log($"Collect animation ({id}): normal {normalSeconds:F2} s, rise {normalRise:F2} m, swell x{normalScale:F2}, spin {normalSpin:F0} deg; Epic {epicSeconds:F2} s, rise {epicRise:F2} m, swell x{epicScale:F2}, spin {epicSpin:F0} deg");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTheCollectAnimation_AndResumeFinishesIt()
        {
            yield return LoadScene();
            TreasureSpot spot = Spot("green_mine.camp_01");
            yield return DigAndMeasureReveal(spot.SpotId, new RevealResult());
            Button collect = FindButton("CollectButton");
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            collect.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(spot.VisualAnimator.IsCollecting, Is.True);

            GamePause.Pause();
            yield return null;
            Vector3 frozen = spot.VisualRoot.transform.localPosition;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(spot.VisualAnimator.IsCollecting, Is.True, "still mid-animation while paused");
            Assert.That((spot.VisualRoot.transform.localPosition - frozen).sqrMagnitude, Is.LessThan(1e-8f), "and frozen");

            GamePause.Resume();
            yield return WaitUntil(() => !spot.VisualAnimator.IsCollecting, 2f);
            Assert.That(spot.VisualAnimator.IsCollecting, Is.False, "it finishes after Resume");
            Assert.That(spot.VisualRoot.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ACollectedTreasure_StaysGone_AfterTheSceneIsReloaded_WithTheSameSave()
        {
            yield return LoadScene();
            TreasureSpot spot = Spot("green_mine.camp_01");
            string id = spot.SpotId;
            yield return CollectAndWatch(id, (s, seconds, rise, scale, spin, _) => { });
            MemoryProgressStore savedGame = store;

            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return null;
            store = savedGame;
            ProgressManager.StoreFactoryOverride = () => savedGame;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return null;
            TreasureSpot again = Spot(id);
            Assert.That(again.State, Is.EqualTo(TreasureState.Collected), "the save restores it as collected");
            Assert.That(again.VisualRoot.activeSelf, Is.False, "straight away, with no animation");
            Assert.That(again.VisualAnimator.IsAnimating, Is.False);
        }
    }
}
#endif
