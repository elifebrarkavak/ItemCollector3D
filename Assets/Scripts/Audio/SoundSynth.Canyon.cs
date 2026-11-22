using UnityEngine;

namespace ItemCollector3D.Audio
{
    /// <summary>
    /// Rocky Canyon's ambience loops (stage 15D), synthesised like the Green Mine ones (mono, 22.05 kHz, seamless, peak-normalised, deterministic): ONE shared dry-wind bed and one
    /// quiet "detail" loop per area. The details hold only sparse, soft events (a distant bird, a creaking board, a rail ringing, a gust whistling, a hollow echo), so the loop is mostly
    /// calm and nothing in it is sharp or loud: they are mixed far below the detector beep and the dig / collect sounds (see CanyonAmbience), and all of their energy sits away from the
    /// beep's 660 / 1320 Hz. Whether they sound pleasant on a device is NOT verified by code, only the measurable properties (length, peak, seam, band energy) are.
    /// </summary>
    public static partial class SoundSynth
    {
        public const float CanyonWindSeconds = 10f;
        public const float OutpostSeconds = 12f;
        public const float RiverbedSeconds = 10f;
        public const float RailwaySeconds = 14f;
        public const float OverlookSeconds = 10f;
        public const float RavineSeconds = 12f;

        // ------------------------------------------------------------------ shared wind bed

