#if UNITY_EDITOR
using System.Collections;
using ItemCollector3D.Treasure;
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
    /// Dig feedback: a short dig phase (player visual motion, dirt particles, dig sound) between DIG and the reveal.
    /// Sound audibility is NOT tested - only that it is triggered. Visual quality is checked separately in the Game View.
    /// </summary>
    public sealed class DigFeedbackPlayModeTests
    {
        private const string GameplayScenePath = "Assets/Scenes/GameplayTest.unity";
        private Scene loadedScene;
        private GameObject spawnedRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (spawnedRoot != null)
            {
                Object.Destroy(spawnedRoot);
            }

            spawnedRoot = null;
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
        }

        [Test]
        public void StrokeCurve_IsZeroAtStartAndEnd_AndPeaksAtEachStroke()
        {
            Assert.That(PlayerDigAnimator.StrokeCurve(0f, 2), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(PlayerDigAnimator.StrokeCurve(1f, 2), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(PlayerDigAnimator.StrokeCurve(0.25f, 2), Is.EqualTo(1f).Within(1e-5f), "first stroke impact");
            Assert.That(PlayerDigAnimator.StrokeCurve(0.75f, 2), Is.EqualTo(1f).Within(1e-5f), "second stroke impact");
            Assert.That(PlayerDigAnimator.StrokeCurve(0.5f, 2), Is.EqualTo(0f).Within(1e-5f), "between strokes");
        }

        [UnityTest]
        public IEnumerator Animator_ReturnsVisualToRestPose_WhenDisabledMidDig()
        {
            spawnedRoot = new GameObject("Test Dig Player");
            var visual = new GameObject("Visual").transform;
            visual.SetParent(spawnedRoot.transform, false);
            visual.localPosition = new Vector3(0f, 0.2f, 0f);
            PlayerDigAnimator animator = spawnedRoot.AddComponent<PlayerDigAnimator>();
            yield return null;

            animator.Play(0.6f);
            yield return WaitSeconds(0.15f);
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(animator.IsAtRest(), Is.False, "Visual must have moved during the dig.");

            animator.enabled = false;
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(animator.IsAtRest(), Is.True, "Visual must return to its exact rest pose.");
            Assert.That(visual.localPosition, Is.EqualTo(new Vector3(0f, 0.2f, 0f)));
        }

        [UnityTest]
        public IEnumerator Scene_DigButton_IsAvailableOnlyInsideDigRange()
        {
            yield return LoadScene();
            TreasureSpot coin = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;
            Button digButton = FindButton("DigButton");
            float digRadius = coin.Definition.DigRadius;

            Teleport(player, coin.transform.position + new Vector3(0f, 1.1f, -(digRadius + 0.8f)));
            yield return null;
            yield return null;
            Assert.That(digButton.gameObject.activeSelf, Is.False, "DIG must stay hidden outside the dig range.");

            Teleport(player, coin.transform.position + new Vector3(0f, 1.1f, -(digRadius - 0.6f)));
            yield return null;
            yield return null;
            Assert.That(digButton.gameObject.activeSelf, Is.True, "DIG must show inside the dig range.");
            Assert.That(digButton.interactable, Is.True);
            Assert.That(coin.State, Is.EqualTo(TreasureState.Hidden));
        }

        [UnityTest]
        public IEnumerator Scene_Dig_PlaysFeedbackOnce_BlocksRepeatedDig_ThenRevealsAndCollectsOnce()
        {
            yield return LoadScene();

            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            DigFeedbackPresenter digPresenter = Object.FindAnyObjectByType<DigFeedbackPresenter>();
            DigDirtEffect dirt = Object.FindAnyObjectByType<DigDirtEffect>();
            DigSoundPlayer digSound = Object.FindAnyObjectByType<DigSoundPlayer>();
            PlayerDigAnimator animator = Object.FindAnyObjectByType<PlayerDigAnimator>();
            RewardFeedbackPresenter reward = Object.FindAnyObjectByType<RewardFeedbackPresenter>();
            TreasureSpot spot = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;
            Transform cameraTransform = Camera.main.transform;
            Button digButton = FindButton("DigButton");
            Button collectButton = FindButton("CollectButton");
            TMP_Text goldText = GameObject.Find("GoldText").GetComponent<TMP_Text>();
            Transform treasureVisual = spot.transform.Find("Visual");
            Assert.That(digPresenter, Is.Not.Null, "DigFeedback must be in GameplayTest.");
            Assert.That(dirt, Is.Not.Null);
            Assert.That(digSound, Is.Not.Null);
            Assert.That(animator, Is.Not.Null, "Player must carry a PlayerDigAnimator.");
            Assert.That(animator.Visual, Is.Not.Null);
            Assert.That(animator.Visual.parent, Is.EqualTo(player), "The animated visual must be a CHILD of the player.");
            Assert.That(hunt.DigDuration, Is.InRange(0.35f, 0.6f), "Dig should take roughly 0.35-0.6 s.");

            int started = 0;
            int completed = 0;
            hunt.DigStarted += (_, _) => started++;
            hunt.DigCompleted += _ => completed++;

            Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -1.5f));
            yield return WaitSeconds(1.0f); // let the scene camera settle behind the player
            Vector3 startPosition = player.position;
            Quaternion startRotation = player.rotation;

            // --- DIG pressed
            float digPressedAt = Time.time;
            digButton.onClick.Invoke();
            yield return null;
            Assert.That(hunt.IsDigging, Is.True);
            Assert.That(started, Is.EqualTo(1));
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden), "Treasure is revealed only when the dig finishes.");
            Assert.That(treasureVisual.gameObject.activeSelf, Is.False);
            Assert.That(digButton.gameObject.activeSelf, Is.True, "DIG stays visible while digging...");
            Assert.That(digButton.interactable, Is.False, "...but disabled.");
            Assert.That(collectButton.gameObject.activeSelf, Is.False, "COLLECT must not show before the reveal.");

            // repeated DIG while digging must be ignored (button disabled, but Invoke()/double taps must be safe as well)
            digButton.onClick.Invoke();
            yield return null;
            digButton.onClick.Invoke();
            Assert.That(started, Is.EqualTo(1), "No second dig while one is running.");
            Assert.That(digPresenter.DigsPresented, Is.EqualTo(1));
            Assert.That(animator.PlayCount, Is.EqualTo(1));

            // --- during the dig: the real player transform and camera are untouched
            bool sawSoundPlaying = false;
            int maxLiveParticles = 0;
            float maxCameraDistance = 0f;
            float maxPlayerDrift = 0f;
            float maxRotationDrift = 0f;
            float deadline = Time.time + 3f;
            while (spot.State == TreasureState.Hidden && Time.time < deadline)
            {
                yield return null;
                sawSoundPlaying |= digSound.IsPlaying;
                maxLiveParticles = Mathf.Max(maxLiveParticles, dirt.LiveParticleCount);
                maxCameraDistance = Mathf.Max(maxCameraDistance, Vector3.Distance(cameraTransform.position, player.position));
                Vector3 drift = player.position - startPosition;
                drift.y = 0f;
                maxPlayerDrift = Mathf.Max(maxPlayerDrift, drift.magnitude);
                maxRotationDrift = Mathf.Max(maxRotationDrift, Quaternion.Angle(player.rotation, startRotation));
            }

            float digTook = Time.time - digPressedAt;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "Dig must finish with a reveal.");
            Assert.That(digTook, Is.GreaterThanOrEqualTo(hunt.DigDuration - 0.05f), "Reveal must not come before the dig time.");
            Assert.That(digTook, Is.LessThan(hunt.DigDuration + 1.0f));
            Assert.That(maxPlayerDrift, Is.LessThan(0.01f), "Player root position must not be moved by the dig animation.");
            Assert.That(maxRotationDrift, Is.LessThan(0.1f), "Player root rotation must not be changed by the dig animation.");
            Assert.That(maxCameraDistance, Is.LessThan(8f), "Camera must keep following the player.");
            Assert.That(animator.MaxVisualOffset, Is.GreaterThan(0.05f), "The visual child must visibly move.");
            Assert.That(animator.MaxTiltApplied, Is.GreaterThan(5f));

            // --- feedback was triggered once per stroke
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(digPresenter.StrokesTriggered, Is.EqualTo(animator.Strokes));
            Assert.That(digPresenter.RevealPuffs, Is.EqualTo(1));
            Assert.That(digSound.PlayCount, Is.EqualTo(animator.Strokes), "One dig sound per stroke, no extra repeats.");
            Assert.That(sawSoundPlaying, Is.True, "AudioSource must be playing after a stroke (trigger check, not audibility).");
            Assert.That(float.IsFinite(digSound.LastPitch) && digSound.LastPitch > 0f, Is.True);
            int expectedDirt = dirt.ParticlesPerStroke * animator.Strokes + dirt.ParticlesOnReveal;
            Assert.That(dirt.TotalEmitted, Is.EqualTo(expectedDirt));
            Assert.That(dirt.TotalEmitted, Is.LessThanOrEqualTo(40), "Keep the dirt count low for Android.");
            Assert.That(maxLiveParticles, Is.GreaterThan(0), "Dirt particles must actually be alive during/after the dig.");
            Assert.That(dirt.LiveParticleCount, Is.LessThanOrEqualTo(dirt.MaxParticleCap));
            Vector3 flat = dirt.LastEmitPosition - spot.transform.position;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.LessThan(0.05f), "Dirt appears at the dig spot.");
            AssertEarthTone(dirt.LightDirt);
            AssertEarthTone(dirt.DarkDirt);

            // --- reveal state and buttons, and the visual is back to normal
            yield return null;
            yield return null;
            yield return null;
            Assert.That(treasureVisual.gameObject.activeSelf, Is.True, "Treasure must be visible after the dig.");
            Assert.That(hunt.IsDigging, Is.False);
            Assert.That(digButton.gameObject.activeSelf, Is.False);
            Assert.That(collectButton.gameObject.activeSelf, Is.True);
            Assert.That(hunt.TotalGold, Is.EqualTo(0), "Gold is earned on COLLECT, not DIG.");
            yield return WaitSeconds(0.2f);
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(animator.IsAtRest(), Is.True, "Player visual must return to its normal pose after the dig.");

            // --- COLLECT still works and gives gold exactly once
            collectButton.onClick.Invoke();
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(10));
            Assert.That(goldText.text, Is.EqualTo("10 / 500 GOLD"));
            Assert.That(reward.RewardsShown, Is.EqualTo(1));
            Assert.That(reward.LastMessage, Is.EqualTo("+10 Gold"));

            collectButton.onClick.Invoke();
            digButton.onClick.Invoke();
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(10), "Gold only once.");
            Assert.That(started, Is.EqualTo(1), "A collected treasure cannot be dug again.");
            Assert.That(digPresenter.DigsPresented, Is.EqualTo(1));
            Assert.That(reward.RewardsShown, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Animator_Cancel_RestoresRestPose_AndIsIdempotent()
        {
            spawnedRoot = new GameObject("Test Dig Player 2");
            var visual = new GameObject("Visual").transform;
            visual.SetParent(spawnedRoot.transform, false);
            PlayerDigAnimator animator = spawnedRoot.AddComponent<PlayerDigAnimator>();
            yield return null;

            animator.Play(0.6f);
            yield return WaitSeconds(0.15f);
            Assert.That(animator.IsAtRest(), Is.False);

            animator.Cancel();
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(animator.IsAtRest(), Is.True, "Cancel must put the visual back to its start pose.");
            Assert.That(animator.CancelCount, Is.EqualTo(1));

            animator.Cancel();
            Assert.That(animator.CancelCount, Is.EqualTo(1), "Cancelling an idle animator does nothing.");
        }

        [UnityTest]
        public IEnumerator Scene_DigOnly_RevealsWithoutCollecting_AndNeverAutoCollects()
        {
            yield return LoadScene();

            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            RewardFeedbackPresenter reward = Object.FindAnyObjectByType<RewardFeedbackPresenter>();
            TreasureSpot spot = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;
            Button digButton = FindButton("DigButton");
            Button collectButton = FindButton("CollectButton");
            TMP_Text goldText = GameObject.Find("GoldText").GetComponent<TMP_Text>();

            int collectedEvents = 0;
            hunt.TreasureCollected += (_, _) => collectedEvents++;

            Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -1.5f));
            yield return WaitSeconds(0.5f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden), "1. Hidden");
            Assert.That(hunt.TotalGold, Is.EqualTo(0));

            // 2/3. only DIG: Digging
            digButton.onClick.Invoke();
            yield return null;
            Assert.That(hunt.IsDigging, Is.True, "3. Digging");
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden));
            Assert.That(collectButton.gameObject.activeSelf, Is.False);

            // 4. Revealed - and nothing else happens
            yield return WaitForRevealed(spot);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "4. Revealed");
            Assert.That(hunt.TotalGold, Is.EqualTo(0), "DIG alone must not change gold.");
            Assert.That(goldText.text, Is.EqualTo("0 / 500 GOLD"));
            Assert.That(collectedEvents, Is.EqualTo(0), "DIG alone must not raise TreasureCollected.");
            Assert.That(reward.RewardsShown, Is.EqualTo(0), "DIG alone must not show a reward.");
            Assert.That(collectButton.gameObject.activeSelf, Is.True, "COLLECT becomes available.");
            Assert.That(collectButton.interactable, Is.True);

            // it stays Revealed: no automatic collect, however long we wait
            yield return WaitSeconds(2f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "No automatic collect.");
            Assert.That(hunt.TotalGold, Is.EqualTo(0));
            Assert.That(collectedEvents, Is.EqualTo(0));
            Assert.That(reward.RewardsShown, Is.EqualTo(0));
            Assert.That(collectButton.gameObject.activeSelf, Is.True);

            // 5/6/7. COLLECT: Collected, gold once, reward once
            collectButton.onClick.Invoke();
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), "6. Collected");
            Assert.That(hunt.TotalGold, Is.EqualTo(10), "7. Gold reward");
            Assert.That(collectedEvents, Is.EqualTo(1));
            Assert.That(reward.RewardsShown, Is.EqualTo(1));
            Assert.That(reward.LastMessage, Is.EqualTo("+10 Gold"));

            // repeats are safe
            collectButton.onClick.Invoke();
            collectButton.onClick.Invoke();
            digButton.onClick.Invoke();
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(10));
            Assert.That(collectedEvents, Is.EqualTo(1));
            Assert.That(reward.RewardsShown, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Scene_PlayerMovingInsideDigRange_DigStillCompletes()
        {
            yield return LoadScene();

            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            PlayerDigAnimator animator = Object.FindAnyObjectByType<PlayerDigAnimator>();
            TreasureSpot spot = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;
            Button digButton = FindButton("DigButton");

            int cancelled = 0;
            hunt.DigCancelled += _ => cancelled++;

            Teleport(player, spot.transform.position + new Vector3(0f, 1.1f, -1.5f));
            yield return WaitSeconds(0.5f);
            digButton.onClick.Invoke();
            yield return WaitSeconds(0.15f);
            Assert.That(hunt.IsDigging, Is.True);

            // walk a little, but stay inside the dig radius
            Teleport(player, spot.transform.position + new Vector3(0.8f, 1.1f, -1.0f));
            Assert.That(spot.HorizontalDistanceTo(player.position), Is.LessThan(spot.Definition.DigRadius));
            yield return WaitForRevealed(spot);

            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), "Dig completes while the player stays in range.");
            Assert.That(cancelled, Is.EqualTo(0));
            yield return WaitSeconds(0.2f);
            Assert.That(animator.IsAtRest(), Is.True, "Visual is back to normal after a normal finish.");
        }

        [UnityTest]
        public IEnumerator Scene_PlayerLeavingDigRange_CancelsDigSafely_AndDigCanBeRepeated()
        {
            yield return LoadScene();

            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            DigFeedbackPresenter digPresenter = Object.FindAnyObjectByType<DigFeedbackPresenter>();
            PlayerDigAnimator animator = Object.FindAnyObjectByType<PlayerDigAnimator>();
            RewardFeedbackPresenter reward = Object.FindAnyObjectByType<RewardFeedbackPresenter>();
            TreasureSpot spot = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;
            Button digButton = FindButton("DigButton");
            Button collectButton = FindButton("CollectButton");
            Transform treasureVisual = spot.transform.Find("Visual");
            float digRadius = spot.Definition.DigRadius;

            int started = 0;
            int completed = 0;
            int cancelled = 0;
            int collected = 0;
            hunt.DigStarted += (_, _) => started++;
            hunt.DigCompleted += _ => completed++;
            hunt.DigCancelled += _ => cancelled++;
            hunt.TreasureCollected += (_, _) => collected++;

            Vector3 inRange = spot.transform.position + new Vector3(0f, 1.1f, -1.5f);
            Vector3 outOfRange = spot.transform.position + new Vector3(0f, 1.1f, -(digRadius + 1.5f));

            Teleport(player, inRange);
            yield return WaitSeconds(0.5f);
            digButton.onClick.Invoke();
            yield return WaitSeconds(0.12f);
            Assert.That(hunt.IsDigging, Is.True);
            Assert.That(animator.IsPlaying, Is.True);
            Assert.That(animator.IsAtRest(), Is.False, "Visual is mid-motion before the player leaves.");

            // --- walk out of the dig range during the dig
            Teleport(player, outOfRange);
            yield return null;
            yield return null;

            Assert.That(hunt.IsDigging, Is.False, "Leaving the range cancels the dig.");
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(0));
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden), "The treasure stays Hidden.");
            Assert.That(treasureVisual.gameObject.activeSelf, Is.False);
            Assert.That(animator.IsPlaying, Is.False);
            Assert.That(animator.IsAtRest(), Is.True, "Animation returns to the start pose.");
            Assert.That(animator.CancelCount, Is.EqualTo(1));
            Assert.That(digPresenter.CancelledDigs, Is.EqualTo(1));
            Assert.That(digPresenter.RevealPuffs, Is.EqualTo(0), "No reveal puff for a cancelled dig.");
            Assert.That(digButton.gameObject.activeSelf, Is.False, "Out of range: DIG hidden.");
            Assert.That(collectButton.gameObject.activeSelf, Is.False, "No COLLECT after a cancelled dig.");
            Assert.That(hunt.TotalGold, Is.EqualTo(0));
            Assert.That(reward.RewardsShown, Is.EqualTo(0));

            // no late reveal after the original dig time has passed
            yield return WaitSeconds(hunt.DigDuration + 0.5f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden));
            Assert.That(completed, Is.EqualTo(0));
            Assert.That(collected, Is.EqualTo(0));
            Assert.That(hunt.TotalGold, Is.EqualTo(0));

            // --- back in range: DIG is usable again and the dig completes normally
            Teleport(player, inRange);
            yield return null;
            yield return null;
            Assert.That(digButton.gameObject.activeSelf, Is.True, "DIG is available again in range.");
            Assert.That(digButton.interactable, Is.True);

            digButton.onClick.Invoke();
            digButton.onClick.Invoke(); // repeated press during the new dig is ignored
            yield return null;
            Assert.That(started, Is.EqualTo(2), "Exactly one new dig started.");
            yield return WaitForRevealed(spot);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(digPresenter.DigsPresented, Is.EqualTo(2));
            Assert.That(animator.PlayCount, Is.EqualTo(2));
            Assert.That(hunt.TotalGold, Is.EqualTo(0), "Still no gold before COLLECT.");
            yield return WaitSeconds(0.2f);
            Assert.That(animator.IsAtRest(), Is.True, "Visual is back to normal after the finished dig as well.");

            // --- COLLECT still gives gold exactly once and the rarity feedback once
            collectButton.onClick.Invoke();
            collectButton.onClick.Invoke();
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(10));
            Assert.That(collected, Is.EqualTo(1));
            Assert.That(reward.RewardsShown, Is.EqualTo(1));
            Assert.That(reward.LastMessage, Is.EqualTo("+10 Gold"));
        }

        [UnityTest]
        public IEnumerator Scene_RareTreasure_DigThenCollect_KeepsRarityFeedback()
        {
            yield return LoadScene();

            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            RewardFeedbackPresenter reward = Object.FindAnyObjectByType<RewardFeedbackPresenter>();
            CollectBurstEffect burst = Object.FindAnyObjectByType<CollectBurstEffect>();
            Transform player = GameObject.Find("Player").transform;
            Button digButton = FindButton("DigButton");
            Button collectButton = FindButton("CollectButton");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Treasure/Treasure_GoldBar.prefab");
            spawnedRoot = Object.Instantiate(prefab, new Vector3(-30f, 0f, 40f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(spawnedRoot, loadedScene);
            TreasureSpot bar = spawnedRoot.GetComponent<TreasureSpot>();
            yield return null;

            Teleport(player, bar.transform.position + new Vector3(0f, 1.1f, -1.5f));
            yield return null;
            yield return null;
            digButton.onClick.Invoke();
            yield return WaitForRevealed(bar);
            Assert.That(bar.State, Is.EqualTo(TreasureState.Revealed));

            yield return WaitSeconds(0.1f);
            collectButton.onClick.Invoke();
            yield return null;
            Assert.That(bar.State, Is.EqualTo(TreasureState.Collected));
            Assert.That(hunt.TotalGold, Is.EqualTo(50));
            Assert.That(reward.LastMessage, Is.EqualTo("RARE! +50 Gold"));
            Assert.That(burst.LastEmitCount, Is.EqualTo(burst.ParticleCountFor(bar.Definition.FeedbackProfile.EffectIntensity)));
            Assert.That(burst.GlowActive, Is.True, "Rare keeps its glow.");
        }

        private static void AssertEarthTone(Color color)
        {
            Assert.That(color.r, Is.GreaterThan(color.g), "Earth tones: red > green");
            Assert.That(color.g, Is.GreaterThan(color.b), "Earth tones: green > blue");
            Assert.That(color.r, Is.LessThan(0.85f), "Earth tones are not bright.");
        }

        private IEnumerator LoadScene()
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                GameplayScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null);
            yield return load;
            loadedScene = SceneManager.GetSceneByPath(GameplayScenePath);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
        }

        private Button FindButton(string name)
        {
            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button.name == name && button.gameObject.scene == loadedScene)
                {
                    return button;
                }
            }

            Assert.Fail("Button not found: " + name);
            return null;
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        private static IEnumerator WaitForRevealed(TreasureSpot spot, float timeoutSeconds = 3f)
        {
            float end = Time.time + timeoutSeconds;
            while (spot.State == TreasureState.Hidden && Time.time < end)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }
    }
}
#endif
