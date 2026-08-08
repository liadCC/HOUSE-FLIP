using System;
using System.IO;
using System.Text;

namespace HouseFlip.EditorTools
{
    /// <summary>
    /// Minimal 16-bit PCM WAV encoder, used by <see cref="HouseFlipAudioBuilder"/> to
    /// write generated placeholder sounds to disk.
    ///
    /// Unity can import a .wav with no configuration, which is why this writes real files
    /// rather than building AudioClips in memory — the results survive a domain reload and
    /// can be auditioned, replaced or deleted like any other asset.
    /// </summary>
    public static class WavWriter
    {
        public const int SampleRate = 44100;

        /// <summary>Writes mono 16-bit PCM. Samples are clamped to [-1, 1].</summary>
        public static void Write(string path, float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            const int channels = 1;
            const int bitsPerSample = 16;
            int byteRate = SampleRate * channels * bitsPerSample / 8;
            int dataBytes = samples.Length * 2;

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                // RIFF header
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataBytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmt chunk
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);                                   // PCM chunk size
                writer.Write((short)1);                             // PCM format
                writer.Write((short)channels);
                writer.Write(SampleRate);
                writer.Write(byteRate);
                writer.Write((short)(channels * bitsPerSample / 8)); // block align
                writer.Write((short)bitsPerSample);

                // data chunk
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataBytes);

                foreach (float sample in samples)
                {
                    float clamped = sample < -1f ? -1f : (sample > 1f ? 1f : sample);

                    // 32767 rather than 32768 so +1.0 does not wrap to the most negative value.
                    writer.Write((short)Math.Round(clamped * 32767f));
                }
            }
        }
    }
}
