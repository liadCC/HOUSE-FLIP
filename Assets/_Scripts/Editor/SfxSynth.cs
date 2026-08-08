using System;

namespace HouseFlip.EditorTools
{
    /// <summary>
    /// Tiny synthesiser for the placeholder sound set (GDD 24).
    ///
    /// These are not final audio — they are the difference between a silent prototype and
    /// one you can actually feel. Everything is deterministic (fixed RNG seed) so
    /// regenerating produces byte-identical files and does not churn the repository.
    ///
    /// The palette leans cartoon: fast attacks, pitch sweeps, and short tails.
    /// </summary>
    public static class SfxSynth
    {
        private const int Rate = WavWriter.SampleRate;

        private static Random _rng = new Random(20260808);

        public static void ResetSeed() => _rng = new Random(20260808);

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        private static float[] Buffer(float seconds) => new float[Math.Max(1, (int)(Rate * seconds))];

        private static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        /// <summary>Exponential decay envelope — the shape of almost every percussive sound.</summary>
        private static float Decay(float t, float duration, float sharpness = 5f)
        {
            float x = t / duration;
            return x >= 1f ? 0f : (float)Math.Exp(-sharpness * x);
        }

        /// <summary>
        /// Keeps the file from popping at its edges.
        ///
        /// The fade-in is deliberately tiny. These sounds live almost entirely in their
        /// attack — a sharply decaying click puts all of its energy in the first few
        /// milliseconds — so a symmetric fade would quietly erase the sound itself. A few
        /// samples is enough to kill a DC pop; the real taper belongs on the tail.
        /// </summary>
        private static void DeClick(float[] buffer, int fadeOutSamples = 128)
        {
            int fadeIn = Math.Min(16, buffer.Length / 4);
            for (int i = 0; i < fadeIn; i++)
            {
                buffer[i] *= (float)i / fadeIn;
            }

            int fadeOut = Math.Min(fadeOutSamples, buffer.Length / 2);
            for (int i = 0; i < fadeOut; i++)
            {
                buffer[buffer.Length - 1 - i] *= (float)i / fadeOut;
            }
        }

        private static void Normalise(float[] buffer, float peak = 0.85f)
        {
            float max = 0f;
            foreach (float s in buffer)
            {
                float a = Math.Abs(s);
                if (a > max) max = a;
            }

            if (max < 1e-6f)
            {
                return;
            }

            float gain = peak / max;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= gain;
            }
        }

        /// <summary>Sine with a linear frequency sweep from <paramref name="fromHz"/> to <paramref name="toHz"/>.</summary>
        private static float[] Sweep(float seconds, float fromHz, float toHz, float sharpness, bool square = false)
        {
            float[] buffer = Buffer(seconds);
            double phase = 0.0;

            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / Rate;
                float k = t / seconds;
                double frequency = fromHz + (toHz - fromHz) * k;
                phase += 2.0 * Math.PI * frequency / Rate;

                float wave = square
                    ? (Math.Sin(phase) >= 0 ? 1f : -1f)
                    : (float)Math.Sin(phase);

                buffer[i] = wave * Decay(t, seconds, sharpness);
            }

