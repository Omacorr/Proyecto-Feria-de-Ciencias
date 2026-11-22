using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carga los tres escalones de texto de las pistas en el HintDirector de cada
/// escena: voiceTextSoft (vaga), voiceText (media, la de siempre) y
/// voiceTextDirect (casi la respuesta). Los textos se emparejan con cada
/// objetivo por su label (sin importar mayusculas ni espacios de los bordes).
///
/// Abre cada escena (Single), la guarda y al final vuelve a abrir la que estaba
/// activa al empezar. Si la escena activa tiene cambios sin guardar, primero
/// pide guardarlos y aborta si el usuario cancela.
/// </summary>
public static class HintTextsWiring
{
    private const string MenuPath = "Tools/Feria de Ciencias/Pistas: cargar textos de 3 niveles";
    private const string ScenesFolder = "Assets/Scenes/";

    /// <summary>Textos de un objetivo, identificado por su label.</summary>
    private sealed class Line
    {
        public readonly string Label;
        public readonly string Soft;
        public readonly string Medium;
        public readonly string Direct;

        public Line(string label, string soft, string medium, string direct)
        {
            Label = label;
            Soft = soft;
            Medium = medium;
            Direct = direct;
        }
    }

    /// <summary>Escena y sus objetivos con textos.</summary>
    private sealed class SceneTexts
    {
        public readonly string FileName;
        public readonly Line[] Lines;

        public SceneTexts(string fileName, params Line[] lines)
        {
            FileName = fileName;
            Lines = lines;
        }
    }

    private static readonly SceneTexts[] Scenes =
    {
        new SceneTexts("Tutorial.unity",
            new Line("Llave",
                "Hay algo brillando en este cuarto... ¿será útil?",
                "Hay una llave en algún lado... tengo que mirarla fijo para agarrarla.",
                "La llave está a la vista. Tengo que mirarla fijo para agarrarla."),
            new Line("Puerta",
                "Esta puerta no se abre sola... ¿qué necesito?",
                "Ya tengo la llave. Ahora la puerta.",
                "Tengo que mirar la puerta de salida para abrirla con la llave.")),

        new SceneTexts("Parte 1 - El Despertar.unity",
            new Line("Linterna",
                "Está muy oscuro... no veo casi nada.",
                "Está muy oscuro... la linterna tiene que estar cerca de la cama.",
                "La linterna está a los pies de la cama. Tengo que mirarla."),
            new Line("Notas",
                "Esas marcas en la pared... ¿son números?",
                "Esas notas en la pared... parecen números. Tengo que mirarlas.",
                "Cero, seis, siete. Eso dicen las notas de la pared."),
            new Line("Candado",
                "Esos números de la pared… ¿para qué serán?",
                "El candado tiene tres discos. Puedo girarlos mirándolos.",
                "Cero, seis, siete. Eso tengo que poner en el candado.")),

        new SceneTexts("Parte 2 - Cumpleaños.unity",
            new Line("Cinta",
                "Algo brilla sobre la mesa... ¿qué será?",
                "Esa cinta sobre la mesa... tengo que agarrarla.",
                "Es una cinta VHS. Tengo que mirarla fijo para agarrarla."),
            new Line("Tele",
                "Ese televisor... ¿para qué servirá?",
                "La tele... ahí tiene que ir la cinta.",
                "Con la cinta en la mano, tengo que mirar la tele para meterla."),
            new Line("Salir",
                "Algo se mueve al fondo... tengo que irme de acá.",
                "¡La puerta! Tengo que salir de acá.",
                "Sigo las flechas brillantes del piso. Me llevan a la puerta.")),

        new SceneTexts("Parte 3 - Pasillo de hotel.unity",
            new Line("Llave",
                "Una luz roja se filtra por debajo de una puerta...",
                "Esa puerta... sale una luz roja por abajo. Tiene que haber algo.",
                "La llave está en la habitación de la luz roja. Tengo que entrar."),
            new Line("Puerta del fondo",
                "Este pasillo no termina nunca... ¿y al fondo?",
                "Ya tengo la llave. La puerta del fondo...",
                "Tengo que ir al fondo del pasillo y abrir la puerta con la llave.")),

        new SceneTexts("Parte 4 - Pasillo de madera.unity",
            new Line("Recorrer",
                "El pasillo sigue y sigue... ¿hay algún final?",
                "Tengo que seguir por el pasillo...",
                "Sigo hacia el fondo. El pasillo siempre me devuelve al mismo lugar."),
            new Line("Cuadro",
                "Algo raro en el fondo... ¿ese cuadro está bien?",
                "Ese cuadro... antes estaba derecho.",
                "El cuadro está al revés. Tengo que mirarlo fijo para enderezarlo."),
            new Line("Pared ciega",
                "Algo no cierra... ¿qué tengo que hacer?",
                "Tengo que volver... de espaldas, sin mirar el cuadro.",
                "Camino de espaldas, alejándome del cuadro, hasta la otra pared.")),

        new SceneTexts("Parte 5 - Backrooms.unity",
            new Line("Fragmentos",
                "Escucho susurros... vienen de algún lado de estas salas.",
                "Hay pedazos de papel... tengo que juntarlos.",
                "Son tres pedazos de la nota. Los susurros salen de ellos."),
            new Line("Entrega",
                "Ya tengo algo... pero ¿qué hago con esto?",
                "Ya los tengo todos. Tengo que llevarlos al centro.",
                "Tengo que mirar el pedestal del centro para dejar la nota.")),
    };

