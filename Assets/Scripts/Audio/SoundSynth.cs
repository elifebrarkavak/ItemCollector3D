using UnityEngine;

namespace ItemCollector3D.Audio
{
    /// <summary>
    /// The game's own sounds, synthesised from maths at run time: no audio files, so no licences and no download size (a few hundred
    /// kilobytes of PCM in memory in total). Every sound is deterministic (fixed random seeds) and tuned to be short, soft and low-pitched
    /// enough for a phone speaker. One-shots are peak-normalised and fade out to exact silence (no click); loops are made seamless by
    /// cross-fading their own tail into their head. Whether it sounds pleasant on a device is NOT verified by code, only the measurable
    /// properties (length, peak, silence at both ends, loop seam) are, see GameAudioPlayModeTests.
    /// </summary>
    public static class SoundSynth
    {
        public const int SfxRate = 44100;
        public const int AmbienceRate = 22050;

        private const float Tau = 6.2831853f;

        // ------------------------------------------------------------------ detector

        public const float BeepSeconds = 0.085f;

        /// <summary>The detector pulse: a soft sine "blip" with a gentle attack and a quick decay. The player pitches it up as the signal gets stronger.</summary>
        public static AudioClip Beep()
        {
            int n = Samples(BeepSeconds, SfxRate);
            var s = new float[n];
            const float f = 660f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SfxRate;
                float tone = Mathf.Sin(Tau * f * t) + 0.2f * Mathf.Sin(Tau * 2f * f * t);
                s[i] = tone * Attack(t, 0.006f) * Mathf.Exp(-t * 24f);
            }

            return Build("DetectorBeep", s, SfxRate, 0.8f, 0.012f);
        }

        // ------------------------------------------------------------------ dig

        public const float DigStrokeSeconds = 0.24f;

        /// <summary>One shovel stroke into soil: a dull low thump, a short bed of soft earth noise and a few gravel grains. Never sharp.</summary>
        public static AudioClip DigStroke()
        {
            int n = Samples(DigStrokeSeconds, SfxRate);
            var s = new float[n];
            var random = new System.Random(4711);
            float phase = 0f, soilLow = 0f, soilHigh = 0f, grainLow = 0f;
            float lowCoeff = OnePole(1200f, SfxRate), dullCoeff = OnePole(220f, SfxRate), grainCoeff = OnePole(2500f, SfxRate);

            // gravel grains: tiny noise bursts spread over the stroke
            const int grains = 7;
            var grainTime = new float[grains];
            var grainGain = new float[grains];
            for (int g = 0; g < grains; g++)
            {
                grainTime[g] = 0.025f + 0.17f * (float)random.NextDouble();
                grainGain[g] = 0.10f + 0.16f * (float)random.NextDouble();
            }

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SfxRate;
                phase += Tau * (85f + 140f * Mathf.Exp(-t * 30f)) / SfxRate;
                float thump = Mathf.Sin(phase) * Attack(t, 0.003f) * Mathf.Exp(-t * 22f);

                float noise = Noise(random);
                soilLow += lowCoeff * (noise - soilLow);
                soilHigh += dullCoeff * (soilLow - soilHigh);
                float soil = (soilLow - soilHigh) * 2.6f * Attack(t, 0.002f) * Mathf.Exp(-t * 24f); // band 220..1200 Hz

                grainLow += grainCoeff * (noise - grainLow);
                float grainNoise = noise - grainLow; // above ~2.5 kHz, kept very quiet
                float grain = 0f;
                for (int g = 0; g < grains; g++)
                {
                    float dt = t - grainTime[g];
                    if (dt >= 0f && dt < 0.03f)
                    {
                        grain += grainNoise * grainGain[g] * Attack(dt, 0.0015f) * Mathf.Exp(-dt * 260f);
                    }
                }

                s[i] = thump * 0.7f + soil * 0.9f + grain * 0.15f;
            }

