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

    // ----------------------------------------------------------------- efectos

    private const string EffectsFolder = "Assets/Audios/Efectos";

    /// <summary>
    /// Efectos del guion que no existian en el proyecto (2026-09-25), tambien
    /// sintetizados por codigo: latidos del despertar (Parte 1), golpe seco del
    /// desmayo, viento de la caida al abismo (Parte 3), derrumbe de madera y luz
    /// que absorbe (Parte 4), zumbido fluorescente en loop (Parte 5) y pitido de
    /// monitor de hospital (final, regular y linea plana).
    /// </summary>
    [MenuItem("Herramientas/Generar efectos de sonido (latidos, golpe, viento, derrumbe...)")]
    public static void GenerateEffects()
    {
        Directory.CreateDirectory(EffectsFolder);
        var written = new List<string>();
        void Save(string name, float[] data)
        {
            string path = EffectsFolder + "/" + name + ".wav";
            WriteWav(path, data);
            written.Add(path);
        }
        Save("latidos_despertar", Heartbeat(11));
        Save("golpe_seco", Impact(22));
        Save("caida_viento", FallWind(33));
        Save("derrumbe_madera", WoodCollapse(44));
        Save("luz_absorcion", LightSwell(55));
        Save("zumbido_fluorescente", FluorescentHum(66));
        Save("monitor_bip", MonitorBeeps(false));
        Save("monitor_linea_plana", MonitorBeeps(true));
        Save("crujido_madera_01", SingleCreak(71, 1.1f, 520f));
        Save("crujido_madera_02", SingleCreak(72, 1.6f, 380f));
        Save("crujido_madera_03", SingleCreak(73, 0.8f, 700f));
        Save("golpes_madera", Knocks(81));

        AssetDatabase.Refresh();
        foreach (string path in written)
        {
            if (AssetImporter.GetAtPath(path) is AudioImporter imp)
            {
                imp.forceToMono = true;
                AudioImporterSampleSettings s = imp.defaultSampleSettings;
                bool longClip = path.Contains("zumbido") || path.Contains("latidos") || path.Contains("derrumbe") || path.Contains("monitor");
                s.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = true;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }
        Debug.Log("[WhisperGenerator] Generados " + written.Count + " efectos en " + EffectsFolder);
    }

    // Latidos acelerados que se van calmando (~10 s): "lub" grave y "dub" mas
    // suave, como escuchados desde adentro del cuerpo (todo filtrado grave).
    private static float[] Heartbeat(int seed)
    {
        var rng = new System.Random(seed);
        float seconds = 10f;
        var data = new float[(int)(seconds * Rate)];
        float t = 0.15f;
        while (t < seconds - 0.6f)
        {
            float k = t / seconds;                       // 0 -> 1
            float bpm = Mathf.Lerp(138f, 78f, k * k * (3f - 2f * k));
            float loud = Mathf.Lerp(1f, 0.55f, k);
            AddThump(data, t, 62f, 38f, 0.16f, loud, rng);
            AddThump(data, t + 60f / bpm * 0.30f, 74f, 46f, 0.12f, loud * 0.6f, rng);
            t += 60f / bpm;
        }
        var low = new Biquad();
        low.SetLowPass(420f, 0.7f);
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = low.Process(data[i]);
        }
        FadeEdges(data, 0.05f, 1.5f);
        Normalize(data, 0.9f);
        return data;
    }

    private static void AddThump(float[] data, float start, float f0, float f1, float decay, float loud, System.Random rng)
    {
        int s0 = (int)(start * Rate);
        int n = (int)((decay * 4f) * Rate);
        double phase = 0;
        for (int i = 0; i < n && s0 + i < data.Length; i++)
        {
            float time = (float)i / Rate;
            float f = Mathf.Lerp(f1, f0, Mathf.Exp(-time / 0.05f));
            phase += 2.0 * Math.PI * f / Rate;
            float env = (1f - Mathf.Exp(-time / 0.004f)) * Mathf.Exp(-time / decay);
            float click = time < 0.012f ? Noise(rng) * 0.25f * (1f - time / 0.012f) : 0f;
            data[s0 + i] += (float)Math.Sin(phase) * env * loud + click * loud;
        }
    }

    // Golpe seco del desmayo (~1.6 s): click, cuerpo grave que cae de tono y
    // una cola de retumbe.
    private static float[] Impact(int seed)
    {
        var rng = new System.Random(seed);
        var data = new float[(int)(1.6f * Rate)];
        var body = new Biquad();
        body.SetLowPass(900f, 0.8f);
        var rumble = new Biquad();
        rumble.SetLowPass(140f, 0.7f);
        double phase = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / Rate;
            float f = Mathf.Lerp(34f, 95f, Mathf.Exp(-t / 0.06f));
            phase += 2.0 * Math.PI * f / Rate;
            float sub = (float)Math.Sin(phase) * Mathf.Exp(-t / 0.35f);
            float n = Noise(rng);
            float thud = body.Process(n) * Mathf.Exp(-t / 0.07f) * 1.6f;
            float tail = rumble.Process(n) * Mathf.Exp(-t / 0.55f) * 2.2f;
            float click = t < 0.006f ? n * (1f - t / 0.006f) * 0.8f : 0f;
            data[i] = (sub * 1.1f + thud + tail + click) * (1f - Mathf.Exp(-t / 0.0015f));
        }
        FadeEdges(data, 0f, 0.3f);
        Normalize(data, 0.95f);
        return data;
    }

    // Viento de la caida al abismo (~3.4 s): ruido que sube de tono y de
    // volumen, con rafagas, mas un retumbe grave que crece.
    private static float[] FallWind(int seed)
    {
        var rng = new System.Random(seed);
        var data = new float[(int)(3.4f * Rate)];
        var band = new Biquad();
        var low = new Biquad();
        low.SetLowPass(110f, 0.7f);
        float gust = 0f, gustTarget = 1f;
        for (int i = 0; i < data.Length; i++)
        {
            float k = (float)i / data.Length;
            if (i % 64 == 0)
            {
                band.SetBandPass(Mathf.Lerp(280f, 1900f, k * k), 0.9f);
            }
            if (i % 2205 == 0)
            {
                gustTarget = 0.6f + 0.4f * (float)rng.NextDouble();
            }
            gust += (gustTarget - gust) * 0.0006f;
            float n = Noise(rng);
            float swell = Mathf.Lerp(0.25f, 1f, k);
            data[i] = (band.Process(n) * 2.2f + low.Process(n) * 1.8f * k) * swell * gust;
        }
        FadeEdges(data, 0.25f, 0.2f);
        Normalize(data, 0.9f);
        return data;
    }

    // Derrumbe de la pared de madera (~7.5 s): crujidos y chasquidos (0-1.5 s,
    // mientras se agrieta), el derrumbe con golpes y escombros (1.5-2.8 s) y un
    // retumbe que se apaga. Coincide con los tiempos de WallCollapse.
    private static float[] WoodCollapse(int seed)
    {
        var rng = new System.Random(seed);
        float seconds = 7.5f;
        var data = new float[(int)(seconds * Rate)];

        // Crujidos: trenes de impulsos (friccion) por un resonador de madera.
        for (int c = 0; c < 5; c++)
        {
            float start = 0.1f + (float)rng.NextDouble() * 1.3f;
            float len = 0.25f + (float)rng.NextDouble() * 0.35f;
            AddCreak(data, rng, start, len, 450f + (float)rng.NextDouble() * 500f, 0.35f + (float)rng.NextDouble() * 0.3f);
        }
        // Chasquidos sueltos mientras se agrieta, cada vez mas seguidos.
        for (float t = 0.2f; t < 1.5f; t += 0.08f + (float)rng.NextDouble() * (0.35f - 0.2f * t))
        {
            AddCrack(data, rng, t, 0.5f + 0.5f * (float)rng.NextDouble());
        }
        // Derrumbe: golpes graves + lluvia de escombros.
        for (int b = 0; b < 4; b++)
        {
            AddBoom(data, rng, 1.5f + b * 0.28f + (float)rng.NextDouble() * 0.1f, 1.0f - b * 0.15f);
        }
        for (float t = 1.5f; t < 3.2f; t += 0.01f + (float)rng.NextDouble() * 0.05f)
        {
            AddCrack(data, rng, t, 0.25f + 0.45f * (float)rng.NextDouble() * (1f - (t - 1.5f) / 1.7f));
        }
        // Retumbe largo.
        var low = new Biquad();
        low.SetLowPass(120f, 0.7f);
        for (int i = (int)(1.5f * Rate); i < data.Length; i++)
        {
            float t = (float)i / Rate - 1.5f;
            data[i] += low.Process(Noise(rng)) * 2.5f * Mathf.Exp(-t / 1.6f);
        }
        FadeEdges(data, 0.02f, 1.0f);
        Normalize(data, 0.95f);
        return data;
    }

    // Un crujido de madera suelto (piso o pared que cede), para sonar lejos.
    private static float[] SingleCreak(int seed, float seconds, float resonanceHz)
    {
        var rng = new System.Random(seed);
        var data = new float[(int)((seconds + 0.3f) * Rate)];
        AddCreak(data, rng, 0.05f, seconds, resonanceHz, 0.6f);
        AddCreak(data, rng, 0.05f + seconds * 0.35f, seconds * 0.5f, resonanceHz * 1.6f, 0.25f);
        FadeEdges(data, 0.01f, 0.2f);
        Normalize(data, 0.85f);
        return data;
    }

    // Tres golpes secos en madera (alguien golpea del otro lado de la pared).
    private static float[] Knocks(int seed)
    {
        var rng = new System.Random(seed);
        var data = new float[(int)(2.2f * Rate)];
        float[] times = { 0.05f, 0.42f, 0.78f };
        foreach (float t in times)
        {
            int s0 = (int)(t * Rate);
            var body = new Biquad();
            body.SetBandPass(180f, 3f);
            var wood = new Biquad();
            wood.SetBandPass(900f, 4f);
            int n = (int)(0.35f * Rate);
            for (int i = 0; i < n && s0 + i < data.Length; i++)
            {
                float time = (float)i / Rate;
                float x = time < 0.004f ? Noise(rng) : Noise(rng) * 0.15f;
                float env = Mathf.Exp(-time / 0.06f);
                data[s0 + i] += (body.Process(x) * 3f + wood.Process(x) * 1.5f) * env;
            }
        }
        FadeEdges(data, 0f, 0.3f);
        Normalize(data, 0.9f);
        return data;
    }

    private static void AddCreak(float[] data, System.Random rng, float start, float seconds, float resonanceHz, float loud)
    {
        var res = new Biquad();
        res.SetBandPass(resonanceHz, 9f);
        int s0 = (int)(start * Rate);
        int n = (int)(seconds * Rate);
        float rate = 45f + (float)rng.NextDouble() * 60f;
        float next = 0f;
        for (int i = 0; i < n && s0 + i < data.Length; i++)
        {
            float t = (float)i / Rate;
            float x = 0f;
            if (t >= next)
            {
                x = 1f;
                float r = rate * (1f + 0.4f * Mathf.Sin(2f * Mathf.PI * 2.3f * t));
                next = t + (1f / r) * (0.8f + 0.4f * (float)rng.NextDouble());
            }
            data[s0 + i] += res.Process(x) * loud * 4f * Envelope(i, n, 0.2f, 0.4f);
        }
    }

    private static void AddCrack(float[] data, System.Random rng, float start, float loud)
    {
        var band = new Biquad();
        band.SetBandPass(1500f + (float)rng.NextDouble() * 3000f, 1.2f);
        int s0 = (int)(start * Rate);
        int n = (int)(0.06f * Rate);
        for (int i = 0; i < n && s0 + i < data.Length; i++)
        {
            float t = (float)i / Rate;
            data[s0 + i] += band.Process(Noise(rng)) * loud * 2.5f * Mathf.Exp(-t / 0.012f);
        }
    }

    private static void AddBoom(float[] data, System.Random rng, float start, float loud)
    {
        var low = new Biquad();
        low.SetLowPass(250f, 0.8f);
        int s0 = (int)(start * Rate);
        int n = (int)(0.9f * Rate);
        double phase = 0;
        for (int i = 0; i < n && s0 + i < data.Length; i++)
        {
            float t = (float)i / Rate;
            phase += 2.0 * Math.PI * Mathf.Lerp(40f, 85f, Mathf.Exp(-t / 0.05f)) / Rate;
            float env = (1f - Mathf.Exp(-t / 0.002f)) * Mathf.Exp(-t / 0.22f);
            data[s0 + i] += ((float)Math.Sin(phase) * 0.9f + low.Process(Noise(rng)) * 1.5f) * env * loud;
        }
    }

    // "Luz que te absorbe" (Parte 4, suena al empezar el fundido de 1.5 s):
    // una subida de ruido y tono que termina de golpe en el corte.
    private static float[] LightSwell(int seed)
    {
        var rng = new System.Random(seed);
        float seconds = 2.2f;
        var data = new float[(int)(seconds * Rate)];
        var band = new Biquad();
        double p1 = 0, p2 = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float k = (float)i / data.Length;
            if (i % 64 == 0)
            {
                band.SetBandPass(Mathf.Lerp(220f, 4200f, k * k), 1.4f);
            }
            float f = Mathf.Lerp(180f, 720f, k * k);
            p1 += 2.0 * Math.PI * f / Rate;
            p2 += 2.0 * Math.PI * f * 1.502 / Rate;     // quinta un poco desafinada
            float tone = (float)(Math.Sin(p1) + 0.6 * Math.Sin(p2)) * 0.35f;
            float swell = Mathf.Pow(k, 2.2f);
            data[i] = (band.Process(Noise(rng)) * 2f + tone) * swell;
        }
        FadeEdges(data, 0.05f, 0.02f);
        Normalize(data, 0.9f);
        return data;
    }

    // Zumbido de tubos fluorescentes (Parte 5), 8 s que empalman en loop: 100 Hz
    // (el doble de la red de 50 Hz) con armonicos, un chisporroteo del balasto y
    // una leve fluctuacion. Todas las frecuencias dan ciclos enteros en 8 s.
    private static float[] FluorescentHum(int seed)
    {
        var rng = new System.Random(seed);
        float seconds = 8f;
        int n = (int)(seconds * Rate);
        var data = new float[n];
        float[] amps = { 1f, 0.55f, 0.35f, 0.22f, 0.15f, 0.1f, 0.07f };
        var buzz = new Biquad();
        buzz.SetBandPass(3200f, 2f);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            double hum = 0;
            for (int h = 0; h < amps.Length; h++)
            {
                hum += amps[h] * Math.Sin(2.0 * Math.PI * 100.0 * (h + 1) * t + h * 0.7);
            }
            // Fluctuacion lenta (0.25 Hz y 0.5 Hz = ciclos enteros en 8 s).
            double wobble = 1.0 + 0.06 * Math.Sin(2.0 * Math.PI * 0.25 * t) + 0.04 * Math.Sin(2.0 * Math.PI * 0.5 * t + 1.3);
            // Chisporroteo modulado a 100 Hz (suena "electrico").
            double gate = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * 100.0 * t);
            float crackle = buzz.Process(Noise(rng)) * (float)(gate * gate) * 0.5f;
            data[i] = (float)(hum * 0.18 * wobble) + crackle;
        }
        // El ruido no es periodico: se funden los ultimos 0.3 s con el principio.
        int x = (int)(0.3f * Rate);
        for (int i = 0; i < x; i++)
        {
            float w = (float)i / x;
            data[i] = data[i] * w + data[n - x + i] * (1f - w);
        }
        var looped = new float[n - x];
        Array.Copy(data, 0, looped, 0, n - x);
        Normalize(looped, 0.7f);
        return looped;
    }

    // Monitor cardiaco: bips de 1 kHz a 72 por minuto. Con flatLine, despues de
    // cuatro bips queda el tono continuo de "linea plana".
    private static float[] MonitorBeeps(bool flatLine)
    {
        float seconds = flatLine ? 9f : 8f;
        var data = new float[(int)(seconds * Rate)];
        float period = 60f / 72f;
        int beeps = flatLine ? 4 : Mathf.FloorToInt((seconds - 0.3f) / period);
        for (int b = 0; b < beeps; b++)
        {
            AddTone(data, 0.3f + b * period, 0.12f, 1000f, 0.8f);
        }
        if (flatLine)
        {
            float start = 0.3f + beeps * period;
            AddTone(data, start, seconds - start - 0.05f, 1000f, 0.8f);
        }
        Normalize(data, 0.6f);
        return data;
    }

    private static void AddTone(float[] data, float start, float seconds, float hz, float loud)
    {
        int s0 = (int)(start * Rate);
        int n = (int)(seconds * Rate);
        int ramp = (int)(0.005f * Rate);
        for (int i = 0; i < n && s0 + i < data.Length; i++)
        {
            float env = Mathf.Min(1f, Mathf.Min((float)i / ramp, (float)(n - i) / ramp));
            data[s0 + i] += Mathf.Sin(2f * Mathf.PI * hz * i / Rate) * loud * env;
        }
    }

    private static void FadeEdges(float[] data, float inSeconds, float outSeconds)
    {
        int fi = (int)(inSeconds * Rate);
        int fo = (int)(outSeconds * Rate);
        for (int i = 0; i < fi && i < data.Length; i++)
        {
            data[i] *= (float)i / fi;
        }
        for (int i = 0; i < fo && i < data.Length; i++)
        {
            data[data.Length - 1 - i] *= (float)i / fo;
        }
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
