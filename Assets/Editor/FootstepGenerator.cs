using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static EditorGenUtil;

/// <summary>
/// Pasos generados por codigo (sin descargar nada) para los pisos que no
/// cubren los packs CC0 de Kenney y bobkingg:
/// - Pasos sueltos del jugador (5 variaciones cada uno): baldosa de hospital
///   (Parte 1), moquette humeda (Parte 5) y piso flotante de salon (Parte 2).
/// - Secuencias largas para los sustos (ExamineScareSounds toma un tramo al
///   azar): descalzo, arrastrando los pies, corriendo lejos, y pasos pesados del
///   monstruo (loop).
/// Semillas fijas: regenerar da los mismos sonidos.
/// Menu: Herramientas > Generar pasos (por piso y sustos).
/// </summary>
public static class FootstepGenerator
{
    private const string Folder = "Assets/Audios/Pasos";

    [MenuItem("Herramientas/Generar pasos (por piso y sustos)")]
    public static void Generate()
    {
        var written = new List<string>();
        void Save(string name, float[] data) => written.Add(WriteClip(Folder, name, data));

        for (int i = 0; i < 5; i++)
        {
            Save("paso_baldosa_0" + (i + 1), TileStep(1000 + i));
            Save("paso_moquette_0" + (i + 1), DampCarpetStep(2000 + i));
            Save("paso_salon_0" + (i + 1), LaminateStep(3000 + i));
        }
        Save("pasos_descalzo", Sequence(4000, 14, 0.62f, 0.12f, BarefootStep, 0.85f));
        Save("pasos_arrastrando", Sequence(5000, 9, 1.15f, 0.15f, DragStep, 0.8f));
        Save("pasos_corriendo_lejos", DistantRun(6000));
        Save("pasos_monstruo_pesado", Sequence(7000, 9, 0.95f, 0f, HeavyStomp, 0.9f));

        ImportClips(written, p => p.Contains("pasos_"));
        Debug.Log("[FootstepGenerator] Generados " + written.Count + " clips en " + Folder);
    }

    // ---------- pasos sueltos ----------

    // Zapato sobre baldosa: taco seco (click agudo + golpe corto), punta un poco
    // despues, y una cola chica de sala dura.
    private static float[] TileStep(int seed)
    {
        var rng = new System.Random(seed);
        float[] d = new float[(int)(0.42f * Rate)];
        float pitch = 0.9f + (float)rng.NextDouble() * 0.2f;
        AddHit(d, rng, 0.004f, 1f, 2600f * pitch, 0.006f, 140f * pitch, 0.035f, 4200f, 0.012f, 0.55f);
        AddHit(d, rng, 0.075f + (float)rng.NextDouble() * 0.02f, 0.45f, 3100f * pitch, 0.004f, 170f, 0.02f, 5000f, 0.008f, 0.4f);
        AddTail(d, rng, 0.006f, 0.16f, 2200f, 0.07f);
        Normalize(d, 0.8f);
        return d;
    }

    // Moquette humeda: golpe sordo y apagado + "chapoteo" chico de agua.
    private static float[] DampCarpetStep(int seed)
    {
        var rng = new System.Random(seed);
        float[] d = new float[(int)(0.45f * Rate)];
        var lp = new Biquad(); lp.SetLowPass(650f + (float)rng.NextDouble() * 150f, 0.8f);
        var bp = new Biquad(); bp.SetBandPass(1300f + (float)rng.NextDouble() * 500f, 3f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float thud = lp.Process(Noise(rng)) * Mathf.Exp(-t / 0.07f) * Mathf.Clamp01(t / 0.008f);
            float body = Mathf.Sin(2f * Mathf.PI * 75f * t) * Mathf.Exp(-t / 0.05f) * 0.5f;
            // chapoteo: rafaga modulada que llega un poco despues del apoyo
            float st = t - 0.05f;
            float squish = st > 0f ? bp.Process(Noise(rng)) * Mathf.Exp(-st / 0.09f) * (0.6f + 0.4f * Mathf.Sin(st * 180f)) * 0.35f : bp.Process(0f);
            d[i] = thud * 1.4f + body + squish;
        }
        Normalize(d, 0.7f);
        return d;
    }

