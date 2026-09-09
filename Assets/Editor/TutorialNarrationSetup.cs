using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Agrega (o actualiza) la locucion del Tutorial: un GameObject "Locucion" con
/// AudioSource + SceneNarration en la escena Assets/Scenes/Tutorial.unity, y le
/// asigna el clip de voz si existe.
///
/// Grabá la voz y guardala como Assets/Audios/TutorialLocucion.wav (o .mp3 /
/// .ogg / .flac), y corré este menu. Es additivo e idempotente: si ya esta
/// puesto, solo re-asigna el clip.
/// </summary>
public static class TutorialNarrationSetup
{
    private const string TutorialScenePath = "Assets/Scenes/Tutorial.unity";

    private static readonly string[] VoiceClipCandidates =
    {
        "Assets/Audios/TutorialLocucion.wav",
        "Assets/Audios/TutorialLocucion.mp3",
        "Assets/Audios/TutorialLocucion.ogg",
        "Assets/Audios/TutorialLocucion.flac",
    };

    [MenuItem("Tools/Feria de Ciencias/Agregar locucion al Tutorial")]
    public static void Setup()
    {
        if (!File.Exists(TutorialScenePath))
        {
            EditorUtility.DisplayDialog("Falta la escena",
                "No se encontro " + TutorialScenePath + ".", "Ok");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        AudioClip clip = null;
        string clipPath = null;
        foreach (string p in VoiceClipCandidates)
        {
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (clip != null)
            {
                clipPath = p;
                break;
            }
        }

        Scene scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

        SceneNarration narration = Object.FindFirstObjectByType<SceneNarration>();
        bool created = false;
        if (narration == null)
        {
            GameObject go = new GameObject("Locucion");
            go.AddComponent<AudioSource>();
            narration = go.AddComponent<SceneNarration>();
            created = true;
        }

        if (clip != null)
        {
            SerializedObject so = new SerializedObject(narration);
            SerializedProperty prop = so.FindProperty("_voiceClip");
            if (prop != null)
            {
                prop.objectReferenceValue = clip;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string msg = (created ? "Se creo el objeto 'Locucion' en Tutorial.unity." : "'Locucion' ya existia en Tutorial.unity.");
        msg += clip != null
            ? "\nClip de voz asignado: " + clipPath
            : "\nSIN clip de voz: grabá la voz y guardala como Assets/Audios/TutorialLocucion.wav, despues volve a correr este menu (o arrastrala a mano al campo 'Voice Clip').";
        Debug.Log("<b>[TutorialNarrationSetup]</b> " + msg);
    }
}