        /// <summary>The canyon wind: a dry, slowly gusting bed (noise through two low-passes whose cut-off drifts), a faint sandy hiss that rises with the gusts and a very soft high whistle. No sub-bass.</summary>
        public static AudioClip CanyonWind()
        {
            const int rate = AmbienceRate;
            int n = Samples(CanyonWindSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(5150);
            float[] s = Bed(n, fade, raw =>
            {
                float stage1 = 0f, stage2 = 0f, rumble = 0f, hissLow = 0f, hissHigh = 0f;
                var whistle = new Resonator();
                float rumbleCoeff = OnePole(110f, rate), hissLowCoeff = OnePole(5200f, rate), hissHighCoeff = OnePole(2300f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    // whole cycles per loop (10 s): 0.1, 0.3 and 0.5 Hz, so the gusts repeat without a step
                    float gust = 0.66f + 0.20f * Mathf.Sin(Tau * 0.1f * t + 0.4f) + 0.14f * Mathf.Sin(Tau * 0.3f * t + 2.3f) + 0.06f * Mathf.Sin(Tau * 0.5f * t + 4.1f);
                    float c = OnePole(400f + 420f * (0.5f + 0.5f * Mathf.Sin(Tau * 0.2f * t + 1.1f)), rate);
                    float noise = Noise(random);
                    stage1 += c * (noise - stage1);
                    stage2 += c * (stage1 - stage2);
                    rumble += rumbleCoeff * (stage2 - rumble);
                    float bed = (stage2 - rumble) * 4.2f * gust;

                    hissLow += hissLowCoeff * (noise - hissLow);
                    hissHigh += hissHighCoeff * (hissLow - hissHigh);
                    float hiss = (hissLow - hissHigh) * 0.55f * gust * gust; // 2.3 .. 5.2 kHz, only in the gusts

                    float whistleHz = 1850f + 330f * Mathf.Sin(Tau * 0.1f * t + 2f);
                    float thin = whistle.Step(noise, whistleHz, 0.996f, rate) * 1.3f * Mathf.Max(0f, gust - 0.66f);
                    raw[i] = bed + hiss + thin;
                }
            });

            return Build("CanyonWind", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ A  Desert Outpost: a few far birds, a creaking board, a loose tin

        public static AudioClip OutpostDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(OutpostSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(2024);
            float[] s = Bed(n, fade, raw =>
            {
                // an almost silent dry bed (the shared wind carries the area): just enough air that the events do not start from digital silence
                float lp1 = 0f, lp2 = 0f;
                float c = OnePole(520f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    lp1 += c * (Noise(random) - lp1);
                    lp2 += c * (lp1 - lp2);
                    raw[i] = lp2 * 1.2f * (0.6f + 0.4f * Mathf.Sin(Tau * 2f / OutpostSeconds * t + 0.5f));
                }
            });

            // far birds: a long rising whistle, answered once by a softer one (low and slow, unlike Green Mine's quick chirps)
            Chirp(s, rate, 1.9f, 0.34f, 1750f, 2450f, 0.34f);
            Chirp(s, rate, 2.45f, 0.22f, 2150f, 2650f, 0.22f);
            Chirp(s, rate, 7.1f, 0.40f, 2300f, 1650f, 0.28f);
            Chirp(s, rate, 7.7f, 0.18f, 1900f, 2400f, 0.16f);
            // the abandoned settlement: a sign board creaking on its hook, a loose sheet of tin knocking twice
            Creak(s, rate, 4.4f, 1.0f, 210f, 265f, 0.5f, 11);
            Creak(s, rate, 9.3f, 0.7f, 250f, 195f, 0.4f, 12);
            Knock(s, rate, 5.9f, 420f, 0.34f);
            Knock(s, rate, 6.2f, 392f, 0.2f);
            return Build("OutpostDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ B  Dry Riverbed: dry scrub and sand

        public static AudioClip RiverbedDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(RiverbedSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(8008);
            float[] s = Bed(n, fade, raw =>
            {
                // blown sand: a thin high hiss that swells and fades (5 s and 10 s periods)
                float lo = 0f, hi = 0f;
                float cLo = OnePole(6000f, rate), cHi = OnePole(2600f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    float noise = Noise(random);
                    lo += cLo * (noise - lo);
                    hi += cHi * (lo - hi);
                    float swell = 0.5f + 0.5f * Mathf.Sin(Tau * 0.2f * t + 0.3f) * Mathf.Sin(Tau * 0.1f * t + 1.2f);
                    raw[i] = (lo - hi) * 0.55f * swell * swell;
                }
            });

            // dry brush rustling in the gusts (band-passed noise with a crackling envelope), and a few twigs / pebbles ticking
            Rustle(s, rate, 1.3f, 1.5f, 900f, 3600f, 1.0f, random);
            Rustle(s, rate, 4.3f, 1.1f, 1000f, 3300f, 0.8f, random);
            Rustle(s, rate, 5.9f, 1.7f, 800f, 3800f, 1.1f, random);
            Rustle(s, rate, 8.2f, 1.0f, 1100f, 3400f, 0.7f, random);
            Tick(s, rate, 3.2f, 1500f, 0.22f);
            Tick(s, rate, 3.34f, 1900f, 0.14f);
            Tick(s, rate, 7.4f, 1300f, 0.2f);
            return Build("RiverbedDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ C  Old Railway: timber and iron

        public static AudioClip RailwayDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(RailwaySeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(1887);
            float[] s = Bed(n, fade, raw =>
            {
                // the wind humming through old rails and ironwork: a very soft, narrow, slowly gusting tone cluster
                var hum1 = new Resonator();
                var hum2 = new Resonator();
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    float noise = Noise(random);
                    float gust = Mathf.Max(0f, 0.55f + 0.45f * Mathf.Sin(Tau * 2f / RailwaySeconds * t + 1.9f));
                    raw[i] = (hum1.Step(noise, 236f, 0.9975f, rate) + 0.7f * hum2.Step(noise, 318f, 0.9975f, rate)) * 2.4f * gust * gust;
                }
            });

            // the bridge: timber groaning under its own weight (low, slow), a rail ringing now and then (metal, with a slow shimmer)
            Creak(s, rate, 2.2f, 1.2f, 150f, 205f, 0.6f, 21);
            Creak(s, rate, 7.6f, 0.9f, 235f, 180f, 0.5f, 22);
            Creak(s, rate, 9.9f, 1.4f, 165f, 225f, 0.55f, 23);
            MetalRing(s, rate, 5.0f, 312f, 0.5f, 2.6f);
            MetalRing(s, rate, 11.9f, 266f, 0.38f, 3.0f);
            return Build("RailwayDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ D  Canyon Overlook: wind in the open

        public static AudioClip OverlookDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(OverlookSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(4242);
            float[] s = Bed(n, fade, raw =>
            {
                // big, soft gusts: a low whoosh and a thin whistle that sweeps up and down with them (two gusts per loop)
                float lp1 = 0f, lp2 = 0f;
                var whistle = new Resonator();
                var whistle2 = new Resonator();
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    float noise = Noise(random);
                    float gust = 0.5f + 0.5f * Mathf.Sin(Tau * 0.2f * t - 0.8f);
                    gust *= gust;
                    float c = OnePole(260f + 240f * gust, rate);
                    lp1 += c * (noise - lp1);
                    lp2 += c * (lp1 - lp2);
                    float whoosh = lp2 * 4.2f * gust;
                    float w1 = whistle.Step(noise, 1750f + 520f * gust, 0.9965f, rate);
                    float w2 = whistle2.Step(noise, 2650f + 380f * gust, 0.997f, rate);
                    raw[i] = whoosh + (w1 + 0.6f * w2) * 1.5f * gust * gust;
                }
            });

            // a bird of prey, very far away: one long falling cry
            Chirp(s, rate, 7.35f, 0.75f, 2900f, 2000f, 0.2f);
            Chirp(s, rate, 7.35f, 0.75f, 5800f, 4000f, 0.03f);
            return Build("OverlookDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ E  Hidden Ravine: muffled, hollow, a little mysterious

        public static AudioClip RavineDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(RavineSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(777);
            // whole cycles per loop for every steady tone so the loop repeats without a step
            float d1 = Mathf.Round(62f * RavineSeconds) / RavineSeconds;
            float d2 = Mathf.Round(93f * RavineSeconds) / RavineSeconds;
            float pad = Mathf.Round(349f * RavineSeconds) / RavineSeconds;
            float pad2 = Mathf.Round(523f * RavineSeconds) / RavineSeconds;
            float[] s = Bed(n, fade, raw =>
            {
                float lp1 = 0f, lp2 = 0f;
                float c = OnePole(300f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    float breathe = 0.65f + 0.35f * Mathf.Sin(Tau * 2f / RavineSeconds * t + 0.7f);
                    float drone = (Mathf.Sin(Tau * d1 * t) * 0.55f + Mathf.Sin(Tau * d2 * t) * 0.40f) * breathe;
                    lp1 += c * (Noise(random) - lp1);
                    lp2 += c * (lp1 - lp2);
                    float breath = lp2 * 5.5f * (0.4f + 0.6f * Mathf.Sin(Tau * 1f / RavineSeconds * t + 2.4f) * Mathf.Sin(Tau * 1f / RavineSeconds * t + 2.4f));
                    // a faint, slowly swelling, slightly wavering tone far inside the rock: the "mysterious" part, kept very soft and well below 660 Hz
                    float swell = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Tau * 3f / RavineSeconds * t - 1.2f), 3f);
                    float wobble = 1f + 0.004f * Mathf.Sin(Tau * 5f / RavineSeconds * t);
                    float tone = (Mathf.Sin(Tau * pad * wobble * t) + 0.35f * Mathf.Sin(Tau * pad2 * t + 0.6f)) * 0.12f * swell;
                    raw[i] = drone * 0.3f + breath * 0.7f + tone;
                }
            });

            // stone ticks and drips far down the passage, each followed by long, dull echoes
            float[] at = { 2.4f, 6.1f, 9.4f };
            float[] pitch = { 980f, 1220f, 860f };
            float[] delay = { 0f, 0.31f, 0.64f, 1.0f, 1.42f };
            float[] gain = { 1f, 0.5f, 0.26f, 0.13f, 0.06f };
            for (int d = 0; d < at.Length; d++)
            {
                for (int e = 0; e < delay.Length; e++)
                {
                    Drip(s, rate, at[d] + delay[e], pitch[d] * (1f - 0.05f * e), 0.3f * gain[e]);
                }

                float wash = 0f;
                float washCoeff = OnePole(500f, rate);
                int start = Mathf.RoundToInt(at[d] * rate);
                int length = Mathf.RoundToInt(1.6f * rate);
                for (int i = 0; i < length && start + i < n; i++)
                {
                    float t = (float)i / rate;
                    wash += washCoeff * (Noise(random) - wash);
                    s[start + i] += wash * Attack(t, 0.02f) * Mathf.Exp(-t * 2.6f) * 0.12f;
                }
            }

            return Build("RavineDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ building blocks (canyon)

        /// <summary>Fills n + fade samples with <paramref name="fill"/> and cross-fades the extra tail into the head, so the loop is seamless.</summary>
        private static float[] Bed(int n, int fade, System.Action<float[]> fill)
        {
            var raw = new float[n + fade];
            fill(raw);
            var s = new float[n];
            System.Array.Copy(raw, s, n);
            CrossfadeSeam(s, raw, fade);
            return s;
        }

        /// <summary>Two-pole resonator (a very narrow band-pass for noise): the frequency may change from sample to sample. Peak gain is about 1 / (2 sin theta).</summary>
        private struct Resonator
        {
            private float y1, y2;

            public float Step(float x, float frequency, float r, int rate)
            {
                float y = (1f - r) * x + 2f * r * Mathf.Cos(Tau * frequency / rate) * y1 - r * r * y2;
                y2 = y1;
                y1 = y;
                return y;
            }
        }

        /// <summary>
        /// A timber creak: a low, slightly wavering saw wave that sweeps from <paramref name="from"/> to <paramref name="to"/> Hz, chopped by a stick-and-slip flutter, dulled by a low-pass
        /// (nothing above ~1.1 kHz) and wrapped in a soft envelope.
        /// </summary>
        private static void Creak(float[] s, int rate, float start, float seconds, float from, float to, float gain, int seed)
        {
            int first = Mathf.RoundToInt(start * rate);
            int length = Mathf.RoundToInt(seconds * rate);
            var random = new System.Random(seed);
            float phase = 0f, lp = 0f, grit = 0f;
            float coeff = OnePole(1100f, rate), gritCoeff = OnePole(700f, rate);
            for (int i = 0; i < length && first + i < s.Length; i++)
            {
                float u = (float)i / length;
                float t = (float)i / rate;
                float f = Mathf.Lerp(from, to, u) * (1f + 0.05f * Mathf.Sin(Tau * 7.5f * t) + 0.02f * Mathf.Sin(Tau * 17f * t));
                phase += f / rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                grit += gritCoeff * (Noise(random) - grit);
                lp += coeff * (saw * 0.6f + grit * 1.6f - lp);
                float slip = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Tau * (9f + 7f * u) * t));
                float env = Mathf.Sin(Mathf.PI * u);
                s[first + i] += lp * slip * env * env * gain;
            }
        }

        /// <summary>A dull knock of loose metal: one inharmonic partial pair with a quick decay.</summary>
        private static void Knock(float[] s, int rate, float start, float frequency, float gain)
        {
            int first = Mathf.RoundToInt(start * rate);
            for (int i = 0; first + i < s.Length; i++)
            {
                float t = (float)i / rate;
                float env = Attack(t, 0.002f) * Mathf.Exp(-t * 16f);
                if (t > 0.05f && env < 0.002f)
                {
                    break;
                }

                s[first + i] += (Mathf.Sin(Tau * frequency * t) + 0.5f * Mathf.Sin(Tau * frequency * 2.63f * t)) * env * gain;
            }
        }

        /// <summary>A rail ringing after something far away touched it: a few inharmonic partials with a slow beat and a gentle shimmer, long decay, everything under 2 kHz.</summary>
        private static void MetalRing(float[] s, int rate, float start, float frequency, float gain, float decay)
        {
            float[] ratio = { 1f, 2.32f, 4.11f };
            float[] amp = { 1f, 0.45f, 0.18f };
            float[] fall = { decay, decay * 1.5f, decay * 2.4f };
            int first = Mathf.RoundToInt(start * rate);
            for (int i = 0; first + i < s.Length; i++)
            {
                float t = (float)i / rate;
                if (t > 0.1f && Mathf.Exp(-t * decay) < 0.002f)
                {
                    break;
                }

                float tone = 0f;
                for (int p = 0; p < ratio.Length; p++)
                {
                    float f = frequency * ratio[p];
                    tone += (Mathf.Sin(Tau * f * t) + Mathf.Sin(Tau * f * 1.005f * t)) * 0.5f * amp[p] * Mathf.Exp(-t * fall[p]);
                }

                float shimmer = 0.8f + 0.2f * Mathf.Sin(Tau * 6.5f * t);
                s[first + i] += tone * shimmer * Attack(t, 0.004f) * gain;
            }
        }

        /// <summary>A burst of dry rustling: noise band-passed between <paramref name="lowHz"/> and <paramref name="highHz"/> under a crackling (irregular) amplitude and a soft envelope.</summary>
        private static void Rustle(float[] s, int rate, float start, float seconds, float lowHz, float highHz, float gain, System.Random random)
        {
            int first = Mathf.RoundToInt(start * rate);
            int length = Mathf.RoundToInt(seconds * rate);
            float lo = 0f, hi = 0f, crackle = 0f;
            float loCoeff = OnePole(highHz, rate), hiCoeff = OnePole(lowHz, rate), crackleCoeff = OnePole(60f, rate);
            for (int i = 0; i < length && first + i < s.Length; i++)
            {
                float u = (float)i / length;
                float noise = Noise(random);
                lo += loCoeff * (noise - lo);
                hi += hiCoeff * (lo - hi);
                crackle += crackleCoeff * (Mathf.Abs(Noise(random)) - crackle);
                float env = Mathf.Sin(Mathf.PI * u);
                s[first + i] += (lo - hi) * 2.2f * env * env * (0.3f + 2.4f * crackle) * gain * 0.35f;
            }
        }

        /// <summary>A single dry tick (a twig or pebble): a very short, damped resonance.</summary>
        private static void Tick(float[] s, int rate, float start, float frequency, float gain)
        {
            int first = Mathf.RoundToInt(start * rate);
            for (int i = 0; first + i < s.Length && i < rate / 8; i++)
            {
                float t = (float)i / rate;
                s[first + i] += Mathf.Sin(Tau * frequency * t) * Attack(t, 0.0008f) * Mathf.Exp(-t * 90f) * gain;
            }
        }
    }
}
