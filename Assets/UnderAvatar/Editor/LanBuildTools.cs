using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public static class LanBuildTools
    {
        [Serializable] sealed class BuildResult { public string result,path,error;public int errors,warnings;public double seconds; }
        public const string PlayerPath="Builds/UnderAvatarLAN/UnderAvatar.exe";
        [MenuItem("Tools/Under Avatar/Build LAN Windows Player")]
        public static void Build()
        {
            string reportPath="output/UnderAvatar/verification/lan-build.json";
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play。");
                Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{PrototypeSetup.ScenePath},locationPathName=PlayerPath,
                    target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
                });
                var summary=report.summary;
                File.WriteAllText(reportPath,JsonUtility.ToJson(new BuildResult
                {result=summary.result.ToString(),path=Path.GetFullPath(PlayerPath),errors=summary.totalErrors,warnings=summary.totalWarnings,seconds=summary.totalTime.TotalSeconds},true));
                Debug.Log("[UnderAvatar LAN] Build: "+summary.result+" "+PlayerPath);
            }
            catch(Exception e)
            {
                File.WriteAllText(reportPath,JsonUtility.ToJson(new BuildResult{result="Failed",error=e.ToString()},true));
                Debug.LogError(e);
            }
        }
        public static void QueueBuild() { EditorApplication.delayCall+=Build; }
    }
}
