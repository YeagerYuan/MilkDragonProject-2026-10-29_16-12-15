#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UnderAvatar
{
    // Explicitly enabled only by the development-player verification command.
    public sealed class DisplayModeSmokeRunner : MonoBehaviour
    {
        [Serializable]
        sealed class Report
        {
            public bool passed, windowed, fullscreen, returnedToWindow;
            public Vector2Int initial, fullResolution, returnedResolution;
            public string mode;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--ua-display-smoke")<0) return;
            new GameObject("DisplayModeSmokeRunner").AddComponent<DisplayModeSmokeRunner>();
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            var args=Environment.GetCommandLineArgs();
            int reportArg=Array.IndexOf(args,"--ua-report");
            string path=reportArg>=0 && reportArg+1<args.Length?args[reportArg+1]:Path.Combine(Application.persistentDataPath,"display-smoke.json");
            var display=FindFirstObjectByType<DisplayModeSettings>();
            var report=new Report();
            if(display)
            {
                report.initial=display.RenderResolution;
                display.SetFullScreen(false,false);
                yield return new WaitForSecondsRealtime(1);
                report.windowed=!Screen.fullScreen;
                display.SetFullScreen(true,false);
                yield return new WaitForSecondsRealtime(2);
                report.fullscreen=Screen.fullScreen && Screen.fullScreenMode==FullScreenMode.FullScreenWindow;
                report.mode=Screen.fullScreenMode.ToString();
                report.fullResolution=new Vector2Int(Screen.width,Screen.height);
                var app=FindFirstObjectByType<PrototypeApp>();
                if(app) app.OpenSettings();
                yield return new WaitForSecondsRealtime(.5f);
                ScreenCapture.CaptureScreenshot(Path.ChangeExtension(path,"png"));
                yield return new WaitForSecondsRealtime(1);
                display.SetFullScreen(false,false);
                yield return new WaitForSecondsRealtime(1);
                report.returnedToWindow=!Screen.fullScreen;
                report.returnedResolution=new Vector2Int(Screen.width,Screen.height);
                report.passed=report.windowed && report.fullscreen && report.returnedToWindow && report.initial==report.fullResolution && report.initial==report.returnedResolution;
            }
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }
    }
}
#endif
