using System.Buffers.Binary;

namespace HeroesOfIdlestan.Core.Render.Audio;

/// <summary>
/// Décodeur WAV minimal pour les heads qui mixent eux-mêmes (miniaudio) : PCM entier 8/16/24/32
/// bits ou flottant 32 bits, mono ou multicanal, ramené en mono flottant.
/// </summary>
public sealed class WavSample
{
    private WavSample(float[] samples, int sampleRate)
    {
        Samples = samples;
        SampleRate = sampleRate;
    }

    /// <summary>Échantillons mono dans [-1, 1].</summary>
    public float[] Samples { get; }

    public int SampleRate { get; }

    /// <summary>Décode un WAV, ou renvoie null s'il est illisible ou d'un format non pris en charge.</summary>
    public static WavSample? TryDecode(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 44) return null;
        if (!wav[..4].SequenceEqual("RIFF"u8) || !wav.Slice(8, 4).SequenceEqual("WAVE"u8)) return null;

        int format = 0, channels = 0, sampleRate = 0, bits = 0;
        ReadOnlySpan<byte> data = default;
        int offset = 12;
        while (offset + 8 <= wav.Length)
        {
            var id = wav.Slice(offset, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(offset + 4, 4));
            int body = offset + 8;
            // Taille annoncée plus grande que le fichier (enregistrement interrompu) : on lit ce qui existe.
            int available = Math.Min((int)Math.Min(size, int.MaxValue), wav.Length - body);
            if (available < 0) return null;

            if (id.SequenceEqual("fmt "u8) && available >= 16)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body + 2, 2));
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(body + 4, 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body + 14, 2));
                // WAVE_FORMAT_EXTENSIBLE : le vrai code de format est au début du sous-GUID.
                if (format == 0xFFFE && available >= 26)
                    format = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body + 24, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                data = wav.Slice(body, available);
            }
            // Chunks alignés sur 2 octets.
            offset = body + available + (available & 1);
        }

        const int PcmInteger = 1, PcmFloat = 3;
        if (channels <= 0 || sampleRate <= 0 || data.IsEmpty) return null;
        if (format != PcmInteger && format != PcmFloat) return null;
        if (format == PcmFloat && bits != 32) return null;
        int bytesPerSample = bits / 8;
        if (bytesPerSample is < 1 or > 4 || bits % 8 != 0) return null;

        int frameBytes = bytesPerSample * channels;
        int frames = data.Length / frameBytes;
        if (frames == 0) return null;

        var samples = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            int at = f * frameBytes;
            for (int c = 0; c < channels; c++)
                sum += ReadSample(data.Slice(at + c * bytesPerSample, bytesPerSample), format, bits);
            samples[f] = sum / channels;
        }
        return new WavSample(samples, sampleRate);
    }

    private static float ReadSample(ReadOnlySpan<byte> bytes, int format, int bits)
    {
        if (format == 3) return BinaryPrimitives.ReadSingleLittleEndian(bytes);
        return bits switch
        {
            8 => (bytes[0] - 128) / 128f, // seul le 8 bits est non signé
            16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f,
            24 => (bytes[0] | (bytes[1] << 8) | ((sbyte)bytes[2] << 16)) / 8388608f,
            32 => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648f,
            _ => 0f,
        };
    }

    /// <summary>Rééchantillonnage linéaire, fait une fois au chargement plutôt qu'à chaque lecture.</summary>
    public WavSample Resample(int targetSampleRate)
    {
        if (targetSampleRate <= 0 || targetSampleRate == SampleRate) return this;
        double ratio = (double)targetSampleRate / SampleRate;
        int count = Math.Max(1, (int)(Samples.Length * ratio));
        var output = new float[count];
        for (int i = 0; i < count; i++)
        {
            double source = i / ratio;
            int index = (int)source;
            float a = Samples[Math.Min(index, Samples.Length - 1)];
            float b = Samples[Math.Min(index + 1, Samples.Length - 1)];
            output[i] = a + (b - a) * (float)(source - index);
        }
        return new WavSample(output, targetSampleRate);
    }
}
