using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera los susurros y la respiracion de Parte 1 (sonidos mientras se ingresa
/// el codigo del candado) sintetizandolos por codigo: no hay que bajar nada ni
/// preocuparse por licencias.
///
/// Un susurro es voz sin cuerdas vocales: ruido filtrado por las resonancias de
/// la boca (formantes). Cada "silaba" es una consonante de ruido (s, sh, h, f,
/// t) seguida de una vocal hecha con tres filtros pasabanda en los formantes de
/// a/e/i/o/u, con transiciones suaves entre vocales. El resultado es un
/// murmullo ininteligible, que es justamente lo que da miedo.
///
/// Menu: Herramientas > Generar susurros (Parte 1). Los .wav quedan en
/// Assets/Audios/Susurros y se pueden regenerar cuando se quiera (mismas
/// semillas = mismos sonidos). Cuando Omar pase el susurro grabado, se puede
/// reemplazar o sumar a la lista del componente ExamineScareSounds.
/// </summary>
public static class WhisperGenerator
{
    private const int Rate = 44100;
    private const string Folder = "Assets/Audios/Susurros";

    // Formantes (Hz) de vocales habladas: F1, F2, F3.
    private static readonly float[][] Vowels =
    {
        new[] { 730f, 1090f, 2440f }, // a
        new[] { 530f, 1840f, 2480f }, // e
        new[] { 300f, 2250f, 3000f }, // i
        new[] { 570f, 840f, 2410f },  // o
        new[] { 320f, 870f, 2240f },  // u
    };

    [MenuItem("Herramientas/Generar susurros (Parte 1)")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        var written = new List<string>();
        int[] syllables = { 4, 6, 5, 7, 3 };
        for (int i = 0; i < syllables.Length; i++)
        {
            string path = Folder + "/susurro_" + (i + 1).ToString("00") + ".wav";
            WriteWav(path, Whisper(1000 + i * 37, syllables[i]));
            written.Add(path);
        }
        string breath = Folder + "/respiracion_01.wav";
        WriteWav(breath, Breath(77));
        written.Add(breath);

        AssetDatabase.Refresh();
        foreach (string path in written)
        {
            if (AssetImporter.GetAtPath(path) is AudioImporter imp)
            {
                imp.forceToMono = true;
                imp.loadInBackground = false;
                AudioImporterSampleSettings s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = true;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }
        Debug.Log("[WhisperGenerator] Generados " + written.Count + " clips en " + Folder);
    }

    // ------------------------------------------------------------------ susurro

    private static float[] Whisper(int seed, int syllableCount)
    {
        var rng = new System.Random(seed);
        var output = new List<float>();
        AppendSilence(output, 0.05f);

        float[] prevVowel = Vowels[rng.Next(Vowels.Length)];
        for (int s = 0; s < syllableCount; s++)
        {
            float loud = 0.6f + 0.4f * (float)rng.NextDouble();
            if (rng.NextDouble() < 0.75)
            {
                AppendConsonant(output, rng, loud);
            }
            float[] vowel = Vowels[rng.Next(Vowels.Length)];
            float vowelSeconds = 0.09f + 0.15f * (float)rng.NextDouble();
            AppendVowel(output, rng, prevVowel, vowel, vowelSeconds, loud);
            prevVowel = vowel;

            // Pausa entre silabas; cada tanto una pausa de "palabra".
            bool wordGap = s < syllableCount - 1 && rng.NextDouble() < 0.35;
            AppendSilence(output, wordGap ? 0.12f + 0.13f * (float)rng.NextDouble() : 0.015f + 0.06f * (float)rng.NextDouble());
        }
        // Una "s" arrastrada al final le da el tono de susurro.
        AppendNoiseSegment(output, rng, 0.18f, loud: 0.5f, bandHz: 6500f, q: 1.4f, highPassHz: 0f);
        AppendSilence(output, 0.15f);

        float[] data = output.ToArray();
        Normalize(data, 0.7f);
        return data;
    }

    private static void AppendConsonant(List<float> output, System.Random rng, float loud)
    {
        switch (rng.Next(5))
        {
            case 0: // s
                AppendNoiseSegment(output, rng, 0.06f + 0.07f * (float)rng.NextDouble(), loud * 0.9f, 6500f, 1.5f, 0f);
                break;
            case 1: // sh
                AppendNoiseSegment(output, rng, 0.07f + 0.07f * (float)rng.NextDouble(), loud, 2800f, 1.8f, 0f);
                break;
            case 2: // h
                AppendNoiseSegment(output, rng, 0.05f + 0.05f * (float)rng.NextDouble(), loud * 0.6f, 1600f, 0.8f, 0f);
                break;
            case 3: // f
                AppendNoiseSegment(output, rng, 0.06f + 0.05f * (float)rng.NextDouble(), loud * 0.35f, 0f, 0f, 2500f);
                break;
            default: // t (golpe corto)
                AppendNoiseSegment(output, rng, 0.018f, loud, 0f, 0f, 3000f);
                AppendSilence(output, 0.02f);
                break;
        }
    }

    private static void AppendNoiseSegment(List<float> output, System.Random rng, float seconds, float loud, float bandHz, float q, float highPassHz)
    {
        int n = Mathf.Max(1, (int)(seconds * Rate));
        var band = new Biquad();
        if (bandHz > 0f)
        {
            band.SetBandPass(bandHz, q);
        }
        else
        {
            band.SetHighPass(highPassHz, 0.7f);
        }
        for (int i = 0; i < n; i++)
        {
            float x = band.Process(Noise(rng));
            output.Add(x * loud * Envelope(i, n, 0.2f, 0.35f) * 2.2f);
        }
    }

    private static void AppendVowel(List<float> output, System.Random rng, float[] from, float[] to, float seconds, float loud)
    {
        int n = Mathf.Max(1, (int)(seconds * Rate));
        var f1 = new Biquad();
        var f2 = new Biquad();
        var f3 = new Biquad();
        var breathy = new Biquad();
        breathy.SetHighPass(1200f, 0.7f);
        const int block = 32;
        for (int i = 0; i < n; i++)
        {
            if (i % block == 0)
            {
                // Coarticulacion: los formantes se deslizan de la vocal anterior
                // a la nueva durante el primer 40% del segmento.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(i / (n * 0.4f)));
                f1.SetBandPass(Mathf.Lerp(from[0], to[0], k), 6f);
                f2.SetBandPass(Mathf.Lerp(from[1], to[1], k), 8f);
                f3.SetBandPass(Mathf.Lerp(from[2], to[2], k), 10f);
            }
            float noise = Noise(rng);
            float y = f1.Process(noise) * 1.0f + f2.Process(noise) * 0.7f + f3.Process(noise) * 0.35f + breathy.Process(noise) * 0.12f;
            float tremolo = 1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 5f * i / Rate);
            output.Add(y * loud * tremolo * Envelope(i, n, 0.15f, 0.3f) * 3.0f);
        }
    }

    // --------------------------------------------------------------- respiracion

    private static float[] Breath(int seed)
    {
        var rng = new System.Random(seed);
        var output = new List<float>();
        AppendSilence(output, 0.05f);
        AppendBreathPart(output, rng, 0.9f, 900f, 0.9f, 0.7f);  // inspiracion
        AppendSilence(output, 0.25f);
        AppendBreathPart(output, rng, 1.2f, 550f, 0.7f, 1.0f);  // exhalacion
        AppendSilence(output, 0.1f);
        float[] data = output.ToArray();
        Normalize(data, 0.6f);
        return data;
    }

    private static void AppendBreathPart(List<float> output, System.Random rng, float seconds, float bandHz, float q, float loud)
    {
        int n = (int)(seconds * Rate);
        var band = new Biquad();
        band.SetBandPass(bandHz, q);
        var low = new Biquad();
        low.SetLowPass(2500f, 0.7f);
        for (int i = 0; i < n; i++)
        {
            float x = low.Process(band.Process(Noise(rng)));
            float env = Mathf.Sin(Mathf.PI * i / n);
            output.Add(x * loud * env * env * 2.5f);
        }
    }

    // ------------------------------------------------------------------- helpers

    private static float Noise(System.Random rng)
    {
        // Suma de dos uniformes: ruido un poco menos "duro" que el uniforme puro.
        return (float)(rng.NextDouble() + rng.NextDouble() - 1.0);
    }

    private static float Envelope(int i, int n, float attackFraction, float releaseFraction)
    {
        int minRamp = (int)(0.008f * Rate);
        int attack = Mathf.Max(minRamp, (int)(n * attackFraction));
        int release = Mathf.Max(minRamp, (int)(n * releaseFraction));
        float e = 1f;
        if (i < attack)
        {
            e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / attack);
        }
        else if (i > n - release)
        {
            e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * (n - i) / release);
        }
        return e;
    }

    private static void AppendSilence(List<float> output, float seconds)
    {
        int n = (int)(seconds * Rate);
        for (int i = 0; i < n; i++)
        {
            output.Add(0f);
        }
    }

    private static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (float v in data)
        {
            max = Mathf.Max(max, Mathf.Abs(v));
        }
        if (max < 1e-6f)
        {
            return;
        }
        float g = peak / max;
        for (int i = 0; i < data.Length; i++)
        {
            data[i] *= g;
        }
    }

    private static void WriteWav(string path, float[] data)
    {
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            int bytes = data.Length * 2;
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + bytes);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);      // PCM
            bw.Write((short)1);      // mono
            bw.Write(Rate);
            bw.Write(Rate * 2);      // bytes por segundo
            bw.Write((short)2);      // bytes por muestra
            bw.Write((short)16);     // bits
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(bytes);
            foreach (float v in data)
            {
                bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            }
        }
    }

    /// <summary>Filtro biquad (transpuesto directa II), formulas del Audio EQ Cookbook.</summary>
    private class Biquad
    {
        private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;

        public void SetBandPass(float hz, float q)
        {
            double w = 2.0 * Math.PI * Mathf.Clamp(hz, 20f, Rate * 0.45f) / Rate;
            double alpha = Math.Sin(w) / (2.0 * q);
            double a0 = 1.0 + alpha;
            Set(alpha / a0, 0.0, -alpha / a0, -2.0 * Math.Cos(w) / a0, (1.0 - alpha) / a0);
        }

        public void SetHighPass(float hz, float q)
        {
            double w = 2.0 * Math.PI * Mathf.Clamp(hz, 20f, Rate * 0.45f) / Rate;
            double alpha = Math.Sin(w) / (2.0 * q);
            double cos = Math.Cos(w);
            double a0 = 1.0 + alpha;
            Set((1.0 + cos) / 2.0 / a0, -(1.0 + cos) / a0, (1.0 + cos) / 2.0 / a0, -2.0 * cos / a0, (1.0 - alpha) / a0);
        }

        public void SetLowPass(float hz, float q)
        {
            double w = 2.0 * Math.PI * Mathf.Clamp(hz, 20f, Rate * 0.45f) / Rate;
            double alpha = Math.Sin(w) / (2.0 * q);
            double cos = Math.Cos(w);
            double a0 = 1.0 + alpha;
            Set((1.0 - cos) / 2.0 / a0, (1.0 - cos) / a0, (1.0 - cos) / 2.0 / a0, -2.0 * cos / a0, (1.0 - alpha) / a0);
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
