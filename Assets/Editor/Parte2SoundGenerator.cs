using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera por sintesis (sin descargar nada, sin licencias) los sonidos de
/// Parte 2 - Cumpleanos: cajita musical, version distorsionada, estatica de TV,
/// videocasetera, respiracion / pasos / rugido del monstruo y ambiente del salon.
///
/// Menu: Herramientas > Generar sonidos (Parte 2). Los .wav (mono, 44.1 kHz,
/// pico maximo 0.8) quedan en Assets/Audios/Parte2. Las semillas son fijas:
/// regenerar da exactamente los mismos sonidos. Los loops se cierran con
/// crossfade (ruido) o escribiendo las colas de forma circular (tonos), asi no
/// hay click en la union.
/// </summary>
public static class Parte2SoundGenerator
{
    private const int Rate = 44100;
    private const string Folder = "Assets/Audios/Parte2";
    private const float Peak = 0.8f;

    [MenuItem("Herramientas/Generar sonidos (Parte 2)")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        var written = new List<string>();
        void Save(string name, float[] data)
        {
            Normalize(data, Peak);
            string path = Folder + "/" + name + ".wav";
            WriteWav(path, data);
            written.Add(path);
        }

        Save("cumple_cajita", MusicBox(101));
        Save("cumple_cajita_distorsionada", MusicBoxDying(102));
        Save("tv_estatica", TvStatic(103));
        Save("vhs_insertar", VhsInsert(104));
        Save("monstruo_respiracion", MonsterBreath(105));
        Save("monstruo_pasos", MonsterSteps(106));
        Save("monstruo_rugido", MonsterRoar(107));
        Save("ambiente_salon", RoomTone(108));

        AssetDatabase.Refresh();
        foreach (string path in written)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter imp))
            {
                continue;
            }
            string name = Path.GetFileNameWithoutExtension(path);
            bool longLoop = name == "ambiente_salon" || name == "cumple_cajita" || name == "tv_estatica"
                            || name == "monstruo_respiracion" || name == "monstruo_pasos";
            bool lowBand = name.StartsWith("monstruo_") || name == "ambiente_salon";

            imp.forceToMono = true;
            imp.loadInBackground = false;
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            s.loadType = longLoop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.preloadAudioData = true;
            if (lowBand)
            {
                s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                s.sampleRateOverride = 22050;
            }
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
        Debug.Log("[Parte2SoundGenerator] Generados " + written.Count + " clips en " + Folder);
    }

    // ------------------------------------------------------------------ cajita

    // "Feliz cumpleanos" (dominio publico): semitonos sobre la tonica y duracion
    // en tiempos.
    private static readonly float[][] Melody =
    {
        new[] { 7f, .75f }, new[] { 7f, .25f }, new[] { 9f, 1f }, new[] { 7f, 1f }, new[] { 12f, 1f }, new[] { 11f, 2f },
        new[] { 7f, .75f }, new[] { 7f, .25f }, new[] { 9f, 1f }, new[] { 7f, 1f }, new[] { 14f, 1f }, new[] { 12f, 2f },
        new[] { 7f, .75f }, new[] { 7f, .25f }, new[] { 19f, 1f }, new[] { 16f, 1f }, new[] { 12f, 1f }, new[] { 11f, 1f }, new[] { 9f, 2f },
        new[] { 17f, .75f }, new[] { 17f, .25f }, new[] { 16f, 1f }, new[] { 12f, 1f }, new[] { 14f, 1f }, new[] { 12f, 2f },
    };

    // Cajita musical: metalofono con armonicos que decaen rapido, un poco
    // desafinada y lenta. 25 tiempos a 0.72 s = 18 s; las colas de las ultimas
    // notas se escriben "dando la vuelta" al principio, asi el loop es limpio.
    private static float[] MusicBox(int seed)
    {
        var rng = new System.Random(seed);
        float beat = 0.72f;
        float totalBeats = 0f;
        foreach (float[] n in Melody)
        {
            totalBeats += n[1];
        }
        var data = new float[(int)(totalBeats * beat * Rate)];
        float t = 0f;
        foreach (float[] n in Melody)
        {
            // Cada nota un poco desafinada (+-14 centesimos) y de volumen irregular.
            float cents = ((float)rng.NextDouble() * 2f - 1f) * 14f;
            float freq = 523.25f * Mathf.Pow(2f, (n[0] + cents / 100f) / 12f);
            float amp = 0.8f + 0.2f * (float)rng.NextDouble();
            AddBell(data, (int)(t * Rate), freq, amp, 1f, true);
            t += n[1] * beat;
        }
        return data;
    }

    // Una nota de metalofono: parciales ligeramente inarmonicos, cada uno con su
    // propio decaimiento (los agudos mueren antes). wrap = escribir en circular.
    private static void AddBell(float[] data, int start, float freq, float amp, float decayScale, bool wrap)
    {
        float[] ratio = { 1f, 2.005f, 3.02f, 4.23f, 5.9f };
        float[] level = { 1f, 0.38f, 0.22f, 0.12f, 0.06f };
        float[] decay = { 1.1f, 0.55f, 0.32f, 0.18f, 0.1f };
        int n = (int)(3.2f * decayScale * Rate);
        for (int p = 0; p < ratio.Length; p++)
        {
            double w = 2.0 * Math.PI * freq * ratio[p] / Rate;
            if (freq * ratio[p] > Rate * 0.45f)
            {
                continue;
            }
            float tau = decay[p] * decayScale;
            for (int i = 0; i < n; i++)
            {
                float time = (float)i / Rate;
                float env = (1f - Mathf.Exp(-time / 0.0015f)) * Mathf.Exp(-time / tau);
                if (env < 0.0005f && time > 0.05f)
                {
                    break;
                }
                int idx = start + i;
                if (wrap)
                {
                    idx %= data.Length;
                }
                else if (idx >= data.Length)
                {
                    break;
                }
                data[idx] += (float)Math.Sin(w * i) * env * level[p] * amp * 0.25f;
            }
        }
    }

    // La misma melodia como cuerda que se acaba: el tempo se alarga y el tono cae
    // (~10 s), con wow/flutter, bitcrush leve y siseo; termina arrastrandose.
    private static float[] MusicBoxDying(int seed)
    {
        var rng = new System.Random(seed);
        float seconds = 10f;
        var data = new float[(int)(seconds * Rate)];
        float t = 0f;
        int noteIndex = 0;
        while (t < seconds - 0.8f && noteIndex < Melody.Length)
        {
            float[] n = Melody[noteIndex++];
            float p = t / seconds;
            float pitchRatio = Mathf.Exp(-t * 0.075f);
            float cents = ((float)rng.NextDouble() * 2f - 1f) * (14f + 40f * p);
            float freq = 523.25f * Mathf.Pow(2f, (n[0] + cents / 100f) / 12f) * pitchRatio;
            AddBell(data, (int)(t * Rate), freq, 0.9f, 1f + 1.5f * p, false);
            float beat = 0.30f * Mathf.Exp(t * 0.19f);
            t += n[1] * beat;
        }

        // Wow (lento) y flutter (rapido): se relee el audio con un retardo variable.
        var wobbled = new float[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            float time = (float)i / Rate;
            float p = time / seconds;
            float d = (30f + 160f * p) * Mathf.Sin(2f * Mathf.PI * (0.6f + 1.2f * p) * time)
                      + 14f * Mathf.Sin(2f * Mathf.PI * 7.3f * time);
            float pos = i + d;
            int i0 = Mathf.FloorToInt(pos);
            float fr = pos - i0;
            float a = (i0 >= 0 && i0 < data.Length) ? data[i0] : 0f;
            float b = (i0 + 1 >= 0 && i0 + 1 < data.Length) ? data[i0 + 1] : 0f;
            wobbled[i] = a * (1f - fr) + b * fr;
        }

        // Bitcrush leve que crece + siseo de cinta.
        var hiss = new Biquad();
        hiss.SetHighPass(2500f, 0.7f);
        float held = 0f;
        for (int i = 0; i < wobbled.Length; i++)
        {
            float p = (float)i / wobbled.Length;
            int hold = p < 0.3f ? 1 : (p < 0.65f ? 2 : 3);
            if (i % hold == 0)
            {
                float levels = Mathf.Lerp(96f, 24f, p);
                held = Mathf.Round(wobbled[i] * levels) / levels;
            }
            float mix = Mathf.Lerp(0.25f, 0.7f, p);
            float y = wobbled[i] * (1f - mix) + held * mix;
            y += hiss.Process(Noise(rng)) * (0.02f + 0.05f * p);
            wobbled[i] = y;
        }
        FadeEdges(wobbled, 0.01f, 1.2f);
        return wobbled;
    }

    // --------------------------------------------------------------------- TV

    // Estatica de TV, 4 s en loop: ruido rosado filtrado, zumbido de 50 Hz con su
    // armonico y chasquidos. 50 Hz da 200 ciclos exactos en 4 s; el ruido se cierra
    // con crossfade.
    private static float[] TvStatic(int seed)
    {
        var rng = new System.Random(seed);
        int n = 4 * Rate;
        int x = (int)(0.4f * Rate);
        var data = new float[n + x];
        var pink = new PinkNoise();
        var hp = new Biquad();
        hp.SetHighPass(180f, 0.7f);
        var lp = new Biquad();
        lp.SetLowPass(9000f, 0.7f);
        float shimmer = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            double t = (double)i / Rate;
            if (i % 441 == 0)
            {
                shimmer = 0.75f + 0.25f * (float)rng.NextDouble();
            }
            float noise = lp.Process(hp.Process(pink.Next(Noise(rng)))) * 1.6f * shimmer;
            float hum = (float)(Math.Sin(2.0 * Math.PI * 50.0 * t) * 0.07 + Math.Sin(2.0 * Math.PI * 100.0 * t + 0.6) * 0.045);
            data[i] = noise + hum;
        }
        // Chasquidos / pops de la senal.
        for (int c = 0; c < 9; c++)
        {
            int at = (int)((0.2f + (float)rng.NextDouble() * 3.6f) * Rate);
            float sign = rng.NextDouble() < 0.5 ? -1f : 1f;
            int len = 20 + rng.Next(80);
            for (int i = 0; i < len && at + i < data.Length; i++)
            {
                data[at + i] += sign * (0.5f + 0.4f * (float)rng.NextDouble()) * Mathf.Exp(-i / (len * 0.3f)) * Noise(rng) * 2f;
            }
        }
        return MakeLoop(data, x);
    }

    // ------------------------------------------------------------------- VHS

    // Videocasetera al insertar el cassette (~1.5 s): click plastico seco y un
    // motor que arranca (zumbido grave que sube de tono y volumen) con un
    // chirrido de engranajes.
    private static float[] VhsInsert(int seed)
    {
        var rng = new System.Random(seed);
        var data = new float[(int)(1.5f * Rate)];
        var click = new Biquad();
        click.SetBandPass(2600f, 1.5f);
        var thunk = new Biquad();
        thunk.SetLowPass(260f, 0.8f);
        var motorNoise = new Biquad();
        motorNoise.SetBandPass(700f, 1.2f);
        double phase = 0, whine = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / Rate;
            float n = Noise(rng);
            // Click inicial + golpe grave del seguro.
            float c = click.Process(n) * Mathf.Exp(-t / 0.006f) * 2.5f;
            float th = thunk.Process(n) * Mathf.Exp(-t / 0.05f) * 3f;
            // Segundo click suave (traba) a los 0.28 s.
            float t2 = t - 0.28f;
            float c2 = t2 > 0f ? click.Process(n) * Mathf.Exp(-t2 / 0.004f) * 1.2f : 0f;
            // Motor: arranca a los 0.18 s.
            float m = 0f;
            float tm = t - 0.18f;
            if (tm > 0f)
            {
                float k = Mathf.Clamp01(tm / 0.9f);
                float f = Mathf.Lerp(35f, 130f, 1f - Mathf.Exp(-tm / 0.35f));
                phase += 2.0 * Math.PI * f / Rate;
                whine += 2.0 * Math.PI * Mathf.Lerp(500f, 1650f, k) / Rate;
                float saw = (float)(Math.Sin(phase) + 0.5 * Math.Sin(2 * phase) + 0.3 * Math.Sin(3 * phase));
                m = (saw * 0.35f + motorNoise.Process(n) * 0.5f + (float)Math.Sin(whine) * 0.08f) * Mathf.Min(1f, tm / 0.25f) * Mathf.Lerp(0.5f, 1f, k);
            }
            data[i] = c + th + c2 + m;
        }
        FadeEdges(data, 0f, 0.25f);
        return data;
    }

    // ---------------------------------------------------------------- monstruo

    // Respiracion grave, lenta y humeda (5 s en loop): inhalar y exhalar con
    // ruido muy grave, un gruñido de fondo y un burbujeo de baja frecuencia.
    // Empieza y termina en silencio, asi cierra sin click.
    private static float[] MonsterBreath(int seed)
    {
        var rng = new System.Random(seed);
        var data = new float[5 * Rate];
        var band = new Biquad();
        band.SetBandPass(230f, 0.6f);
        var low = new Biquad();
        low.SetLowPass(700f, 0.7f);
        var wet = new Biquad();     // modulador "humedo": ruido muy lento
        wet.SetLowPass(28f, 0.7f);
        var growl = new Biquad();
        growl.SetLowPass(120f, 0.9f);
        double gp = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / Rate;
            float n = Noise(rng);
            float inhale = BumpEnv(t, 0.25f, 1.65f);
            float exhale = BumpEnv(t, 2.1f, 2.45f);
            float env = inhale * 0.65f + exhale;
            // Humedad: el nivel se "rompe" con un modulador de ruido lento.
            float wobble = Mathf.Clamp(0.55f + wet.Process(Noise(rng)) * 9f, 0.1f, 1.3f);
            float breath = low.Process(band.Process(n)) * 5.5f * env * wobble;
            // Gruñido en la exhalacion: tono grave con vibrato lento.
            gp += 2.0 * Math.PI * (52f + 6f * Mathf.Sin(2f * Mathf.PI * 3f * t)) / Rate;
            float g = ((float)Math.Sin(gp) * 0.4f + growl.Process(n) * 2f) * exhale * 0.7f;
            data[i] = breath + g;
        }
        FadeEdges(data, 0.03f, 0.03f);
        return data;
    }

    // Pasos pesados y lentos: 4 pasos a 0.7 pasos/s (5.7 s). Cada uno es un golpe
    // grave con cola; las colas dan la vuelta al principio para cerrar el loop.
    private static float[] MonsterSteps(int seed)
    {
        var rng = new System.Random(seed);
        float period = 1f / 0.7f;
        var data = new float[(int)(4 * period * Rate)];
        for (int s = 0; s < 4; s++)
        {
            float loud = (s % 2 == 0) ? 1f : 0.85f;
            float pitch = (s % 2 == 0) ? 1f : 0.9f;
            float jitter = ((float)rng.NextDouble() - 0.5f) * 0.05f;
            AddHeavyStep(data, rng, (int)((s * period + 0.05f + jitter) * Rate), loud, pitch);
        }
        return data;
    }

    private static void AddHeavyStep(float[] data, System.Random rng, int start, float loud, float pitch)
    {
        var body = new Biquad();
        body.SetLowPass(220f * pitch, 0.8f);
        var scrape = new Biquad();
        scrape.SetBandPass(380f, 1.5f);
        var tail = new Biquad();
        tail.SetLowPass(90f, 0.7f);
        double phase = 0;
        int n = (int)(1.2f * Rate);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float f = Mathf.Lerp(32f, 78f, Mathf.Exp(-t / 0.05f)) * pitch;
            phase += 2.0 * Math.PI * f / Rate;
            float noise = Noise(rng);
            float attack = 1f - Mathf.Exp(-t / 0.003f);
            float thump = (float)Math.Sin(phase) * Mathf.Exp(-t / 0.20f);
            float thud = body.Process(noise) * Mathf.Exp(-t / 0.06f) * 2.2f;
            float rumble = tail.Process(noise) * Mathf.Exp(-t / 0.4f) * 4f;
            // Roce del pie arrastrandose, un instante despues del golpe.
            float ts = t - 0.09f;
            float drag = ts > 0f ? scrape.Process(noise) * Mathf.Exp(-ts / 0.09f) * 0.5f * (1f - Mathf.Exp(-ts / 0.02f)) : 0f;
            data[(start + i) % data.Length] += (thump * 1.1f + thud + rumble + drag) * attack * loud;
        }
    }

    // Rugido corto del monstruo (~1.8 s): sub-bajo que cae, mas ruido aspero
    // pasado por formantes (voz abierta, "aaa") y distorsionado con modulacion.
    private static float[] MonsterRoar(int seed)
    {
        var rng = new System.Random(seed);
        var data = new float[(int)(1.8f * Rate)];
        var f1 = new Biquad();
        var f2 = new Biquad();
        var f3 = new Biquad();
        var low = new Biquad();
        low.SetLowPass(160f, 0.7f);
        double sub = 0, saw = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / Rate;
            if (i % 32 == 0)
            {
                // Los formantes se cierran con el tiempo ("aaa" -> "ooo").
                float k = Mathf.Clamp01(t / 1.4f);
                f1.SetBandPass(Mathf.Lerp(760f, 450f, k), 5f);
                f2.SetBandPass(Mathf.Lerp(1150f, 800f, k), 6f);
                f3.SetBandPass(Mathf.Lerp(2500f, 2100f, k), 7f);
            }
            float env = (1f - Mathf.Exp(-t / 0.025f)) * Mathf.Exp(-t / 0.55f);
            sub += 2.0 * Math.PI * Mathf.Lerp(34f, 75f, Mathf.Exp(-t / 0.12f)) / Rate;
            float subTone = (float)Math.Sin(sub) * Mathf.Exp(-t / 0.7f);
            // Diente de sierra grave (cuerdas vocales) con vibrato aspero de 38 Hz.
            saw += 2.0 * Math.PI * (68f - 18f * t + 8f * Mathf.Sin(2f * Mathf.PI * 38f * t)) / Rate;
            float buzz = (float)((saw % (2 * Math.PI)) / Math.PI - 1.0);
            float n = Noise(rng);
            float src = buzz * 0.7f + n * 0.5f;
            float voice = f1.Process(src) * 1.0f + f2.Process(src) * 0.7f + f3.Process(src) * 0.35f;
            float rough = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 41f * t);
            float y = (float)Math.Tanh(voice * 3.5f * rough) * 0.9f;
            // Golpe inicial.
            float hit = t < 0.05f ? low.Process(n) * (1f - t / 0.05f) * 3f : low.Process(n) * 0.2f * Mathf.Exp(-t / 0.3f);
            data[i] = (subTone * 1.3f + y * 0.9f + hit) * env;
        }
        FadeEdges(data, 0f, 0.35f);
        return data;
    }

    // ----------------------------------------------------------------- ambiente

    // Salon vacio (20 s, loop): room tone muy grave, ventilacion que respira
    // lenta y crujidos lejanos ocasionales. El ruido se cierra con crossfade; los
    // crujidos se escriben en circular.
    private static float[] RoomTone(int seed)
    {
        var rng = new System.Random(seed);
        int n = 20 * Rate;
        int x = (int)(1.5f * Rate);
        var raw = new float[n + x];
        var brown = new Biquad();
        brown.SetLowPass(140f, 0.7f);
        var air = new Biquad();
        air.SetBandPass(420f, 0.5f);
        var hiss = new Biquad();
        hiss.SetBandPass(2200f, 0.6f);
        for (int i = 0; i < raw.Length; i++)
        {
            float t = (float)i / Rate;
            float noise = Noise(rng);
            float tone = brown.Process(noise) * 7f;
            // Ventilacion: sube y baja cada ~10 s y 6.7 s.
            float swell = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.1f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.15f * t + 1.1f);
            float vent = air.Process(noise) * 1.3f * swell + hiss.Process(noise) * 0.35f * swell;
            float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.025f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.012f;
            raw[i] = tone + vent + hum;
        }
        float[] data = MakeLoop(raw, x);

        // Crujidos lejanos (6 repartidos), chicos y amortiguados.
        float[] times = { 2.4f, 5.9f, 8.7f, 12.3f, 15.6f, 18.4f };
        for (int c = 0; c < times.Length; c++)
        {
            float len = 0.4f + (float)rng.NextDouble() * 0.9f;
            float res = 260f + (float)rng.NextDouble() * 500f;
            float loud = 0.12f + 0.12f * (float)rng.NextDouble();
            AddFarCreak(data, rng, (int)(times[c] * Rate), len, res, loud);
        }
        return data;
    }

    private static void AddFarCreak(float[] data, System.Random rng, int start, float seconds, float resHz, float loud)
    {
        var res = new Biquad();
        res.SetBandPass(resHz, 8f);
        var soften = new Biquad();
        soften.SetLowPass(1500f, 0.7f);
        int n = (int)(seconds * Rate);
        float rate = 40f + (float)rng.NextDouble() * 50f;
        float next = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float imp = 0f;
            if (t >= next)
            {
                imp = 1f;
                float r = rate * (1f + 0.4f * Mathf.Sin(2f * Mathf.PI * 2.1f * t));
                next = t + (1f / r) * (0.8f + 0.4f * (float)rng.NextDouble());
            }
            float env = Envelope(i, n, 0.25f, 0.45f);
            data[(start + i) % data.Length] += soften.Process(res.Process(imp)) * loud * 5f * env;
        }
    }

    // ------------------------------------------------------------------ helpers

    // Campana suave con silencio fuera de [a, b] (para inhalar / exhalar).
    private static float BumpEnv(float t, float a, float b)
    {
        if (t <= a || t >= b)
        {
            return 0f;
        }
        float s = Mathf.Sin(Mathf.PI * (t - a) / (b - a));
        return s * s;
    }

    // Cierra un loop de ruido: el audio trae x muestras de mas al final; las
    // primeras x muestras se mezclan con ese sobrante y listo (union continua).
    private static float[] MakeLoop(float[] data, int x)
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

    private static float Noise(System.Random rng)
    {
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
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(Rate);
            bw.Write(Rate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(bytes);
            foreach (float v in data)
            {
                bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            }
        }
    }

    /// <summary>Ruido rosado (aproximacion de Paul Kellet).</summary>
    private class PinkNoise
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

    /// <summary>Filtro biquad (Audio EQ Cookbook).</summary>
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
