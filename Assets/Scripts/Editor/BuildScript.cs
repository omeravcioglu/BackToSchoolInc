using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SchoolSim.EditorTools
{
    /// <summary>
    /// Windows builds. Menu: Tools/School Sim/Build Windows Player (a development build, with the
    /// F1 debug view) and Build Windows Release (what players get, with the font licences beside it).
    /// Batch mode: -executeMethod SchoolSim.EditorTools.BuildScript.BuildWindows (or BuildWindowsRelease)
    /// -buildPath &lt;folder&gt;
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Tools/School Sim/Build Windows Player")]
        public static void BuildWindowsMenu()
        {
            string folder = EditorUtility.SaveFolderPanel("Build folder", "", "Build");
            if (!string.IsNullOrEmpty(folder)) Build(folder, release: false);
        }

        [MenuItem("Tools/School Sim/Build Windows Release")]
        public static void BuildWindowsReleaseMenu()
        {
            string folder = EditorUtility.SaveFolderPanel("Release folder", "", "Release");
            if (!string.IsNullOrEmpty(folder)) Build(folder, release: true);
        }

        public static void BuildWindows() => BuildFromCommandLine(release: false);

        public static void BuildWindowsRelease() => BuildFromCommandLine(release: true);

        static void BuildFromCommandLine(bool release)
        {
            string folder = release ? "Release" : "Build";
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-buildPath");
            if (i >= 0 && i + 1 < args.Length) folder = args[i + 1];
            var report = Build(folder, release);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build(string folder, bool release)
        {
            if (release)
            {
                var icons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
                if (icons.Length == 0 || icons[0] == null) ReleaseTools.MakeAppIcon();
            }
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = Path.Combine(folder, "BackToSchoolInc.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = release ? BuildOptions.None : BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded) ReleaseTools.WriteLicenses(folder);
            Debug.Log($"Build {report.summary.result}: {report.summary.outputPath} ({report.summary.totalErrors} errors)");
            return report;
        }
    }
}
