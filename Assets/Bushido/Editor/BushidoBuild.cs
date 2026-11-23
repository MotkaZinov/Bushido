using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Compila la versión jugable para Windows, comprimida para que pese poco.
//   Menú: Bushido/Compilar versión jugable (Windows)
//   Lotes: Unity -batchmode -quit -projectPath <proyecto> -executeMethod BushidoBuild.Windows
// Sale en ../Builds/Bushido-Windows/ (fuera del proyecto, para no meterla en el repositorio).
public static class BushidoBuild
{
    [MenuItem("Bushido/Compilar versión jugable (Windows)")]
    public static void Windows()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/Bushido-Windows"));
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = Path.Combine(folder, "Bushido.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CompressWithLz4HC,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("BUSHIDO_BUILD " + report.summary.result + " " + report.summary.totalSize / (1024 * 1024) + " MB -> " + folder);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
