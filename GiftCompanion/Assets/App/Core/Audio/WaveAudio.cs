using System;
using System.IO;
using System.Text;

namespace GiftCompanion.Core.Audio
{
    public sealed class WaveAudio
    {
        public const int MaximumBytes = 16 * 1024 * 1024;
        public int Channels { get; private set; }
        public int Frequency { get; private set; }
        public float[] Samples { get; private set; }
        public double Duration => (double)Samples.Length / Channels / Frequency;
        public static WaveAudio Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44 || bytes.Length > MaximumBytes) throw new InvalidDataException("Voice WAV size invalid.");
            using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Encoding.ASCII);
            if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Voice WAV header invalid.");
            uint declared = reader.ReadUInt32();
            if (declared + 8L != bytes.Length || new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Voice WAV length invalid.");
            int format = 0, channels = 0, rate = 0, bits = 0, align = 0, length = 0; long offset = 0;
            while (stream.Position + 8 <= stream.Length)
            {
                string tag = new string(reader.ReadChars(4)); uint size = reader.ReadUInt32(); long start = stream.Position;
                if (start + size > stream.Length) throw new InvalidDataException("Voice WAV chunk truncated.");
                if (tag == "fmt ")
                {
                    if (size < 16) throw new InvalidDataException("Voice WAV format truncated.");
                    format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32(); reader.ReadInt32(); align = reader.ReadUInt16(); bits = reader.ReadUInt16();
                }
                if (tag == "data") { offset = start; length = checked((int)size); }
                stream.Position = start + size + (size & 1);
            }
            if ((format != 1 || bits != 16) && (format != 3 || bits != 32) || channels < 1 || channels > 2 || rate < 8000 || rate > 96000 ||
                align != channels * (bits / 8) || offset == 0 || length == 0 || length % align != 0)
                throw new InvalidDataException("Voice WAV format unsupported.");
            stream.Position = offset; var samples = new float[length / (bits / 8)];
            for (int i = 0; i < samples.Length; i++)
            {
                float value = bits == 16 ? reader.ReadInt16() / 32768f : reader.ReadSingle();
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Voice WAV samples invalid.");
                samples[i] = Math.Max(-1, Math.Min(1, value));
            }
            return new WaveAudio { Channels = channels, Frequency = rate, Samples = samples };
        }
    }
}
