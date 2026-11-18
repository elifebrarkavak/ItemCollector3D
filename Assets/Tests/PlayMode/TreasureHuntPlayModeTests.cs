using System.Collections;
using ItemCollector3D.Treasure;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ItemCollector3D.Tests.PlayMode
{
    public sealed class TreasureHuntPlayModeTests
    {
        private const string GameplayScenePath = "Assets/Scenes/GameplayTest.unity";
        private Scene loadedScene;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Keep injected keyboard input independent of Editor window focus (see Docs/KnownIssues.md).
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null && keyboard.added)
            {
                InputSystem.RemoveDevice(keyboard);
            }

            keyboard = null;

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        }

        [Test]
        public void SignalMapping_IsMonotonic_AndHasNoSignalOutsideRange()
        {
            Assert.That(TreasureDetector.ProximityFor(20f, 2.5f, 15f), Is.EqualTo(0f));
            Assert.That(TreasureDetector.ProximityFor(15f, 2.5f, 15f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(TreasureDetector.ProximityFor(2.5f, 2.5f, 15f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(TreasureDetector.ProximityFor(1f, 2.5f, 15f), Is.EqualTo(1f).Within(1e-4f));

            float previous = float.MaxValue;
            for (float distance = 15f; distance >= 2.5f; distance -= 2.5f)
            {
                float interval = TreasureDetector.IntervalFor(
                    TreasureDetector.ProximityFor(distance, 2.5f, 15f), 1.6f, 0.15f);
                Assert.That(interval, Is.LessThanOrEqualTo(previous), $"Interval must not grow as distance shrinks ({distance} m).");
                previous = interval;
            }
        }

        [UnityTest]
        public IEnumerator GameplayTestScene_FullLoop_Detect_Dig_Reveal_Collect_EarnGold_OnlyOnce()
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                GameplayScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, "Could not load " + GameplayScenePath);
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(GameplayScenePath);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;

            TreasureDetector detector = Object.FindAnyObjectByType<TreasureDetector>();
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            TreasureSpot spot = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;
            Button digButton = FindButton("DigButton");
            Button collectButton = FindButton("CollectButton");
            TMP_Text goldText = GameObject.Find("GoldText").GetComponent<TMP_Text>();
            Image goldFill = GameObject.Find("GoldProgressFill").GetComponent<Image>();
            Transform visual = spot.transform.Find("Visual");
            Assert.That(detector, Is.Not.Null);
            Assert.That(hunt, Is.Not.Null);
            Assert.That(spot, Is.Not.Null);
            Assert.That(visual, Is.Not.Null);

            // Explore: far away -> hidden, no signal, no buttons, 0 gold.
            Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -30f));
            yield return null;
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden));
            Assert.That(visual.gameObject.activeSelf, Is.False, "Treasure must be invisible while hidden.");
            Assert.That(detector.HasSignal, Is.False);
            Assert.That(digButton.gameObject.activeSelf, Is.False);
            Assert.That(collectButton.gameObject.activeSelf, Is.False);
            Assert.That(hunt.TotalGold, Is.EqualTo(0));
            Assert.That(goldText.text, Is.EqualTo("0 / 500 GOLD"));
            Assert.That(goldFill.fillAmount, Is.EqualTo(0f).Within(1e-4f));

            // Detect: signal appears and gets faster as the player approaches.
            Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -12f));
            yield return null;
            yield return null;
            Assert.That(detector.HasSignal, Is.True);
            float farInterval = detector.CurrentInterval;
            Assert.That(digButton.gameObject.activeSelf, Is.False, "DIG must not show outside dig range.");

            Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -5f));
            yield return null;
            yield return null;
            float nearInterval = detector.CurrentInterval;
            Assert.That(nearInterval, Is.LessThan(farInterval), "Signal must get faster when closer.");

            int pulses = 0;
            detector.Pulsed += _ => pulses++;
            yield return WaitSeconds(1.0f);
            Assert.That(pulses, Is.GreaterThanOrEqualTo(2), "Detector should pulse repeatedly while close.");

            // Dig: button appears only in range; clicking reveals the treasure.
            Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -1f));
            yield return null;
            yield return null;
            Assert.That(digButton.gameObject.activeSelf, Is.True, "DIG must show in dig range.");
            Assert.That(collectButton.gameObject.activeSelf, Is.False, "COLLECT must not show before reveal.");
            digButton.onClick.Invoke();
            yield return WaitForRevealed(spot);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed));
            Assert.That(visual.gameObject.activeSelf, Is.True, "Treasure must be visible after DIG.");
            Assert.That(digButton.gameObject.activeSelf, Is.False);
            Assert.That(collectButton.gameObject.activeSelf, Is.True);
            Assert.That(hunt.TotalGold, Is.EqualTo(0), "Gold is earned on COLLECT, not DIG.");

            // Collect: gold goes 0 -> 10 on the existing HUD.
            collectButton.onClick.Invoke();
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(10));
            Assert.That(goldText.text, Is.EqualTo("10 / 500 GOLD"));
            Assert.That(goldFill.fillAmount, Is.EqualTo(0.02f).Within(1e-4f), "10 / 500 must fill the bar 2%.");
            Assert.That(visual.gameObject.activeSelf, Is.False);
            Assert.That(collectButton.gameObject.activeSelf, Is.False);

            // Second attempts must do nothing.
            collectButton.onClick.Invoke();
            digButton.onClick.Invoke();
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(10), "Treasure must not be collectable twice.");
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(spot.Collect(), Is.False);
            Assert.That(spot.Reveal(), Is.False);
