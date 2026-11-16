#if UNITY_EDITOR
using System.Collections;
using ItemCollector3D.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Regression test for the gold progress bar. The bar used to stay fully drawn because the Filled Image had no
    /// sprite, even though fillAmount was correct. This test checks the GEOMETRY Unity actually generates for the
    /// fill (what ends up on screen), not just the fillAmount value.
    /// </summary>
    public sealed class HudProgressBarPlayModeTests
    {
        private GameObject hudRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hudRoot != null)
            {
                Object.Destroy(hudRoot);
            }

            hudRoot = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GoldProgressBar_FillImageHasSprite_AndDrawnWidthMatchesGold()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerUI.prefab");
            Assert.That(prefab, Is.Not.Null);
            hudRoot = Object.Instantiate(prefab);
            yield return null;

            HudController hud = hudRoot.GetComponentInChildren<HudController>(true);
            Image fill = null;
            foreach (Image image in hudRoot.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "GoldProgressFill")
                {
                    fill = image;
                }
            }

            Assert.That(hud, Is.Not.Null);
            Assert.That(fill, Is.Not.Null, "GoldProgressFill must exist in the PlayerUI prefab.");
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.sprite, Is.Not.Null,
                "A Filled Image without a sprite ignores fillAmount and draws the whole bar.");
            Assert.That(hud.MaximumGold, Is.EqualTo(500));

            float fullWidth = fill.rectTransform.rect.width;
            Assert.That(fullWidth, Is.GreaterThan(1f));

            foreach (int gold in new[] { 0, 10, 100, 250, 500 })
            {
                hud.SetGold(gold);
                Canvas.ForceUpdateCanvases();
                yield return null;

                float expected = gold / 500f;
                Assert.That(fill.fillAmount, Is.EqualTo(expected).Within(1e-4f), $"fillAmount at {gold}");

                Mesh mesh = fill.canvasRenderer.GetMesh();
                float drawnWidth = mesh != null && mesh.vertexCount > 0 ? mesh.bounds.size.x : 0f;
                Assert.That(drawnWidth / fullWidth, Is.EqualTo(expected).Within(0.01f),
                    $"Drawn bar width at {gold}/500 must be {expected:P0} of the full bar.");
            }
        }
    }
}
#endif
