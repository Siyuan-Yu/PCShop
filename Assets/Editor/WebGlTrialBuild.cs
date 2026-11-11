using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGlTrialBuild
{
    public static void Run()
    {
        int exitCode = 1;
        try
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-pcshop-build-output");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing -pcshop-build-output");
            string output = Path.GetFullPath(args[index + 1]);
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Build output already exists: " + output);

            PlayerSettings.productName = "PCShop 试玩版";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                extraScriptingDefines = new[] { "PCSHOP_FIRST_CHAPTER_TRIAL" }
            });
            Debug.Log($"WEBGL_TRIAL_BUILD_RESULT {report.summary.result} bytes={report.summary.totalSize} output={output}");
            exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
        }
        catch (Exception error) { Debug.LogException(error); }
        EditorApplication.Exit(exitCode);
    }
}
