using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// External Content stays editable next to the executable, matching CardDatabaseService.
public sealed class MvpPlayerBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
            report.summary.platform != BuildTarget.StandaloneLinux64) return;
        string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Content"));
        string destination = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "Content");
        foreach (var sourceFile in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = sourceFile.Substring(source.Length + 1);
            if (relative.EndsWith(".meta")) continue;
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(sourceFile, target, true);
        }
    }
    [MenuItem("Mishi/Networking/Build Windows Test Player")]
    private static void BuildTestPlayer()
    {
        string output = EditorUtility.SaveFilePanel("Build networking test", "Builds", "Mishi", "exe");
        if (string.IsNullOrEmpty(output)) return;
        BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] {
            "Assets/Scenes/MainMenu.unity", "Assets/Scenes/CardTestGym.unity", "Assets/Scenes/DeckBuilder.unity" },
            locationPathName = output, target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development });
    }
}