    // Piso flotante / vinilico: golpe hueco (resonancia media) + click suave.
    private static float[] LaminateStep(int seed)
    {
        var rng = new System.Random(seed);
        float[] d = new float[(int)(0.36f * Rate)];
        float pitch = 0.9f + (float)rng.NextDouble() * 0.2f;
        AddHit(d, rng, 0.004f, 1f, 1500f * pitch, 0.006f, 205f * pitch, 0.06f, 2600f, 0.01f, 0.35f);
        AddHit(d, rng, 0.08f + (float)rng.NextDouble() * 0.02f, 0.35f, 1800f, 0.004f, 230f, 0.03f, 3000f, 0.006f, 0.3f);
        AddTail(d, rng, 0.006f, 0.08f, 1400f, 0.05f);
        Normalize(d, 0.75f);
        return d;
    }

    // ---------- pasos para secuencias de sustos ----------

    private delegate void StepFn(float[] d, int start, System.Random rng, float gain);

    // Pie descalzo: palmada de piel corta + golpe grave blando.
    private static void BarefootStep(float[] d, int start, System.Random rng, float gain)
    {
        var hp = new Biquad(); hp.SetHighPass(1800f, 0.7f);
        var lp = new Biquad(); lp.SetLowPass(500f, 0.7f);
        int n = (int)(0.22f * Rate);
        float f = 65f + (float)rng.NextDouble() * 15f;
        for (int i = 0; i < n && start + i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float slap = hp.Process(Noise(rng)) * Mathf.Exp(-t / 0.006f);
            float soft = lp.Process(Noise(rng)) * Mathf.Exp(-t / 0.04f) * 0.8f;
            float body = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / 0.06f) * 0.6f;
            d[start + i] += (slap * 0.9f + soft + body) * gain;
        }
    }

    // Arrastrando los pies: apoyo blando + roce largo y aspero.
    private static void DragStep(float[] d, int start, System.Random rng, float gain)
    {
        var bp = new Biquad(); bp.SetBandPass(900f + (float)rng.NextDouble() * 400f, 0.9f);
        var lp = new Biquad(); lp.SetLowPass(400f, 0.7f);
        int n = (int)(0.75f * Rate);
        float scrape = 0.4f + (float)rng.NextDouble() * 0.25f;
        for (int i = 0; i < n && start + i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float thud = lp.Process(Noise(rng)) * Mathf.Exp(-t / 0.05f);
            float env = Mathf.Clamp01((t - 0.03f) / 0.08f) * Mathf.Clamp01((scrape + 0.1f - t) / 0.15f);
            float rough = 0.55f + 0.45f * Mathf.PerlinNoise(t * 40f, start * 0.001f);
            float sh = bp.Process(Noise(rng)) * env * rough * 0.7f;
            d[start + i] += (thud * 1.2f + sh) * gain;
        }
    }

    // Pisada pesada del monstruo: golpe muy grave y largo + crujido.
    private static void HeavyStomp(float[] d, int start, System.Random rng, float gain)
    {
        var lp = new Biquad(); lp.SetLowPass(220f, 0.9f);
        var bp = new Biquad(); bp.SetBandPass(700f, 1.2f);
        int n = (int)(0.6f * Rate);
        float f = 48f + (float)rng.NextDouble() * 10f;
        for (int i = 0; i < n && start + i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float a = Mathf.Clamp01(t / 0.01f);
            float body = Mathf.Sin(2f * Mathf.PI * f * t * (1f - 0.25f * t)) * Mathf.Exp(-t / 0.16f);
            float rumble = lp.Process(Noise(rng)) * Mathf.Exp(-t / 0.12f) * 1.5f;
            float crunch = bp.Process(Noise(rng)) * Mathf.Exp(-t / 0.05f) * 0.5f;
            d[start + i] += (body + rumble + crunch) * a * gain;
        }
    }

    // Arma una secuencia de pasos: intervalo con jitter, ganancia variable.
    private static float[] Sequence(int seed, int steps, float interval, float jitter, StepFn step, float peak)
    {
        var rng = new System.Random(seed);
        float total = steps * interval;
        float[] d = new float[(int)(total * Rate)];
        float t = 0.03f;
        for (int s = 0; s < steps; s++)
        {
            float g = 0.75f + (float)rng.NextDouble() * 0.25f;
            if (s % 2 == 1) g *= 0.88f; // un pie pisa un poco mas fuerte
            step(d, (int)(t * Rate), rng, g);
            t += interval + ((float)rng.NextDouble() * 2f - 1f) * jitter;
            if (t >= total - 0.05f) break;
        }
        FadeEdges(d, 0.01f, 0.08f);
        Normalize(d, peak);
        return d;
    }

    // Alguien corre a lo lejos: pasos rapidos, filtrados como si vinieran de
    // otra sala, que se acercan y se alejan.
    private static float[] DistantRun(int seed)
    {
        var rng = new System.Random(seed);
        float total = 9f;
        float[] d = new float[(int)(total * Rate)];
        float t = 0.2f;
        while (t < total - 0.4f)
        {
            float k = t / total;
            float g = Mathf.Sin(k * Mathf.PI); // se acerca y se va
            g = 0.25f + 0.75f * g;
            BarefootStep(d, (int)(t * Rate), rng, g * (0.8f + (float)rng.NextDouble() * 0.2f));
            t += 0.29f + ((float)rng.NextDouble() * 2f - 1f) * 0.03f;
        }
        var lp = new Biquad(); lp.SetLowPass(900f, 0.7f);
        float[] wet = new float[d.Length];
        int delay = (int)(0.045f * Rate);
        for (int i = 0; i < d.Length; i++)
        {
            float x = lp.Process(d[i]);
            float echo = i >= delay ? wet[i - delay] * 0.45f : 0f;
            wet[i] = x + echo;
        }
        FadeEdges(wet, 0.3f, 0.5f);
        Normalize(wet, 0.7f);
        return wet;
    }

    // ---------- piezas ----------

    // Golpe = click tonal + golpe grave + ruido agudo corto.
    private static void AddHit(float[] d, System.Random rng, float at, float gain,
        float clickHz, float clickDecay, float bodyHz, float bodyDecay, float noiseHz, float noiseDecay, float noiseGain)
    {
        var hp = new Biquad(); hp.SetHighPass(noiseHz, 0.7f);
        int start = (int)(at * Rate);
        int n = (int)(0.25f * Rate);
        for (int i = 0; i < n && start + i < d.Length; i++)
        {
            float t = (float)i / Rate;
            float click = Mathf.Sin(2f * Mathf.PI * clickHz * t) * Mathf.Exp(-t / clickDecay);
            float body = Mathf.Sin(2f * Mathf.PI * bodyHz * t) * Mathf.Exp(-t / bodyDecay);
            float nz = hp.Process(Noise(rng)) * Mathf.Exp(-t / noiseDecay) * noiseGain;
            d[start + i] += (click * 0.6f + body + nz) * gain;
        }
    }

    // Cola de sala: ruido filtrado que decae despacio.
    private static void AddTail(float[] d, System.Random rng, float at, float decay, float lpHz, float gain)
    {
        var lp = new Biquad(); lp.SetLowPass(lpHz, 0.7f);
        int start = (int)(at * Rate);
        for (int i = start; i < d.Length; i++)
        {
            float t = (float)(i - start) / Rate;
            d[i] += lp.Process(Noise(rng)) * Mathf.Exp(-t / decay) * gain;
        }
    }
}
