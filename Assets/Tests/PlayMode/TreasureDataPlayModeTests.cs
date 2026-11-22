#if UNITY_EDITOR
using System.Collections.Generic;
using ItemCollector3D.Treasure;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Data integrity of the four treasure types from Concept Document v1.0 (2.7):
    /// Gold Coin (Common, 10), Gold Nugget (Uncommon, 25), Gold Bar (Rare, 50), Ancient Artifact (Epic, 100).
    /// </summary>
    public sealed class TreasureDataPlayModeTests
    {
        private const string DataFolder = "Assets/Data/Treasures/";

        private static readonly object[] Expected =
        {
            new object[] { "GoldCoin", "gold_coin", TreasureRarity.Common, 10, "+10 Gold" },
            new object[] { "GoldNugget", "gold_nugget", TreasureRarity.Uncommon, 25, "UNCOMMON! +25 Gold" },
            new object[] { "GoldBar", "gold_bar", TreasureRarity.Rare, 50, "RARE! +50 Gold" },
            new object[] { "AncientArtifact", "ancient_artifact", TreasureRarity.Epic, 100, "EPIC! +100 Gold" }
        };

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            spawned.Clear();
        }

        private static TreasureDefinition Load(string file)
        {
            var definition = AssetDatabase.LoadAssetAtPath<TreasureDefinition>(DataFolder + file + ".asset");
            Assert.That(definition, Is.Not.Null, "Missing definition asset: " + file);
            return definition;
        }

        [TestCaseSource(nameof(Expected))]
        public void Definition_HasConceptDocumentRarityGoldAndMessage(
            string file, string id, TreasureRarity rarity, int gold, string message)
        {
            TreasureDefinition definition = Load(file);
            Assert.That(definition.Id, Is.EqualTo(id));
            Assert.That(definition.Rarity, Is.EqualTo(rarity));
            Assert.That(definition.GoldValue, Is.EqualTo(gold));
            Assert.That(definition.CollectMessage, Is.EqualTo(message));
        }

        [Test]
        public void AllDefinitions_AreValid_HaveUniqueIds_AndOneProfilePerRarity()
        {
            var ids = new HashSet<string>();
            var profileByRarity = new Dictionary<TreasureRarity, TreasureFeedbackProfile>();

            foreach (object[] row in Expected)
            {
                TreasureDefinition definition = Load((string)row[0]);
                Assert.That(definition.Validate(out string error), Is.True, $"{definition.name}: {error}");
                Assert.That(ids.Add(definition.Id), Is.True, "Duplicate id: " + definition.Id);
                Assert.That(definition.DisplayName, Is.Not.Empty);
                Assert.That(definition.DigRadius, Is.LessThanOrEqualTo(definition.DetectionRadius));
                profileByRarity[definition.Rarity] = definition.FeedbackProfile;
            }

            Assert.That(ids.Count, Is.EqualTo(4));
            Assert.That(profileByRarity.Count, Is.EqualTo(4), "Each rarity must be present exactly once.");

            // Feedback intensity must grow with rarity (Concept Document 2.6: rare rewards get stronger feedback).
            float previous = -1f;
            foreach (TreasureRarity rarity in new[] { TreasureRarity.Common, TreasureRarity.Uncommon, TreasureRarity.Rare, TreasureRarity.Epic })
            {
                float intensity = profileByRarity[rarity].EffectIntensity;
                Assert.That(intensity, Is.GreaterThan(previous), $"{rarity} feedback must be stronger than the previous tier.");
                previous = intensity;
            }
        }

        [Test]
        public void Gold_IncreasesWithRarity()
        {
            int previous = 0;
            foreach (object[] row in Expected)
            {
                int gold = Load((string)row[0]).GoldValue;
                Assert.That(gold, Is.GreaterThan(previous));
                previous = gold;
            }
        }

        [Test]
        public void Definition_WithMissingData_FailsValidation()
        {
            var definition = ScriptableObject.CreateInstance<TreasureDefinition>();
            try
            {
                Assert.That(definition.Validate(out string error), Is.False);
                Assert.That(error, Is.Not.Empty); // default asset has no model / feedback profile
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [TestCaseSource(nameof(Expected))]
        public void SpotWithoutOwnVisual_BuildsModelFromDefinition_AndStateMovesForwardOnly(
            string file, string id, TreasureRarity rarity, int gold, string message)
        {
            TreasureDefinition definition = Load(file);
            var go = new GameObject("Test " + file);
            spawned.Add(go);
            TreasureSpot spot = go.AddComponent<TreasureSpot>();
            spot.Configure(definition, null);

            Assert.That(go.transform.childCount, Is.EqualTo(1), "Model must be created from the definition.");
            Transform model = go.transform.GetChild(0);
            Assert.That(model.name, Is.EqualTo(definition.ModelPrefab.name));
            Assert.That(model.GetComponentInChildren<Collider>(), Is.Null, "Placeholder models must not block the camera.");
            Assert.That(model.gameObject.activeSelf, Is.False, "Hidden treasure must be invisible.");

            Assert.That(spot.Collect(), Is.False, "Cannot collect before reveal.");
            Assert.That(spot.Reveal(), Is.True);
            Assert.That(model.gameObject.activeSelf, Is.True);
            Assert.That(spot.Reveal(), Is.False, "Cannot reveal twice.");
            Assert.That(spot.Collect(), Is.True);
            Assert.That(model.gameObject.activeSelf, Is.True, "The treasure plays its short collect animation first (stage 12B) ...");
            Assert.That(spot.VisualAnimator.IsCollecting, Is.True);
            spot.VisualAnimator.FinishNow(); // ... and is hidden when it ends (a frame loop would take about 0.4 s)
            Assert.That(model.gameObject.activeSelf, Is.False);
            Assert.That(spot.Collect(), Is.False, "Cannot collect twice.");
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
        }

        [TestCase("Treasure_GoldNugget", "GoldNugget")]
        [TestCase("Treasure_GoldBar", "GoldBar")]
        [TestCase("Treasure_AncientArtifact", "AncientArtifact")]
        public void SpotPrefab_ReferencesMatchingDefinition_WithHiddenVisual(string prefabName, string definitionFile)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Treasure/" + prefabName + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            TreasureSpot spot = prefab.GetComponent<TreasureSpot>();
            Assert.That(spot, Is.Not.Null);
            Assert.That(spot.Definition, Is.EqualTo(Load(definitionFile)));
            Assert.That(prefab.transform.childCount, Is.EqualTo(1));
            Assert.That(prefab.transform.GetChild(0).gameObject.activeSelf, Is.False);
            Assert.That(prefab.GetComponentInChildren<Collider>(true), Is.Null);
        }

        [Test]
        public void ExistingGoldCoinPrefab_IsPreserved_AndStillUsesGoldCoinDefinition()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Treasure/Treasure_GoldCoin.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<TreasureSpot>().Definition, Is.EqualTo(Load("GoldCoin")));
            Assert.That(prefab.transform.Find("Visual"), Is.Not.Null, "Existing coin visual must be kept.");
        }
    }
}
#endif