            return Build("DigStroke", s, SfxRate, 0.7f, 0.02f);
        }

        // ------------------------------------------------------------------ treasure

        public const float RevealSeconds = 0.6f;

        /// <summary>The treasure shows itself: two soft rising bell notes with a faint shimmer. Short, calm, a little curious.</summary>
        public static AudioClip Reveal()
        {
            int n = Samples(RevealSeconds, SfxRate);
            var s = new float[n];
            Bell(s, SfxRate, 0.000f, 659.25f, 1.00f, 5.5f);  // E5
            Bell(s, SfxRate, 0.115f, 987.77f, 0.85f, 5.0f);  // B5
            Bell(s, SfxRate, 0.115f, 1975.5f, 0.10f, 9.0f);  // shimmer
            return Build("TreasureReveal", s, SfxRate, 0.7f, 0.05f);
        }

        public const float CollectSeconds = 0.7f;

        /// <summary>The reward: a quick two-ping coin chime (metallic partials, short decay).</summary>
        public static AudioClip CollectCoin()
        {
            int n = Samples(CollectSeconds, SfxRate);
            var s = new float[n];
            Coin(s, SfxRate, 0.000f, 783.99f, 1.00f);   // G5
            Coin(s, SfxRate, 0.075f, 1046.5f, 0.95f);   // C6
            return Build("CollectCoin", s, SfxRate, 0.85f, 0.06f);
        }

        public const float CollectEpicSeconds = 1.5f;

        /// <summary>The Epic reward: the coin chime followed by a rising four-note arpeggio, a soft shimmering tail and a few sparkles. About twice as long and richer than the normal one.</summary>
        public static AudioClip CollectEpic()
        {
            int n = Samples(CollectEpicSeconds, SfxRate);
            var s = new float[n];
            var random = new System.Random(2718);
            Coin(s, SfxRate, 0.000f, 783.99f, 0.9f);
            float[] arpeggio = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }; // C5 E5 G5 C6 E6
            for (int k = 0; k < arpeggio.Length; k++)
            {
                Bell(s, SfxRate, 0.09f + 0.085f * k, arpeggio[k], 0.75f, 4.2f);
            }

            // shimmering tail: two soft partials with a slow tremolo
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SfxRate;
                float swell = Attack(t - 0.3f, 0.25f) * Mathf.Exp(-(t - 0.3f) * 2.2f);
                if (t >= 0.3f)
                {
                    float tremolo = 0.75f + 0.25f * Mathf.Sin(Tau * 6f * t);
                    s[i] += (Mathf.Sin(Tau * 1046.5f * t) + 0.6f * Mathf.Sin(Tau * 1567.98f * t)) * 0.18f * swell * tremolo;
                }
            }

            for (int k = 0; k < 6; k++) // sparkles
            {
                float at = 0.45f + 0.7f * (float)random.NextDouble();
                float pitch = 2600f + 1700f * (float)random.NextDouble();
                Bell(s, SfxRate, at, pitch, 0.10f + 0.06f * (float)random.NextDouble(), 22f);
            }

            return Build("CollectEpic", s, SfxRate, 0.85f, 0.12f);
        }

        // ------------------------------------------------------------------ ambience loops

        public const float ForestSeconds = 10f;

        /// <summary>
        /// Green Mine outdoors: a soft wind bed, a faint leaf rustle and a few distant bird calls. Mono, 22 kHz, 10 s, seamless.
        /// The birds stay clear of the loop's start and end, so repeating it never repeats a call back to back.
        /// </summary>
        public static AudioClip ForestAmbience()
        {
            const int rate = AmbienceRate;
            int n = Samples(ForestSeconds, rate);
            int fade = Samples(0.8f, rate);
            int total = n + fade;
            var raw = new float[total];
            var random = new System.Random(9001);

            // wind: noise through two low-pass stages whose cut-off drifts, and a slow gusting gain
            float stage1 = 0f, stage2 = 0f, rumble = 0f, rustleLow = 0f, rustlePhase = 0f;
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / rate;
                float gust = 0.7f + 0.18f * Mathf.Sin(Tau * 0.1f * t + 0.6f) + 0.12f * Mathf.Sin(Tau * 0.3f * t + 2.1f);
                float cutoff = 380f + 260f * (0.5f + 0.5f * Mathf.Sin(Tau * 0.2f * t + 1.3f));
                float c = OnePole(cutoff, rate);
                float noise = Noise(random);
                stage1 += c * (noise - stage1);
                stage2 += c * (stage1 - stage2);
                rumble += OnePole(110f, rate) * (stage2 - rumble); // remove the sub-bass a phone speaker cannot play anyway
                float wind = (stage2 - rumble) * 5.5f * gust;

                // leaf rustle: highpassed noise that swells now and then (slow, irregular amplitude)
                rustleLow += OnePole(1800f, rate) * (noise - rustleLow);
                rustlePhase = 0.5f + 0.5f * Mathf.Sin(Tau * 0.37f * t + 0.9f) * Mathf.Sin(Tau * 0.11f * t + 0.2f);
                float rustle = (noise - rustleLow) * 0.06f * rustlePhase * rustlePhase;

                raw[i] = wind + rustle;
            }

            // loop seam: blend the extra tail into the head
            var s = new float[n];
            System.Array.Copy(raw, s, n);
            CrossfadeSeam(s, raw, fade);

            // bird calls, placed between 1.2 s and n - 0.5 s (outside the seam)
            float[] callAt = { 1.4f, 3.1f, 4.0f, 6.0f, 7.5f, 8.6f };
            float[] basePitch = { 3200f, 2700f, 3500f, 2900f, 3300f, 2600f };
            int[] syllables = { 3, 2, 4, 2, 3, 2 };
            for (int b = 0; b < callAt.Length; b++)
            {
                for (int k = 0; k < syllables[b]; k++)
                {
                    float start = callAt[b] + k * 0.11f;
                    float f0 = basePitch[b] * (1f + 0.05f * k);
                    Chirp(s, rate, start, 0.07f, f0, f0 * (b % 2 == 0 ? 1.28f : 0.8f), 0.2f);
                }
            }

            return Build("ForestAmbience", s, rate, 0.6f, 0f);
        }

        public const float CaveSeconds = 12f;

        /// <summary>
        /// Inside the mine: a deep, slow drone, a faint draught and a few water drips with their own echoes baked in (the "slightly reverberant" feel,
        /// at no run-time DSP cost). Mono, 22 kHz, 12 s, seamless.
        /// </summary>
        public static AudioClip CaveAmbience()
        {
            const int rate = AmbienceRate;
            int n = Samples(CaveSeconds, rate);
            int fade = Samples(0.8f, rate);
            int total = n + fade;
            var raw = new float[total];
            var random = new System.Random(31337);

            // whole cycles per loop for the drone so it repeats without a step
            float f1 = Mathf.Round(55f * CaveSeconds) / CaveSeconds;
            float f2 = Mathf.Round(110f * CaveSeconds) / CaveSeconds;
            float f3 = Mathf.Round(165f * CaveSeconds) / CaveSeconds;
            float f4 = Mathf.Round(220.5f * CaveSeconds) / CaveSeconds;
            float air1 = 0f, air2 = 0f;
            float airCoeff = OnePole(420f, rate);
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / rate;
                float breathe = 0.7f + 0.3f * Mathf.Sin(Tau * 3f / CaveSeconds * t);
                float drone = Mathf.Sin(Tau * f1 * t) * 0.2f + Mathf.Sin(Tau * f2 * t) * 0.5f + Mathf.Sin(Tau * f3 * t) * 0.3f * breathe + Mathf.Sin(Tau * f4 * t) * 0.16f * (1.7f - breathe);
                float noise = Noise(random);
                air1 += airCoeff * (noise - air1);
                air2 += airCoeff * (air1 - air2);
                float draught = air2 * 8f * (0.5f + 0.5f * Mathf.Sin(Tau * 2f / CaveSeconds * t + 1f));
                raw[i] = drone * 0.4f + draught * 0.5f;
            }

            var s = new float[n];
            System.Array.Copy(raw, s, n);
            CrossfadeSeam(s, raw, fade);

            // drips with baked echoes, all finished well before the end of the loop
            float[] dripAt = { 1.3f, 4.0f, 5.7f, 8.4f, 10.0f };
            float[] dripPitch = { 1250f, 1750f, 1450f, 1950f, 1100f };
            float[] echoDelay = { 0f, 0.23f, 0.47f, 0.76f };
            float[] echoGain = { 1f, 0.42f, 0.2f, 0.09f };
            for (int d = 0; d < dripAt.Length; d++)
            {
                for (int e = 0; e < echoDelay.Length; e++)
                {
                    float at = dripAt[d] + echoDelay[e];
                    float pitch = dripPitch[d] * (1f - 0.04f * e); // each echo a little duller
                    Drip(s, rate, at, pitch, 0.34f * echoGain[e]);
                }

                // a diffuse wash after the drip (a dark, quickly fading noise puff)
                float wash = 0f;
                float washCoeff = OnePole(700f, rate);
                int start = Mathf.RoundToInt(dripAt[d] * rate);
                int length = Mathf.RoundToInt(0.9f * rate);
                for (int i = 0; i < length && start + i < n; i++)
                {
                    float t = (float)i / rate;
                    wash += washCoeff * (Noise(random) - wash);
                    s[start + i] += wash * 0.9f * Attack(t, 0.01f) * Mathf.Exp(-t * 4.5f) * 0.1f;
                }
            }

            return Build("CaveAmbience", s, rate, 0.6f, 0f);
        }

        // ------------------------------------------------------------------ building blocks

        private static int Samples(float seconds, int rate) => Mathf.CeilToInt(seconds * rate);

        private static float Noise(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        /// <summary>One-pole low-pass coefficient for a cut-off frequency.</summary>
        private static float OnePole(float cutoff, int rate) => 1f - Mathf.Exp(-Tau * cutoff / rate);

        /// <summary>Raised-cosine fade-in over <paramref name="seconds"/>; 0 before the start (negative time).</summary>
        private static float Attack(float t, float seconds)
        {
            if (t <= 0f)
            {
                return 0f;
            }

            return t >= seconds ? 1f : 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t / seconds);
        }

        /// <summary>A soft bell: sine plus two decaying overtones, added into <paramref name="s"/> from <paramref name="start"/> seconds.</summary>
        private static void Bell(float[] s, int rate, float start, float frequency, float gain, float decay)
        {
            int first = Mathf.RoundToInt(start * rate);
            for (int i = first; i < s.Length; i++)
            {
                float t = (float)(i - first) / rate;
                float env = Attack(t, 0.004f) * Mathf.Exp(-t * decay);
                if (env < 0.0005f && t > 0.05f)
                {
                    break;
                }

                float tone = Mathf.Sin(Tau * frequency * t) + 0.28f * Mathf.Sin(Tau * 2f * frequency * t) * Mathf.Exp(-t * decay)
                             + 0.1f * Mathf.Sin(Tau * 3f * frequency * t) * Mathf.Exp(-t * decay * 1.6f);
                s[i] += tone * env * gain * 0.5f;
            }
        }

        /// <summary>A coin ping: a metallic, slightly inharmonic bell with a very fast attack.</summary>
        private static void Coin(float[] s, int rate, float start, float frequency, float gain)
        {
            float[] ratio = { 1f, 2.41f, 3.97f, 5.93f };
            float[] amp = { 1f, 0.42f, 0.22f, 0.1f };
            float[] decay = { 7.5f, 11f, 16f, 24f };
            int first = Mathf.RoundToInt(start * rate);
            for (int i = first; i < s.Length; i++)
            {
                float t = (float)(i - first) / rate;
                float tone = 0f;
                for (int p = 0; p < ratio.Length; p++)
                {
                    tone += Mathf.Sin(Tau * frequency * ratio[p] * t) * amp[p] * Mathf.Exp(-t * decay[p]);
                }

                if (t > 0.05f && Mathf.Exp(-t * decay[0]) < 0.002f)
                {
                    break;
                }

                s[i] += tone * Attack(t, 0.0015f) * gain * 0.5f;
            }
        }

        /// <summary>A short bird syllable: a frequency sweep with a little vibrato and a smooth window.</summary>
        private static void Chirp(float[] s, int rate, float start, float seconds, float from, float to, float gain)
        {
            int first = Mathf.RoundToInt(start * rate);
            int length = Mathf.RoundToInt(seconds * rate);
            float phase = 0f;
            for (int i = 0; i < length && first + i < s.Length; i++)
            {
                float u = (float)i / length;
                float f = Mathf.Lerp(from, to, u) * (1f + 0.012f * Mathf.Sin(Tau * 38f * u * seconds));
                phase += Tau * f / rate;
                float window = Mathf.Sin(Mathf.PI * u);
                s[first + i] += Mathf.Sin(phase) * window * window * gain;
            }
        }

        /// <summary>One water drip: a short sine ping with a quick downward pitch bend.</summary>
        private static void Drip(float[] s, int rate, float start, float frequency, float gain)
        {
            int first = Mathf.RoundToInt(start * rate);
            int length = Mathf.RoundToInt(0.18f * rate);
            float phase = 0f;
            for (int i = 0; i < length && first + i < s.Length; i++)
            {
                float t = (float)i / rate;
                phase += Tau * frequency * (1f + 0.1f * Mathf.Exp(-t * 55f)) / rate;
                s[first + i] += Mathf.Sin(phase) * Attack(t, 0.002f) * Mathf.Exp(-t * 32f) * gain;
            }
        }

        /// <summary>
        /// Makes a loop seamless: the first <paramref name="fade"/> samples become a blend of the head and the extra tail (raw[n..n+fade)),
        /// so the sample after the last one (the head) continues the tail smoothly. Equal-power weights keep the level steady for noise.
        /// </summary>
        private static void CrossfadeSeam(float[] s, float[] raw, int fade)
        {
            int n = s.Length;
            for (int i = 0; i < fade; i++)
            {
                float u = (float)i / fade;
                float head = Mathf.Sin(0.5f * Mathf.PI * u);
                float tail = Mathf.Cos(0.5f * Mathf.PI * u);
                s[i] = raw[i] * head + raw[n + i] * tail;
            }
        }

        /// <summary>Peak-normalises, optionally fades the end to exact silence (smoothstep), and creates the mono clip.</summary>
        private static AudioClip Build(string name, float[] s, int rate, float peak, float fadeOutSeconds)
        {
            int fade = Mathf.Min(s.Length, Mathf.RoundToInt(fadeOutSeconds * rate));
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                s[s.Length - 1 - i] *= w * w * (3f - 2f * w);
            }

            float max = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(s[i]));
            }

            float scale = max > 1e-6f ? peak / max : 1f;
            for (int i = 0; i < s.Length; i++)
            {
                s[i] *= scale;
            }

            AudioClip clip = AudioClip.Create(name, s.Length, 1, rate, false);
            clip.SetData(s, 0);
            return clip;
        }
    }
}
