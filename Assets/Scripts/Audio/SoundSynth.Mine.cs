using UnityEngine;

namespace ItemCollector3D.Audio
{
    /// <summary>
    /// The Ancient Mine's ambience loops (stage 16D), synthesised like the Green Mine and Rocky Canyon ones (mono, 22.05 kHz, seamless, peak-normalised, deterministic, built from the same
    /// helpers): ONE shared cave bed, one quiet "detail" loop per area and the grotto's water. The details hold only sparse soft events (a far drip, a stone tick, a crystal chime, a metal knock,
    /// a deep resonance) and are mixed far below the detector beep and the DIG / COLLECT sounds (see MineAmbience). Steady tones have a whole number of cycles per loop, so the loops repeat
    /// without a step. Everything stays well away from the beep's 660 / 1320 Hz band, and nothing is sub-bass. Whether they sound good on a device is NOT verified by code, only the
    /// measurable properties (length, peak, seam, band energy).
    /// </summary>
    public static partial class SoundSynth
    {
        public const float MineBedSeconds = 12f;
        public const float GateSeconds = 12f;
        public const float HallSeconds = 14f;
        public const float CavernsSeconds = 14f;
        public const float WorkshopSeconds = 14f;
        public const float VaultSeconds = 16f;
        public const float GrottoSeconds = 10f;

        private static float WholeCycles(float hz, float seconds) => Mathf.Round(hz * seconds) / seconds;

        /// <summary>Adds a decaying echo train of one event: <paramref name="play"/> is called once per echo with its delay and gain.</summary>
        private static void Echoes(float start, float[] delays, float[] gains, System.Action<float, float> play)
        {
            for (int e = 0; e < delays.Length; e++)
            {
                play(start + delays[e], gains[e]);
            }
        }

        /// <summary>A soft swelling tone pair (a pad note): raised-cosine fade in and out over <paramref name="seconds"/>, a fifth-ish partner at <paramref name="ratio"/>.</summary>
        private static void PadNote(float[] s, int rate, float start, float seconds, float frequency, float ratio, float gain)
        {
            int first = Mathf.RoundToInt(start * rate);
            int length = Mathf.RoundToInt(seconds * rate);
            for (int i = 0; i < length && first + i < s.Length; i++)
            {
                float t = (float)i / rate;
                float u = (float)i / length;
                float env = Mathf.Sin(Mathf.PI * u);
                env *= env;
                float tone = Mathf.Sin(Tau * frequency * t) + 0.6f * Mathf.Sin(Tau * frequency * ratio * t + 0.7f) + 0.2f * Mathf.Sin(Tau * frequency * 2f * t + 1.4f);
                s[first + i] += tone * env * gain;
            }
        }

        // ------------------------------------------------------------------ shared bed: air moving through a big cave

