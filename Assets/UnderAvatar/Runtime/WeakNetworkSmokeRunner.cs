#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // Explicit opt-in, two-process validation of owner input during delayed snapshots.
    public sealed class WeakNetworkSmokeRunner : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public bool passed,host,connected,hall,localLook,localMove,menuKeptMatch,menuBlockedInput,recovered;
            public string role,error;
            public int maxPending,lookFrames,samples;
            public float maxSnapshotAge,minFps=999,rtt;
        }
        public string[] EditorArguments;
        PrototypeApp app;
        string[] args;
        string path;
        Report report=new Report();
        float started,hallStarted=-1;
        bool menuDone,observing;
        Vector3 previous,menuPosition;
        float previousYaw;
        string Arg(string key,string fallback="")
        { int index=Array.IndexOf(args,key);return index>=0 && index+1<args.Length?args[index+1]:fallback; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            if(Environment.GetCommandLineArgs().Contains("--ua-weak-smoke"))
                new GameObject("Weak network verification").AddComponent<WeakNetworkSmokeRunner>();
        }
        void Start()
        {
            args=EditorArguments??Environment.GetCommandLineArgs();
            app=UnityEngine.Object.FindFirstObjectByType<PrototypeApp>();
            path=Arg("--ua-report");report.host=Arg("--ua-weak-smoke")=="host";started=Time.realtimeSinceStartup;
            int port=int.Parse(Arg("--ua-port","17896"));
            app.Lan.TestReceiveDelay=.3f;
            if(report.host) app.Lan.CreateRoom(Arg("--ua-role")=="reporter"?TestRole.Reporter:TestRole.Streamer,port);
            else app.Lan.JoinRoom("127.0.0.1",port);
        }
        Transform Owner => app.Session.IsStreamerPlayer?app.Session.Streamer.transform:app.Session.Reporter.transform;
        void Update()
        {
            try
            {
                float now=Time.realtimeSinceStartup;
                if(hallStarted<0?now-started>120:now-hallStarted>35) {End("Timed out: "+app.Lan.Status);return;}
                if(!app.Lan.Active) {End(report.host && hallStarted>=0 && now-hallStarted>17?null:"Disconnected: "+app.Lan.Status);return;}
                if(app.ComicOpen) app.Comic.Finish();
                if(app.Lan.Phase==LanPhase.Lobby)
                {
                    if(app.Lan.PeerJoined && !app.Lan.LocalReady) app.Lan.SetReady(true);
                    if(app.Lan.CanStart) app.Lan.StartMatch();
                    return;
                }
                if(app.Lan.Phase!=LanPhase.Training) return;
                if(hallStarted<0)
                {
                    hallStarted=now;report.connected=report.hall=true;report.role=app.Role.ToString();
                    app.Lan.CommandProvider=Command;
                    // Start outside the shared tutorial props so the movement check is independent of layout.
                    if(report.host)
                    {
                        app.Session.Reporter.Teleport(new Vector3(-4,0,-4));
                        var streamer=app.Session.Streamer;var body=streamer.GetComponent<CharacterController>();
                        body.enabled=false;streamer.transform.position=new Vector3(4,0,-4);body.enabled=true;
                    }
                }
                float elapsed=now-hallStarted;
                if(!report.host) app.Lan.TestReceiveDelay=elapsed>=3 && elapsed<7?6f:.3f;
                if(elapsed>=1.5f && !menuDone)
                { menuDone=true;menuPosition=Owner.position;app.OpenSettings();report.menuKeptMatch=app.Session && app.SettingsOpen && Time.timeScale==1; }
                if(app.SettingsOpen && elapsed>2)
                { report.menuBlockedInput=Vector3.Distance(menuPosition,Owner.position)<.08f;app.CloseSettings(); }
                report.maxPending=Mathf.Max(report.maxPending,app.Lan.PendingInputs);
                report.maxSnapshotAge=Mathf.Max(report.maxSnapshotAge,app.Lan.SnapshotAge);
                if(elapsed>1) report.minFps=Mathf.Min(report.minFps,app.Lan.LocalFps);
                if(elapsed>14 && app.Lan.SnapshotAge<1.5f && app.Lan.PendingInputs<120) report.recovered=true;
                if(elapsed>(report.host?23:18)) End(null);
            }
            catch(Exception error) {End(error.ToString());}
        }
        PlayerCommand Command()
        {
            previous=Owner.position;previousYaw=Owner.eulerAngles.y;
            observing=!app.GameplayInputBlocked;
            // Alternating motion stays in a small clear area, while turning is checked every render frame.
            float t=Time.realtimeSinceStartup-hallStarted;
            return new PlayerCommand{x=Mathf.Sin(t*3)>.0f?.45f:-.45f,lookX=1};
        }
        void LateUpdate()
        {
            if(!observing || !app.Session || app.Lan.Phase!=LanPhase.Training) return;
            observing=false;report.samples++;
            float delta=Mathf.Abs(Mathf.DeltaAngle(previousYaw,Owner.eulerAngles.y));
            if(delta>.05f && delta<.3f) report.lookFrames++;
            report.localLook=report.samples>60 && report.lookFrames>report.samples*.75f;
            report.localMove|=Vector3.Distance(previous,Owner.position)>.002f;
        }
        void End(string error)
        {
            if(!enabled) return;enabled=false;
            report.error=error;report.rtt=app.Lan.RoundTripMs;
            report.passed=error==null && report.connected && report.hall && report.localLook && report.localMove && report.menuKeptMatch && report.menuBlockedInput &&
                (report.host || report.maxPending>300 && report.maxSnapshotAge>4 && report.recovered);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllText(path,JsonUtility.ToJson(report,true));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit(report.passed?0:1);
#endif
        }
    }
}
#endif
