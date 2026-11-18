using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MacPlayerBuild
{
    public static void Run()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-pcshop-build-output");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("缺少 -pcshop-build-output");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Mac 输出必须以 .app 结尾");
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("输出已存在，不覆盖：" + output);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new BuildFailedException("尚未安装 Mac Build Support (Mono)");

            // The platform extension is loaded by Unity after installing Mac Build Support.
            var settings = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings"))
                .FirstOrDefault(t => t != null);
            var architecture = settings?.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
            if (architecture == null) throw new BuildFailedException("无法读取 Mac 架构设置");
            architecture.SetValue(null, Enum.Parse(architecture.PropertyType, "x64ARM64"));
            Debug.Log("MAC_BUILD_ARCHITECTURE " + architecture.GetValue(null));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.productName = "PETSHOP";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            CityMapBaker.SelectEdition(false);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.CompressWithLz4HC
            });
            Debug.Log("MAC_BUILD_RESULT " + report.summary.result + " bytes=" + report.summary.totalSize + " output=" + output);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
