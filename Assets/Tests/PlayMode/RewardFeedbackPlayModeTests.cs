#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
    /// Rarity reward feedback (floating text, particle burst + glow, sound) and detector signal strength.
    /// Everything is checked against the feedback profile DATA, not hard-coded numbers.
    /// Audibility of the sound is NOT tested (only that it is triggered with the profile's pitch).
    /// </summary>
    public sealed class RewardFeedbackPlayModeTests
    {
        private const string GameplayScenePath = "Assets/Scenes/GameplayTest.unity";
        private static readonly string[] Types = { "GoldCoin", "GoldNugget", "GoldBar", "AncientArtifact" };

        private readonly List<GameObject> spawned = new();
        private Scene loadedScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            spawned.Clear();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
        }

        private static TreasureFeedbackProfile LoadProfile(string type)
        {
            var definition = AssetDatabase.LoadAssetAtPath<TreasureDefinition>($"Assets/Data/Treasures/{type}.asset");
            Assert.That(definition, Is.Not.Null, type);
            Assert.That(definition.FeedbackProfile, Is.Not.Null, type);
            return definition.FeedbackProfile;
        }

        [Test]
        public void ProfileData_DrivesEffectScaling_AndGrowsWithRarity()
        {
            var burstObject = new GameObject("Test Burst", typeof(CollectBurstEffect));
            var textObject = new GameObject("Test Text", typeof(FloatingRewardText));
            spawned.Add(burstObject);
            spawned.Add(textObject);
            CollectBurstEffect burst = burstObject.GetComponent<CollectBurstEffect>();
            FloatingRewardText floating = textObject.GetComponent<FloatingRewardText>();

            int previousCount = 0;
            float previousDuration = 0f;
            float previousScale = 0f;
            float previousGlow = -1f;
            float previousPitch = 0f;
            var colors = new HashSet<Color>();

            foreach (string type in Types)
            {
                TreasureFeedbackProfile profile = LoadProfile(type);
                int count = burst.ParticleCountFor(profile.EffectIntensity);
                Assert.That(count, Is.GreaterThan(previousCount), $"{type}: particle count must grow with rarity.");
                Assert.That(count, Is.LessThanOrEqualTo(burst.MaxParticles), $"{type}: stay within the mobile particle cap.");
                Assert.That(floating.DurationFor(profile.EffectIntensity), Is.GreaterThan(previousDuration), type);
                Assert.That(floating.ScaleFor(profile.EffectIntensity), Is.GreaterThan(previousScale), type);
                Assert.That(profile.Glow, Is.GreaterThanOrEqualTo(previousGlow), $"{type}: glow must not shrink with rarity.");
                Assert.That(profile.SoundPitch, Is.GreaterThan(previousPitch), $"{type}: pitch must rise with rarity.");
                Assert.That(colors.Add(profile.AccentColor), Is.True, $"{type}: accent colours must be distinct.");

                previousCount = count;
                previousDuration = floating.DurationFor(profile.EffectIntensity);
                previousScale = floating.ScaleFor(profile.EffectIntensity);
                previousGlow = profile.Glow;
                previousPitch = profile.SoundPitch;
            }

            Assert.That(LoadProfile("GoldCoin").Glow, Is.EqualTo(0f), "Common has no glow.");
            Assert.That(LoadProfile("GoldNugget").Glow, Is.EqualTo(0f), "Uncommon has no glow.");
            Assert.That(LoadProfile("GoldBar").Glow, Is.GreaterThan(0f), "Rare glows.");
            Assert.That(LoadProfile("AncientArtifact").Glow, Is.GreaterThan(LoadProfile("GoldBar").Glow), "Epic glows most.");
        }

        [Test]
        public void SignalStrength_IsWeakFar_AndStrongerWhenClose()
        {
            float far = DetectorSignalIndicator.StrengthFor(0f, 0.35f);
            float mid = DetectorSignalIndicator.StrengthFor(0.5f, 0.35f);
            float near = DetectorSignalIndicator.StrengthFor(1f, 0.35f);
            Assert.That(far, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(mid, Is.GreaterThan(far));
            Assert.That(near, Is.GreaterThan(mid));
            Assert.That(near, Is.EqualTo(1f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Scene_RewardFeedback_MatchesProfiles_ForAllFourRarities_AndNeverRewardsTwice()
        {
            yield return LoadScene();

            RewardFeedbackPresenter presenter = Object.FindAnyObjectByType<RewardFeedbackPresenter>();
            CollectBurstEffect burst = Object.FindAnyObjectByType<CollectBurstEffect>();
            FloatingRewardText floating = Object.FindAnyObjectByType<FloatingRewardText>();
            CollectSoundPlayer sound = Object.FindAnyObjectByType<CollectSoundPlayer>();
            TreasureHuntController hunt = Object.FindAnyObjectByType<TreasureHuntController>();
            TMP_Text goldText = GameObject.Find("GoldText").GetComponent<TMP_Text>();
            Transform player = GameObject.Find("Player").transform;
            Button digButton = FindButton("DigButton");
            Button collectButton = FindButton("CollectButton");
            Assert.That(presenter, Is.Not.Null, "RewardFeedback must be in GameplayTest.");
            Assert.That(burst, Is.Not.Null);
            Assert.That(floating, Is.Not.Null);
            Assert.That(sound, Is.Not.Null);

            // The reward text must never block touch input.
            Assert.That(floating.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(floating.GetComponent<TMP_Text>().raycastTarget, Is.False);

            var rows = new[]
            {
                (spot: Object.FindAnyObjectByType<TreasureSpot>(), message: "+10 Gold", gold: 10, total: 10),
                (spot: Spawn("Treasure_GoldNugget", new Vector3(-30f, 0f, 10f)), message: "UNCOMMON! +25 Gold", gold: 25, total: 35),
                (spot: Spawn("Treasure_GoldBar", new Vector3(-30f, 0f, 40f)), message: "RARE! +50 Gold", gold: 50, total: 85),
                (spot: Spawn("Treasure_AncientArtifact", new Vector3(30f, 0f, 10f)), message: "EPIC! +100 Gold", gold: 100, total: 185)
            };
            yield return null;

            int previousParticles = 0;
            int shown = 0;
            foreach (var row in rows)
            {
                TreasureFeedbackProfile profile = row.spot.Definition.FeedbackProfile;
                Teleport(player, row.spot.transform.position + new Vector3(0f, 1.1f, -1f));
                yield return null;
                yield return null;
                digButton.onClick.Invoke();
                yield return null;
                yield return WaitSeconds(0.1f); // real play never collects twice within the sound repeat guard
                collectButton.onClick.Invoke();
                yield return null;
                shown++;

                Assert.That(presenter.RewardsShown, Is.EqualTo(shown), row.message);
                Assert.That(presenter.LastMessage, Is.EqualTo(row.message));
                Assert.That(presenter.LastGold, Is.EqualTo(row.gold));
                Assert.That(hunt.TotalGold, Is.EqualTo(row.total));
                Assert.That(goldText.text, Is.EqualTo($"{row.total} / 500 GOLD"), "HUD counter must keep updating.");

                // Floating text follows the profile.
                Assert.That(floating.IsVisible, Is.True);
                Assert.That(floating.CurrentMessage, Is.EqualTo(row.message));
                Assert.That(floating.CurrentColor, Is.EqualTo(profile.AccentColor));
                Assert.That(floating.CurrentScale, Is.EqualTo(floating.ScaleFor(profile.EffectIntensity)).Within(1e-4f));
                Assert.That(floating.CurrentDuration, Is.EqualTo(floating.DurationFor(profile.EffectIntensity)).Within(1e-4f));

                // Particle burst + glow follow the profile.
                Assert.That(burst.LastIntensity, Is.EqualTo(profile.EffectIntensity).Within(1e-4f));
                Assert.That(burst.LastEmitCount, Is.EqualTo(burst.ParticleCountFor(profile.EffectIntensity)));
                Assert.That(burst.LastEmitCount, Is.GreaterThan(previousParticles), "Higher rarity must burst more.");
                Assert.That(burst.LastGlow, Is.EqualTo(profile.Glow).Within(1e-4f));
                Assert.That(burst.GlowActive, Is.EqualTo(profile.Glow > 0f), "Only Rare/Epic glow.");
                previousParticles = burst.LastEmitCount;

                // Sound is triggered once with the profile pitch (audibility itself is NOT tested).
                Assert.That(sound.PlayCount, Is.EqualTo(shown));
                Assert.That(sound.LastPitch, Is.EqualTo(profile.SoundPitch).Within(1e-4f));
                Assert.That(sound.IsPlaying, Is.True);

                // Repeat attempts on the same treasure give no second reward.
                int showCount = floating.ShowCount;
                int playCount = burst.PlayCount;
                int soundCount = sound.PlayCount;
                collectButton.onClick.Invoke();
                digButton.onClick.Invoke();
                yield return null;
                Assert.That(presenter.RewardsShown, Is.EqualTo(shown), "No second reward for the same treasure.");
                Assert.That(floating.ShowCount, Is.EqualTo(showCount));
                Assert.That(burst.PlayCount, Is.EqualTo(playCount));
                Assert.That(sound.PlayCount, Is.EqualTo(soundCount));
                Assert.That(hunt.TotalGold, Is.EqualTo(row.total));
            }

            // The text animates out and hides again.
            yield return WaitSeconds(floating.CurrentDuration + 0.3f);
            Assert.That(floating.IsVisible, Is.False);
            Assert.That(floating.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            Assert.That(burst.GlowActive, Is.False, "Glow light must switch off again.");
        }

        [UnityTest]
        public IEnumerator Scene_DetectorSignalStrength_GrowsAsPlayerApproaches_AndDistanceLogicIsIntact()
        {
            yield return LoadScene();

            TreasureDetector detector = Object.FindAnyObjectByType<TreasureDetector>();
            DetectorSignalIndicator indicator = Object.FindAnyObjectByType<DetectorSignalIndicator>();
            TreasureSpot coin = Object.FindAnyObjectByType<TreasureSpot>();
            Transform player = GameObject.Find("Player").transform;

            float[] distances = { 13f, 8f, 1.5f };
            var strengths = new List<float>();
            var intervals = new List<float>();
            foreach (float distance in distances)
            {
                Teleport(player, coin.transform.position + new Vector3(0f, 1.1f, -distance));
                yield return null;
                yield return null;
                Assert.That(detector.HasSignal, Is.True, $"Signal expected at {distance} m.");
                intervals.Add(detector.CurrentInterval);

                int before = indicator.PulseCount;
                float end = Time.time + 3f;
                while (indicator.PulseCount == before && Time.time < end)
                {
                    yield return null;
                }

                Assert.That(indicator.PulseCount, Is.GreaterThan(before), $"No pulse at {distance} m.");
                strengths.Add(indicator.LastPulseStrength);
            }

            Assert.That(strengths[0], Is.LessThan(strengths[1]), "Mid distance must pulse stronger than far.");
            Assert.That(strengths[1], Is.LessThan(strengths[2]), "Very close must pulse strongest.");
            Assert.That(intervals[0], Is.GreaterThan(intervals[1]), "Interval logic must still speed up.");
            Assert.That(intervals[1], Is.GreaterThan(intervals[2]));
            Assert.That(coin.State, Is.EqualTo(TreasureState.Hidden), "Detector must not change treasure state.");
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

        private TreasureSpot Spawn(string prefabName, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Treasure/" + prefabName + ".prefab");
            Assert.That(prefab, Is.Not.Null, prefabName);
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
