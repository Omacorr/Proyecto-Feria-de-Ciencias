using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Compila el APK release (Builds/RecuerdosRotos.apk) con las escenas tildadas en Build Settings.
/// - A mano: menú Tools/Feria de Ciencias/Compilar APK release.
/// - Automático: si existe Builds/build-request.txt cuando el Editor recarga scripts, lo borra y compila
///   (sirve para pedir un build sin Unity MCP y sin batch mode con el Editor abierto).
/// El resultado queda en Builds/build-status.txt.
/// </summary>
[InitializeOnLoad]
public static class ReleaseBuilder
{
    const string RequestFile = "Builds/build-request.txt";
    const string StatusFile = "Builds/build-status.txt";
    const string OutputPath = "Builds/RecuerdosRotos.apk";

    static ReleaseBuilder()
    {
        if (!File.Exists(RequestFile)) return;
        EditorApplication.delayCall += TryAutoBuild;
    }

    static void TryAutoBuild()
    {
        if (!File.Exists(RequestFile)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            File.WriteAllText(StatusFile, "Waiting | el Editor está en Play o compilando; salí de Play y volvé a pedir el build");
            return;
        }
        File.Delete(RequestFile);
        Build();
    }

    [MenuItem("Tools/Feria de Ciencias/Compilar APK release")]
    public static void Build()
    {
        Directory.CreateDirectory("Builds");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        File.WriteAllText(StatusFile, "Building | " + System.DateTime.Now + " | " + scenes.Length + " escenas");
        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        var rep = BuildPipeline.BuildPlayer(opts);
        var s = rep.summary;
        File.WriteAllText(StatusFile, s.result + " | " + s.totalTime + " | " + (s.totalSize / 1048576) + " MB | errores " + s.totalErrors + " | " + System.DateTime.Now);
        Debug.Log("[ReleaseBuilder] " + s.result + " -> " + OutputPath);
    }
}
