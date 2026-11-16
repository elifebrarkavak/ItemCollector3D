using ItemCollector3D.UI;
using NUnit.Framework;
using UnityEngine;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Regression tests for the safe-area maths. Before the guard, a zero-sized screen (hidden/collapsed Game View)
    /// produced NaN anchors: the whole HUD vanished and TextMeshPro logged "Invalid AABB inAABB".
    /// </summary>
    public sealed class SafeAreaFitterPlayModeTests
    {
        [Test]
        public void ValidScreen_GivesNormalisedAnchors()
        {
            bool ok = SafeAreaFitter.TryComputeAnchors(
                new Rect(50f, 20f, 1900f, 1000f), new Vector2(2000f, 1080f), out Vector2 min, out Vector2 max);
            Assert.That(ok, Is.True);
            Assert.That(min.x, Is.EqualTo(0.025f).Within(1e-5f));
            Assert.That(min.y, Is.EqualTo(20f / 1080f).Within(1e-5f));
            Assert.That(max.x, Is.EqualTo(0.975f).Within(1e-5f));
            Assert.That(max.y, Is.EqualTo(1020f / 1080f).Within(1e-5f));
        }

        [Test]
        public void FullScreenSafeArea_GivesFullStretch()
        {
            Assert.That(SafeAreaFitter.TryComputeAnchors(new Rect(0f, 0f, 1571f, 1044f), new Vector2(1571f, 1044f),
                out Vector2 min, out Vector2 max), Is.True);
            Assert.That(min, Is.EqualTo(Vector2.zero));
            Assert.That(max.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(max.y, Is.EqualTo(1f).Within(1e-5f));
        }

        [TestCase(0f, 1080f)]
        [TestCase(1920f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(-5f, 1080f)]
        [TestCase(float.NaN, 1080f)]
        [TestCase(1920f, float.PositiveInfinity)]
        public void InvalidScreenSize_IsRejected_AndNeverProducesNaN(float width, float height)
        {
            bool ok = SafeAreaFitter.TryComputeAnchors(
                new Rect(0f, 0f, 1920f, 1080f), new Vector2(width, height), out Vector2 min, out Vector2 max);
            Assert.That(ok, Is.False);
            Assert.That(float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y), Is.False,
                "Rejected input must not leak NaN anchors.");
        }

        [Test]
        public void InvalidSafeArea_IsRejected()
        {
            Assert.That(SafeAreaFitter.TryComputeAnchors(new Rect(0f, 0f, 0f, 1080f), new Vector2(1920f, 1080f), out _, out _), Is.False);
            Assert.That(SafeAreaFitter.TryComputeAnchors(new Rect(float.NaN, 0f, 1920f, 1080f), new Vector2(1920f, 1080f), out _, out _), Is.False);
        }
    }
}