#else
            Assert.Ignore("Scene based test is Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator GameplayTestScene_WalkWithWasdToTreasure_StaysGrounded_CameraFollows_SignalSpeedsUp()
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                GameplayScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(GameplayScenePath);
            SceneManager.SetActiveScene(loadedScene);

            TreasureDetector detector = Object.FindAnyObjectByType<TreasureDetector>();
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            DetectorSignalIndicator indicator = Object.FindAnyObjectByType<DetectorSignalIndicator>();
            TreasureSpot spot = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;
            Transform cameraTransform = Camera.main.transform;
            Button digButton = FindButton("DigButton");
            Button collectButton = FindButton("CollectButton");
            AudioSource audioSource = indicator.GetComponent<AudioSource>();
            Image flashDot = indicator.transform.Find("SignalDot").GetComponent<Image>();
            Assert.That(audioSource, Is.Not.Null, "Detector needs an AudioSource.");
            Assert.That(Object.FindAnyObjectByType<AudioListener>(), Is.Not.Null, "Scene needs an AudioListener.");

            keyboard = InputSystem.AddDevice<Keyboard>("Automated Test Keyboard");
            keyboard.MakeCurrent();

            // The scene camera starts ~10 m behind the Player and SmoothDamps in; let it settle so the
            // follow-distance check measures real following, not the start-up transient.
            yield return WaitSeconds(1.0f);

            float dig = spot.Definition.DigRadius;
            bool sawFlash = false;
            bool sawAudio = false;
            // The indicator updates the dot colour in its own Update, so flash/audio are sampled every frame
            // below (the colour is still idle inside the Pulsed event itself).
            detector.Pulsed += _ => sawAudio |= audioSource.isPlaying;

            float previousInterval = float.MaxValue;
            float firstInterval = 0f;
            float lastInterval = 0f;
            bool digShownInRange = false;
            float maxCameraDistance = 0f;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            // Phase 1: hold W (forward, +Z) until we are about 1 m short of the coin's Z. Phase 2: hold D (right, +X).
            float deadline = Time.time + 20f;
            while (Time.time < deadline)
            {
                Vector3 p = player.position;
                bool forwardPhase = p.z < spot.transform.position.z - 1f;
                bool strafePhase = !forwardPhase && p.x < spot.transform.position.x - 1.5f;
                if (!forwardPhase && !strafePhase)
                {
                    break;
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(forwardPhase ? Key.W : Key.D));
                yield return null;

                p = player.position;
                float distance = spot.HorizontalDistanceTo(p);
                Color dotColor = flashDot.color;
                sawFlash |= dotColor.r > 0.8f && dotColor.g > 0.6f && dotColor.b < 0.4f; // idle dot is grey (0.35, 0.35, 0.35)
                sawAudio |= audioSource.isPlaying;
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
                maxCameraDistance = Mathf.Max(maxCameraDistance, Vector3.Distance(cameraTransform.position, p));

                if (detector.HasSignal)
                {
                    if (firstInterval == 0f)
                    {
                        firstInterval = detector.CurrentInterval;
                    }

                    Assert.That(detector.CurrentInterval, Is.LessThanOrEqualTo(previousInterval + 0.02f),
                        "Signal interval must not grow while walking towards the treasure.");
                    previousInterval = detector.CurrentInterval;
                    lastInterval = detector.CurrentInterval;
                }

                if (distance > dig + 0.3f)
                {
                    Assert.That(digButton.gameObject.activeSelf, Is.False, $"DIG must stay hidden at {distance:F1} m.");
                }
                else if (distance < dig - 0.3f)
                {
                    Assert.That(digButton.gameObject.activeSelf, Is.True, $"DIG must show at {distance:F1} m.");
                    digShownInRange = true;
                }
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;

            Assert.That(spot.HorizontalDistanceTo(player.position), Is.LessThan(dig), "Walking should end inside dig range.");
            Assert.That(minY, Is.GreaterThan(0.8f).And.LessThan(1.4f), "Player must stay on the ground while walking.");
            Assert.That(maxY, Is.LessThan(1.4f));
            Assert.That(maxCameraDistance, Is.LessThan(8f), "Camera must keep following the Player.");
            Assert.That(firstInterval, Is.GreaterThan(0f), "Signal must have started while walking in.");
            Assert.That(lastInterval, Is.LessThan(firstInterval), "Signal must get faster closer to the treasure.");
            Assert.That(digShownInRange, Is.True);
            Assert.That(sawFlash, Is.True, "Indicator dot must flash on a pulse.");
            Assert.That(sawAudio, Is.True, "AudioSource must be playing right after a pulse (trigger check, not an audibility check).");
            Assert.That(indicator.PulseCount, Is.GreaterThan(1));

            digButton.onClick.Invoke();
            yield return WaitForRevealed(spot);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed));
            collectButton.onClick.Invoke();
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(10));
            collectButton.onClick.Invoke();
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(10), "Second collect must not add gold.");
#else
            Assert.Ignore("Scene based test is Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator GameplayTestScene_AllFourTreasureTypes_UseSameLoop_GoldAccumulates_NoDoubleCollect()
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                GameplayScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(GameplayScenePath);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;

            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            TMP_Text goldText = GameObject.Find("GoldText").GetComponent<TMP_Text>();
            Transform player = GameObject.Find("Player").transform;
            Button digButton = FindButton("DigButton");
            Button collectButton = FindButton("CollectButton");

            // Gold Coin already lives in the scene; spawn the other three from their prefabs, far from each other.
            var spots = new System.Collections.Generic.List<(TreasureSpot spot, int expectedTotal, TreasureRarity rarity)>
            {
                (Object.FindAnyObjectByType<TreasureSpot>(), 10, TreasureRarity.Common),
                (Spawn("Treasure_GoldNugget", new Vector3(-30f, 0f, 10f)), 35, TreasureRarity.Uncommon),
                (Spawn("Treasure_GoldBar", new Vector3(-30f, 0f, 40f)), 85, TreasureRarity.Rare),
                (Spawn("Treasure_AncientArtifact", new Vector3(30f, 0f, 10f)), 185, TreasureRarity.Epic)
            };
            yield return null;

            foreach ((TreasureSpot spot, int expectedTotal, TreasureRarity rarity) in spots)
            {
                Assert.That(spot.Definition.Rarity, Is.EqualTo(rarity));
                Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -1f));
                yield return null;
                yield return null;

                Assert.That(digButton.gameObject.activeSelf, Is.True, $"DIG must show for {spot.Definition.DisplayName}.");
                digButton.onClick.Invoke();
                yield return WaitForRevealed(spot);
                Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed));
                Assert.That(collectButton.gameObject.activeSelf, Is.True);

                collectButton.onClick.Invoke();
                yield return null;
                Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
                Assert.That(hunt.TotalGold, Is.EqualTo(expectedTotal), $"Gold after {spot.Definition.DisplayName}.");
                Assert.That(goldText.text, Is.EqualTo($"{expectedTotal} / 500 GOLD"));

                // Second attempts add nothing.
                collectButton.onClick.Invoke();
                digButton.onClick.Invoke();
                yield return null;
                Assert.That(hunt.TotalGold, Is.EqualTo(expectedTotal), $"{spot.Definition.DisplayName} must not be collectable twice.");
            }
#else
            Assert.Ignore("Scene based test is Editor only.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        private Button FindButton(string name)
        {
            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button.name == name && button.gameObject.scene == loadedScene)
                {
                    return button;
                }
            }

            Assert.Fail("Button not found in GameplayTest: " + name);
            return null;
        }

        private TreasureSpot Spawn(string prefabName, Vector3 position)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Treasure/" + prefabName + ".prefab");
            Assert.That(prefab, Is.Not.Null, "Missing prefab " + prefabName);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(instance, loadedScene);
            return instance.GetComponent<TreasureSpot>();
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        /// <summary>The dig now takes a short time (TreasureHuntController.digDuration): wait for the reveal.</summary>
        private static IEnumerator WaitForRevealed(TreasureSpot spot, float timeoutSeconds = 3f)
        {
            float end = Time.time + timeoutSeconds;
            while (spot.State == TreasureState.Hidden && Time.time < end)
            {
                yield return null;
            }

            yield return null; // let the controller refresh the buttons after the reveal
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }
#endif
    }
}