    [MenuItem(MenuPath)]
    public static void LoadHintTexts()
    {
        Scene original = SceneManager.GetActiveScene();
        string originalPath = original.path;

        if (original.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[HintTextsWiring] Cancelado: no se guardaron los cambios de la escena activa.");
            return;
        }

        int totalUpdated = 0;
        try
        {
            foreach (SceneTexts st in Scenes)
            {
                totalUpdated += WireScene(st);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalPath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(originalPath) != null)
            {
                EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[HintTextsWiring] La escena original no tiene path; no se pudo reabrir.");
            }
        }

        Debug.Log($"[HintTextsWiring] Listo: {totalUpdated} objetivos actualizados en {Scenes.Length} escenas.");
    }

    // Devuelve cuantos objetivos se actualizaron en la escena.
    private static int WireScene(SceneTexts st)
    {
        string path = ScenesFolder + st.FileName;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            Debug.LogError($"[HintTextsWiring] No existe la escena '{path}'. Salteada.");
            return 0;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        HintDirector director = FindDirector(scene);
        if (director == null)
        {
            Debug.LogError($"[HintTextsWiring] {st.FileName}: no hay HintDirector en la escena. Salteada.");
            return 0;
        }

        List<HintDirector.Objective> objectives = FeriaWiring.GetObjectives(director);
        var usedLines = new HashSet<Line>();
        var unmatchedObjectives = new List<string>();
        int updated = 0;

        foreach (HintDirector.Objective o in objectives)
        {
            if (o == null) continue;

            Line line = FindLine(st, o.label);
            if (line == null)
            {
                unmatchedObjectives.Add(o.label);
                continue;
            }

            Undo.RecordObject(director, "Pistas: textos de 3 niveles");
            o.voiceTextSoft = line.Soft;
            o.voiceText = line.Medium;
            o.voiceTextDirect = line.Direct;
            EditorUtility.SetDirty(director);

            usedLines.Add(line);
            updated++;
        }

        var missingLines = new List<string>();
        foreach (Line line in st.Lines)
        {
            if (!usedLines.Contains(line)) missingLines.Add(line.Label);
        }

        if (updated > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[HintTextsWiring] {st.FileName}: {updated} de {objectives.Count} objetivos actualizados.");
        foreach (string label in unmatchedObjectives)
            Debug.LogWarning($"[HintTextsWiring] {st.FileName}: el objetivo '{label}' no tiene textos definidos en el script.");
        foreach (string label in missingLines)
            Debug.LogWarning($"[HintTextsWiring] {st.FileName}: no hay objetivo '{label}' en el HintDirector.");

        return updated;
    }

    private static Line FindLine(SceneTexts st, string label)
    {
        if (string.IsNullOrEmpty(label)) return null;

        string key = label.Trim();
        foreach (Line line in st.Lines)
        {
            if (string.Equals(line.Label.Trim(), key, StringComparison.OrdinalIgnoreCase))
                return line;
        }
        return null;
    }

    private static HintDirector FindDirector(Scene scene)
    {
        // Primero por nombre (el objeto que crea FeriaWiring), despues cualquiera de la escena.
        GameObject named = FeriaWiring.Find(FeriaWiring.DirectorName);
        if (named != null)
        {
            HintDirector byName = named.GetComponent<HintDirector>();
            if (byName != null) return byName;
        }

        foreach (HintDirector d in UnityEngine.Object.FindObjectsByType<HintDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (d.gameObject.scene == scene) return d;
        }
        return null;
    }
}
