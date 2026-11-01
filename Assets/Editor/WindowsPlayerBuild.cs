using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WindowsPlayerBuild
{
    public static void Run()
    {
        try
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-pcshop-build-output");
            if(index<0||index+1>=args.Length)throw new ArgumentException("Missing -pcshop-build-output");
            string output=Path.GetFullPath(args[index+1]);
            if(File.Exists(output))throw new IOException("Refusing to overwrite an existing player: "+output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.defaultScreenWidth=1920;
            PlayerSettings.defaultScreenHeight=1080;
            PlayerSettings.defaultIsNativeResolution=false;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName=output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.CompressWithLz4HC
            });
            Debug.Log("WINDOWS_BUILD_RESULT "+report.summary.result+" bytes="+report.summary.totalSize+" output="+output);
            EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
