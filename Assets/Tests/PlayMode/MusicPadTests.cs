#if UNITY_EDITOR
using ItemCollector3D.Menus;
using NUnit.Framework;
using UnityEngine;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// The background music pad (stage 17B sound fix). Measurable properties only: the old pad was four pure sines at 110 / 165 / 220 / 277 Hz (a mains-hum signature); the new one must have no
    /// energy on the 110 / 220 Hz hum lines, stay below the old level, be finite, bounded and seamless. These numbers say nothing about how it SOUNDS: that has to be listened to on a device.
    /// </summary>
    public sealed class MusicPadTests
    {
        private static AudioClip MakePad() =>
            (AudioClip)typeof(AmbientMusicPlayer).GetMethod("CreatePad", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);

        /// <summary>Amplitude of one frequency in the signal (Goertzel), as a share of the signal's own RMS.</summary>
        private static float Component(float[] x, int rate, float frequency)
        {
            double w = 2.0 * System.Math.PI * frequency / rate, c = 2.0 * System.Math.Cos(w), s1 = 0, s2 = 0;
            foreach (float v in x)
            {
                double s0 = v + c * s1 - s2;
                s2 = s1;
                s1 = s0;
            }

            double power = s1 * s1 + s2 * s2 - c * s1 * s2;
            double amplitude = 2.0 * System.Math.Sqrt(System.Math.Max(0.0, power)) / x.Length;
            double sum = 0;
            foreach (float v in x)
            {
                sum += v * v;
            }

            return (float)(amplitude / System.Math.Sqrt(sum / x.Length));
        }

        [Test]
        public void ThePad_HasNoMainsHumLines_IsQuieterThanBefore_AndLoopsWithoutAClick()
        {
            AudioClip clip = MakePad();
            var data = new float[clip.samples];
            Assert.That(clip.GetData(data, 0), Is.True);
            Assert.That(clip.length, Is.EqualTo(8f).Within(0.01f));
            Assert.That(clip.frequency, Is.EqualTo(22050));
            Assert.That(clip.channels, Is.EqualTo(1));

            float peak = 0f, sum = 0f, biggestStep = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                Assert.That(float.IsFinite(data[i]), Is.True);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                sum += data[i] * data[i];
                if (i > 0)
                {
                    biggestStep = Mathf.Max(biggestStep, Mathf.Abs(data[i] - data[i - 1]));
                }
            }

            float rms = Mathf.Sqrt(sum / data.Length);
            Debug.Log($"Music pad: peak {peak:F2}, rms {rms:F3} (the old pad: peak 0.35, rms 0.115)");
            Assert.That(peak, Is.LessThan(0.3f), "below the old peak of 0.35");
            Assert.That(rms, Is.InRange(0.05f, 0.1f), "about 3 dB below the old 0.115, still a real bed");
            Assert.That(Mathf.Abs(data[0] - data[data.Length - 1]), Is.LessThanOrEqualTo(biggestStep), "the loop point is no click");

            // the old pad's lines: 110 Hz (the strongest) and its octave 220 Hz must be gone
            Assert.That(Component(data, 22050, 110f), Is.LessThan(0.02f), "no 110 Hz hum line");
            Assert.That(Component(data, 22050, 220f), Is.LessThan(0.05f), "no 220 Hz hum line");
            Assert.That(Component(data, 22050, 55f), Is.LessThan(0.02f), "no 55 Hz line");
            Assert.That(Mathf.Max(Component(data, 22050, 195.75f), Component(data, 22050, 196.25f)), Is.GreaterThan(0.3f), "the pad itself (the detuned pair around G3) is there");
            // and nothing in the detector beep's own band (660 / 1320 Hz)
            Assert.That(Component(data, 22050, 660f), Is.LessThan(0.01f));
            Assert.That(Component(data, 22050, 1320f), Is.LessThan(0.01f));
            Object.DestroyImmediate(clip);
        }
    }
}
#endif
