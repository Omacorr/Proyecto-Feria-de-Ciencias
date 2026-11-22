using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilidades compartidas de los generadores de Editor (WhisperGenerator,
/// Parte2SoundGenerator): sintesis basica (ruido, filtros, envolventes), escritura
/// de WAV e importacion de los clips. Uso: <c>using static EditorGenUtil;</c>.
/// </summary>
public static class EditorGenUtil
{
    public const int Rate = 44100;

    /// <summary>Suma de dos uniformes: ruido un poco menos "duro" que el uniforme puro.</summary>
    public static float Noise(System.Random rng)
    {
        return (float)(rng.NextDouble() + rng.NextDouble() - 1.0);
    }

    public static float Envelope(int i, int n, float attackFraction, float releaseFraction)
    {
        int minRamp = (int)(0.008f * Rate);
        int attack = Mathf.Max(minRamp, (int)(n * attackFraction));
        int release = Mathf.Max(minRamp, (int)(n * releaseFraction));
        float e = 1f;
        if (i < attack) e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / attack);
        else if (i > n - release) e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * (n - i) / release);
        return e;
    }

    public static void FadeEdges(float[] data, float inSeconds, float outSeconds)
    {
        int fi = (int)(inSeconds * Rate);
        int fo = (int)(outSeconds * Rate);
        for (int i = 0; i < fi && i < data.Length; i++) data[i] *= (float)i / fi;
        for (int i = 0; i < fo && i < data.Length; i++) data[data.Length - 1 - i] *= (float)i / fo;
    }

    public static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
        if (max < 1e-6f) return;
        float g = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= g;
    }

    /// <summary>Cierra un loop de ruido: el audio trae x muestras de mas al final y se mezclan con las primeras x.</summary>
    public static float[] MakeLoop(float[] data, int x)
    {
        int n = data.Length - x;
        var output = new float[n];
        Array.Copy(data, 0, output, 0, n);
        for (int i = 0; i < x; i++)
        {
            float w = (float)i / x;
            output[i] = data[i] * w + data[n + i] * (1f - w);
        }
        return output;
    }

    /// <summary>Escribe un WAV mono de 16 bits.</summary>
    public static void WriteWav(string path, float[] data, int rate = Rate)
    {
        using (var bw = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write)))
        {
            int bytes = data.Length * 2;
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + bytes);
            bw.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            bw.Write(16);
            bw.Write((short)1);      // PCM
            bw.Write((short)1);      // mono
            bw.Write(rate);
            bw.Write(rate * 2);      // bytes por segundo
            bw.Write((short)2);      // bytes por muestra
            bw.Write((short)16);     // bits
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(bytes);
            foreach (float v in data) bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }

    /// <summary>Crea la carpeta, escribe <c>folder/name.wav</c> y devuelve la ruta.</summary>
    public static string WriteClip(string folder, string name, float[] data, int rate = Rate)
    {
        Directory.CreateDirectory(folder);
        string path = folder + "/" + name + ".wav";
        WriteWav(path, data, rate);
        return path;
    }

    /// <summary>
    /// Refresca y configura los clips recien escritos: mono, Vorbis 0.7, preload;
    /// Decompress On Load salvo que <paramref name="compressed"/> diga CompressedInMemory;
    /// <paramref name="lowBand"/> los baja a 22050 Hz.
    /// </summary>
    public static void ImportClips(IEnumerable<string> paths, Func<string, bool> compressed = null, Func<string, bool> lowBand = null)
    {
        AssetDatabase.Refresh();
        foreach (string path in paths)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) continue;
            imp.forceToMono = true;
            imp.loadInBackground = false;
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            s.loadType = compressed != null && compressed(path) ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.preloadAudioData = true;
            if (lowBand != null && lowBand(path))
            {
                s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                s.sampleRateOverride = 22050;
            }
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }

    /// <summary>Ruido rosado (aproximacion de Paul Kellet).</summary>
    public class PinkNoise
    {
        private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;

        public float Next(float white)
        {
            _b0 = 0.99886f * _b0 + white * 0.0555179f;
            _b1 = 0.99332f * _b1 + white * 0.0750759f;
            _b2 = 0.96900f * _b2 + white * 0.1538520f;
            _b3 = 0.86650f * _b3 + white * 0.3104856f;
            _b4 = 0.55000f * _b4 + white * 0.5329522f;
            _b5 = -0.7616f * _b5 - white * 0.0168980f;
            float pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362f;
            _b6 = white * 0.115926f;
            return pink * 0.25f;
        }
    }

    /// <summary>Filtro biquad (transpuesto directa II), formulas del Audio EQ Cookbook, a 44.1 kHz.</summary>
    public class Biquad
    {
        private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;

        public void SetBandPass(float hz, float q)
        {
            Coef(hz, q, out double w, out double alpha, out double a0);
            Set(alpha / a0, 0.0, -alpha / a0, -2.0 * Math.Cos(w) / a0, (1.0 - alpha) / a0);
        }

        public void SetHighPass(float hz, float q)
        {
            Coef(hz, q, out double w, out double alpha, out double a0);
            double cos = Math.Cos(w);
            Set((1.0 + cos) / 2.0 / a0, -(1.0 + cos) / a0, (1.0 + cos) / 2.0 / a0, -2.0 * cos / a0, (1.0 - alpha) / a0);
        }

        public void SetLowPass(float hz, float q)
        {
            Coef(hz, q, out double w, out double alpha, out double a0);
            double cos = Math.Cos(w);
            Set((1.0 - cos) / 2.0 / a0, (1.0 - cos) / a0, (1.0 - cos) / 2.0 / a0, -2.0 * cos / a0, (1.0 - alpha) / a0);
        }

        private static void Coef(float hz, float q, out double w, out double alpha, out double a0)
        {
            w = 2.0 * Math.PI * Mathf.Clamp(hz, 20f, Rate * 0.45f) / Rate;
            alpha = Math.Sin(w) / (2.0 * q);
            a0 = 1.0 + alpha;
        }

        private void Set(double b0, double b1, double b2, double a1, double a2)
        {
            _b0 = (float)b0; _b1 = (float)b1; _b2 = (float)b2; _a1 = (float)a1; _a2 = (float)a2;
        }

        public float Process(float x)
        {
            float y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return y;
        }
    }
}