        /// <summary>The cave bed: a dull, slowly breathing air movement (noise through two low-passes whose cut-off drifts, a little rumble taken out) with a very faint low hum. No sub-bass.</summary>
        public static AudioClip MineBed()
        {
            const int rate = AmbienceRate;
            int n = Samples(MineBedSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(1616);
            float hum1 = WholeCycles(98f, MineBedSeconds), hum2 = WholeCycles(147f, MineBedSeconds);
            float[] s = Bed(n, fade, raw =>
            {
                float stage1 = 0f, stage2 = 0f, rumble = 0f;
                float rumbleCoeff = OnePole(95f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    // whole cycles per loop (12 s): 1/12, 3/12 and 5/12 Hz
                    float breath = 0.62f + 0.22f * Mathf.Sin(Tau * (1f / 12f) * t + 0.9f) + 0.12f * Mathf.Sin(Tau * (3f / 12f) * t + 2.1f) + 0.04f * Mathf.Sin(Tau * (5f / 12f) * t + 4f);
                    float c = OnePole(300f + 260f * (0.5f + 0.5f * Mathf.Sin(Tau * (2f / 12f) * t + 1.3f)), rate);
                    float noise = Noise(random);
                    stage1 += c * (noise - stage1);
                    stage2 += c * (stage1 - stage2);
                    rumble += rumbleCoeff * (stage2 - rumble);
                    float air = (stage2 - rumble) * 4.4f * breath;
                    float hum = (Mathf.Sin(Tau * hum1 * t) * 0.6f + Mathf.Sin(Tau * hum2 * t + 1f) * 0.4f) * 0.035f * (0.5f + 0.5f * breath);
                    raw[i] = air + hum;
                }
            });

            return Build("MineBed", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ A  Ancient Gate: far echoes, a rare crystal ring

        public static AudioClip GateDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(GateSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(4001);
            float[] s = Bed(n, fade, raw =>
            {
                // a faint draught: a thin, slow, whistling air from the cave mouth behind the gate
                var whistle = new Resonator();
                float lp = 0f;
                float c = OnePole(700f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    float noise = Noise(random);
                    lp += c * (noise - lp);
                    float gust = Mathf.Max(0f, 0.5f + 0.5f * Mathf.Sin(Tau * (2f / GateSeconds) * t + 0.4f));
                    float hz = 880f + 120f * Mathf.Sin(Tau * (1f / GateSeconds) * t);
                    raw[i] = lp * 1.4f * (0.4f + 0.6f * gust) + whistle.Step(noise, hz, 0.996f, rate) * 1.1f * gust * gust;
                }
            });

            float[] delay = { 0f, 0.46f, 1.0f, 1.65f, 2.4f };
            float[] gain = { 1f, 0.5f, 0.27f, 0.13f, 0.06f };
            Echoes(2.6f, delay, gain, (at, g) => Drip(s, rate, at, 1050f, 0.28f * g)); // a far drip, long dull echoes
            Echoes(8.4f, delay, gain, (at, g) => Drip(s, rate, at, 780f, 0.24f * g));
            Echoes(5.6f, new[] { 0f, 0.7f, 1.5f }, new[] { 1f, 0.4f, 0.16f }, (at, g) => Bell(s, rate, at, 1568f, 0.10f * g, 4.5f)); // a rare crystal tone from the entrance crystals
            Echoes(10.3f, new[] { 0f, 0.55f }, new[] { 1f, 0.35f }, (at, g) => Tick(s, rate, at, 1150f, 0.2f * g));
            return Build("GateDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ B  Hall of Pillars: a big stone room

        public static AudioClip HallDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(HallSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(4002);
            float h1 = WholeCycles(110f, HallSeconds), h2 = WholeCycles(165f, HallSeconds), h3 = WholeCycles(220f, HallSeconds);
            float[] s = Bed(n, fade, raw =>
            {
                // the ancient room tone: a very low, wide hum of a few harmonically related partials, breathing slowly, over a dull noise floor
                float lp1 = 0f, lp2 = 0f;
                float c = OnePole(260f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    lp1 += c * (Noise(random) - lp1);
                    lp2 += c * (lp1 - lp2);
                    float breathe = 0.6f + 0.4f * Mathf.Sin(Tau * (2f / HallSeconds) * t + 0.3f);
                    float hum = Mathf.Sin(Tau * h1 * t) * 0.5f + Mathf.Sin(Tau * h2 * t + 0.8f) * 0.32f + Mathf.Sin(Tau * h3 * t + 2.1f) * 0.18f;
                    raw[i] = hum * 0.16f * breathe + lp2 * 3.2f;
                }
            });

            // stone ticks and shifting grit, each carried by the big hall's slow echoes
            float[] delay = { 0f, 0.42f, 0.9f, 1.5f, 2.2f };
            float[] gain = { 1f, 0.55f, 0.32f, 0.17f, 0.08f };
            Echoes(3.4f, delay, gain, (at, g) => Tick(s, rate, at, 1320f * 0.92f, 0.34f * g));
            Echoes(8.8f, delay, gain, (at, g) => Tick(s, rate, at, 880f, 0.3f * g));
            Echoes(11.6f, new[] { 0f, 0.5f, 1.1f }, new[] { 1f, 0.4f, 0.18f }, (at, g) => Knock(s, rate, at, 310f, 0.2f * g));
            return Build("HallDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ C  Crystal Caverns: a soft magical shimmer and rare chimes

        public static AudioClip CavernsDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(CavernsSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(4003);
            float[] f = { WholeCycles(415f, CavernsSeconds), WholeCycles(554f, CavernsSeconds), WholeCycles(831f, CavernsSeconds), WholeCycles(1109f, CavernsSeconds) };
            float[] g = { 0.06f, 0.05f, 0.035f, 0.02f };
            float[] period = { CavernsSeconds / 2f, CavernsSeconds / 3f, CavernsSeconds / 4f, CavernsSeconds / 5f };
            float[] phase = { 0.3f, 2.2f, 4.1f, 1.1f };
            float[] s = Bed(n, fade, raw =>
            {
                // the crystal hum: four soft tones, each swelling on its own slow cycle (they never swell together), plus a trace of air
                float lp = 0f;
                float c = OnePole(900f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    float tone = 0f;
                    for (int k = 0; k < f.Length; k++)
                    {
                        float swell = 0.5f + 0.5f * Mathf.Sin(Tau * t / period[k] + phase[k]);
                        tone += Mathf.Sin(Tau * f[k] * t + phase[k]) * g[k] * swell * swell;
                    }

                    lp += c * (Noise(random) - lp);
                    raw[i] = tone + lp * 0.5f;
                }
            });

            // rare crystal chimes (3 in 14 s), each with a glassy echo
            Echoes(1.9f, new[] { 0f, 0.38f, 0.86f }, new[] { 1f, 0.42f, 0.16f }, (at, gn) => Bell(s, rate, at, 1760f, 0.16f * gn, 3.2f));
            Echoes(6.5f, new[] { 0f, 0.45f, 1.0f }, new[] { 1f, 0.4f, 0.15f }, (at, gn) => Bell(s, rate, at, 2349f, 0.12f * gn, 3.6f));
            Echoes(10.4f, new[] { 0f, 0.5f }, new[] { 1f, 0.4f }, (at, gn) => Bell(s, rate, at, 1397f, 0.14f * gn, 3.0f));
            return Build("CavernsDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ D  Forgotten Workshop: old stone, metal and timber far off

        public static AudioClip WorkshopDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(WorkshopSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(4004);
            float[] s = Bed(n, fade, raw =>
            {
                // a dull, warm room tone: low-passed noise with a faint steady hum of old machinery
                float lp1 = 0f, lp2 = 0f;
                float c = OnePole(340f, rate);
                float hum = WholeCycles(123f, WorkshopSeconds);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    lp1 += c * (Noise(random) - lp1);
                    lp2 += c * (lp1 - lp2);
                    raw[i] = lp2 * 2.6f * (0.7f + 0.3f * Mathf.Sin(Tau * (1f / WorkshopSeconds) * t + 1.6f)) + Mathf.Sin(Tau * hum * t) * 0.03f;
                }
            });

            Knock(s, rate, 1.6f, 520f, 0.3f);
            Knock(s, rate, 1.86f, 390f, 0.18f);
            Tick(s, rate, 4.1f, 1750f, 0.22f);
            Tick(s, rate, 4.24f, 2100f, 0.12f);
            Creak(s, rate, 5.0f, 1.4f, 150f, 215f, 0.34f, 21); // far off mechanical complaint
            MetalRing(s, rate, 8.2f, 410f, 0.16f, 2.6f);
            Creak(s, rate, 9.6f, 1.0f, 230f, 165f, 0.28f, 22);
            Knock(s, rate, 11.3f, 360f, 0.26f);
            Tick(s, rate, 11.55f, 1500f, 0.16f);
            return Build("WorkshopDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ E  Royal Vault: deep and vast

        public static AudioClip VaultDetail()
        {
            const int rate = AmbienceRate;
            int n = Samples(VaultSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(4005);
            float d1 = WholeCycles(62f, VaultSeconds), d2 = WholeCycles(93f, VaultSeconds), d3 = WholeCycles(124f, VaultSeconds);
            float[] s = Bed(n, fade, raw =>
            {
                // the deep hum: three low partials with a slow, uneven swell and a dark breath of air, nothing below ~55 Hz
                float lp1 = 0f, lp2 = 0f;
                float c = OnePole(220f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    lp1 += c * (Noise(random) - lp1);
                    lp2 += c * (lp1 - lp2);
                    float breathe = 0.65f + 0.35f * Mathf.Sin(Tau * (2f / VaultSeconds) * t + 0.7f);
                    float hum = Mathf.Sin(Tau * d1 * t) * 0.55f + Mathf.Sin(Tau * d2 * t + 0.5f) * 0.35f + Mathf.Sin(Tau * d3 * t + 1.7f) * 0.2f * Mathf.Sin(Tau * (1f / VaultSeconds) * t + 2f);
                    raw[i] = hum * 0.2f * breathe + lp2 * 3.4f * (0.5f + 0.5f * breathe);
                }
            });

            // the vault's resonance: two slow, soft chords swelling out of the dark, and one far crystal ring
            PadNote(s, rate, 2.2f, 4.6f, 196f, 1.5f, 0.1f);
            PadNote(s, rate, 8.6f, 5.2f, 261.6f, 1.5f, 0.085f);
            Echoes(13.1f, new[] { 0f, 0.8f, 1.7f }, new[] { 1f, 0.4f, 0.16f }, (at, g) => Bell(s, rate, at, 2093f, 0.09f * g, 3.0f));
            return Build("VaultDetail", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ grotto: trickling water

        public static AudioClip GrottoWater()
        {
            const int rate = AmbienceRate;
            int n = Samples(GrottoSeconds, rate);
            int fade = Samples(0.8f, rate);
            var random = new System.Random(4006);
            float[] s = Bed(n, fade, raw =>
            {
                // a thin trickle: noise band-passed 1.2 .. 3.6 kHz under an irregular, quick-changing level
                float lo = 0f, hi = 0f, flutter = 0f;
                float loCoeff = OnePole(3600f, rate), hiCoeff = OnePole(1200f, rate), flutterCoeff = OnePole(14f, rate);
                for (int i = 0; i < raw.Length; i++)
                {
                    float t = (float)i / rate;
                    float noise = Noise(random);
                    lo += loCoeff * (noise - lo);
                    hi += hiCoeff * (lo - hi);
                    flutter += flutterCoeff * (Mathf.Abs(Noise(random)) - flutter);
                    float swell = 0.55f + 0.45f * Mathf.Sin(Tau * (2f / GrottoSeconds) * t + 0.6f);
                    raw[i] = (lo - hi) * 2.2f * swell * (0.25f + 2.2f * flutter);
                }
            });

            float[] at = { 0.9f, 2.7f, 3.4f, 5.2f, 6.6f, 8.1f, 9.0f };
            float[] pitch = { 1700f, 1300f, 2000f, 1500f, 1850f, 1250f, 1600f };
            for (int d = 0; d < at.Length; d++)
            {
                Echoes(at[d], new[] { 0f, 0.3f, 0.7f }, new[] { 1f, 0.35f, 0.12f }, (when, g) => Drip(s, rate, when, pitch[d], 0.3f * g));
            }

            return Build("GrottoWater", s, rate, 0.6f, 0f);
        }
    }
}