            return buffer;
        }

        private static float[] NoiseBurst(float seconds, float sharpness, float lowPass = 1f)
        {
            float[] buffer = Buffer(seconds);
            float previous = 0f;

            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / Rate;

                // One-pole low-pass: lowPass of 1 leaves it bright, lower values dull it.
                float raw = Noise();
                previous += (raw - previous) * lowPass;

                buffer[i] = previous * Decay(t, seconds, sharpness);
            }

            return buffer;
        }

        private static float[] Mix(params float[][] layers)
        {
            int length = 0;
            foreach (float[] layer in layers)
            {
                if (layer.Length > length) length = layer.Length;
            }

            var result = new float[length];
            foreach (float[] layer in layers)
            {
                for (int i = 0; i < layer.Length; i++)
                {
                    result[i] += layer[i];
                }
            }

            return result;
        }

        private static float[] Concat(params float[][] parts)
        {
            int total = 0;
            foreach (float[] part in parts) total += part.Length;

            var result = new float[total];
            int offset = 0;
            foreach (float[] part in parts)
            {
                Array.Copy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        private static float[] Tone(float seconds, float hz, float sharpness, float amplitude = 1f)
        {
            float[] buffer = Buffer(seconds);
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / Rate;
                buffer[i] = (float)Math.Sin(2.0 * Math.PI * hz * t) * Decay(t, seconds, sharpness) * amplitude;
            }

            return buffer;
        }

        /// <summary>De-click first, then normalise, so the requested peak is the real one.</summary>
        private static float[] Finish(float[] buffer, float peak = 0.85f)
        {
            DeClick(buffer);
            Normalise(buffer, peak);
            return buffer;
        }

        // ------------------------------------------------------------------
        // The sound set (GDD 24)
        // ------------------------------------------------------------------

        /// <summary>Wooden thunk plus a bright transient — one hammer swing connecting.</summary>
        public static float[] HammerHit() =>
            Finish(Mix(
                Sweep(0.12f, 180f, 70f, 9f),
                NoiseBurst(0.05f, 24f, 0.5f)));

        /// <summary>Something giving way: a crunch with a falling body under it.</summary>
        public static float[] ObjectBreak() =>
            Finish(Mix(
                NoiseBurst(0.45f, 7f, 0.35f),
                Sweep(0.35f, 220f, 55f, 6f),
                NoiseBurst(0.12f, 20f, 0.9f)));

        /// <summary>Rising two-note chime: something got better.</summary>
        public static float[] BuildComplete() =>
            Finish(Concat(
                Tone(0.10f, 660f, 5f),
                Tone(0.22f, 880f, 4f)));

        public static float[] Pickup() => Finish(Sweep(0.09f, 420f, 760f, 8f, square: true), 0.6f);

        public static float[] Drop() => Finish(Sweep(0.11f, 500f, 180f, 8f, square: true), 0.6f);

        /// <summary>Airy whoosh for a launched sofa.</summary>
        public static float[] Throw() =>
            Finish(Mix(
                NoiseBurst(0.30f, 4f, 0.12f),
                Sweep(0.28f, 300f, 900f, 3f)), 0.7f);

        /// <summary>Burbling loop for the water leak. Longer, so it can be left running.</summary>
        public static float[] WaterGurgle()
        {
            float seconds = 1.6f;
            float[] buffer = Buffer(seconds);
            float previous = 0f;

            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / Rate;

                // Wobbling low-pass on noise reads as bubbling far better than plain noise.
                float wobble = 0.05f + 0.045f * (float)Math.Sin(2.0 * Math.PI * 3.1 * t);
                previous += (Noise() - previous) * wobble;

                float swell = 0.6f + 0.4f * (float)Math.Sin(2.0 * Math.PI * 0.7 * t);
                buffer[i] = previous * swell;
            }

            return Finish(buffer, 0.55f);
        }

        /// <summary>Electrical crack: a few sharp ticks in quick succession.</summary>
        public static float[] ElectricalSpark() =>
            Finish(Concat(
                NoiseBurst(0.05f, 40f, 1f),
                Buffer(0.03f),
                NoiseBurst(0.07f, 30f, 0.85f),
                Buffer(0.02f),
                NoiseBurst(0.11f, 18f, 1f)));

        /// <summary>Ka-ching: a mechanical click followed by the bell.</summary>
        public static float[] CashRegister() =>
            Finish(Concat(
                NoiseBurst(0.04f, 45f, 0.8f),
                Mix(Tone(0.40f, 1318f, 3.5f, 0.7f), Tone(0.40f, 1976f, 4f, 0.4f))));

        public static float[] UIClick() => Finish(NoiseBurst(0.035f, 45f, 0.75f), 0.45f);

        /// <summary>Soft swish for the vacuum.</summary>
        public static float[] Clean() => Finish(NoiseBurst(0.26f, 6f, 0.08f), 0.5f);

        /// <summary>Ratchet clicks resolving into a confirming note.</summary>
        public static float[] Repair() =>
            Finish(Concat(
                NoiseBurst(0.03f, 50f, 0.7f),
                Buffer(0.035f),
                NoiseBurst(0.03f, 50f, 0.7f),
                Buffer(0.035f),
                Tone(0.24f, 740f, 4f)));

        public static float[] Paint() => Finish(NoiseBurst(0.32f, 5f, 0.05f), 0.45f);

        /// <summary>Major arpeggio — you made money.</summary>
        public static float[] SuccessFanfare() =>
            Finish(Concat(
                Tone(0.13f, 523f, 3f),
                Tone(0.13f, 659f, 3f),
                Tone(0.13f, 784f, 3f),
                Tone(0.55f, 1046f, 2f)));

        /// <summary>Descending "wah wah" — you did not.</summary>
        public static float[] FailSound() =>
            Finish(Concat(
                Sweep(0.22f, 330f, 300f, 2.5f, square: true),
                Sweep(0.22f, 294f, 262f, 2.5f, square: true),
                Sweep(0.55f, 247f, 180f, 2.2f, square: true)), 0.7f);

        /// <summary>Two-tone alert for a random event firing.</summary>
        public static float[] EventAlarm() =>
            Finish(Concat(
                Tone(0.16f, 880f, 2.5f),
                Tone(0.16f, 660f, 2.5f),
                Tone(0.16f, 880f, 2.5f),
                Tone(0.28f, 660f, 3f)), 0.7f);

        /// <summary>
        /// A short, loopable, deliberately silly backing track: a walking bass under a
        /// bouncing arpeggio in C major. Placeholder, but it loops seamlessly and keeps
        /// the prototype from feeling dead.
        /// </summary>
        public static float[] BackgroundMusic()
        {
            const float bpm = 116f;
            float beat = 60f / bpm;
            const int beats = 32;

            float[] buffer = Buffer(beat * beats);

            // C major: bass root per bar, arpeggio riding on top.
            int[] bassNotes = { 131, 131, 165, 165, 175, 175, 196, 147 };
            int[] arp = { 523, 659, 784, 659, 587, 698, 880, 698 };

            for (int b = 0; b < beats; b++)
            {
                int bar = (b / 4) % bassNotes.Length;
                int start = (int)(b * beat * Rate);

                AddNote(buffer, start, beat * 0.9f, bassNotes[bar], 0.32f, square: true, sharpness: 2.2f);

                // Two arpeggio notes per beat, giving it a skipping feel.
                for (int half = 0; half < 2; half++)
                {
                    int index = (b * 2 + half) % arp.Length;
                    int noteStart = start + (int)(half * beat * 0.5f * Rate);
                    AddNote(buffer, noteStart, beat * 0.42f, arp[index], 0.16f, square: false, sharpness: 4.5f);
                }
            }

            Normalise(buffer, 0.6f);
            DeClick(buffer, 512);
            return buffer;
        }

        private static void AddNote(float[] buffer, int start, float seconds, float hz,
            float amplitude, bool square, float sharpness)
        {
            int length = (int)(seconds * Rate);

            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index < 0 || index >= buffer.Length)
                {
                    break;
                }

                float t = (float)i / Rate;
                double phase = 2.0 * Math.PI * hz * t;
                float wave = square ? (Math.Sin(phase) >= 0 ? 1f : -1f) : (float)Math.Sin(phase);

                buffer[index] += wave * Decay(t, seconds, sharpness) * amplitude;
            }
        }
    }
}
