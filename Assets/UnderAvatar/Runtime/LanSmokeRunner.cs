#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // Opt-in executable smoke harness. It sends ordinary player commands through the LAN connection.
    // World placements below only arrange test fixtures on the authority; they are never network RPCs.
    public sealed class LanSmokeRunner : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public bool passed,moved,crouched,doorOpened,photo,heardNoise,completedFifteen,captured,rematch,noAI,cameraCorrect;
            public string side,role,map,result,error;
            public int frames;
            public float roundTripMs;
        }
        PrototypeApp app;
        Report report=new Report();
        string path;
        bool host,preparedDoor,preparedPhoto,phoneSent,interactSent,shotSent,inMatch;
        int rounds;
        float started,roundStarted,finishedAt=-1,readyToExit=-1;
        Vector3 initialReporter;
        Vector3 lockedStreamer;
        string[] args;
        TestRole requestedRole;
#if UNITY_EDITOR
        public string[] EditorArguments;
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Environment.GetCommandLineArgs().Contains("--ua-smoke"))
                new GameObject("LAN executable verification").AddComponent<LanSmokeRunner>();
        }
        string Arg(string key,string fallback="")
        {
            int index=Array.IndexOf(args,key);return index>=0&&index+1<args.Length?args[index+1]:fallback;
        }
        void Start()
        {
            args=Environment.GetCommandLineArgs();
#if UNITY_EDITOR
            if(EditorArguments!=null) args=EditorArguments;
#endif
            host=Arg("--ua-smoke")=="host";
            requestedRole=Arg("--ua-role")=="streamer"?TestRole.Streamer:TestRole.Reporter;
            path=Arg("--ua-report");started=Time.realtimeSinceStartup;
            app=UnityEngine.Object.FindFirstObjectByType<PrototypeApp>();
            report.side=host?"host":"guest";
            if(!app) { End("PrototypeApp missing");return; }
            app.Lan.TestReceiveDelay=float.Parse(Arg("--ua-latency-ms","0"))/1000;
            if(host)
            {
                var data=app.Building?app.Building.CreateRuntimeCopy():BuildingData.CreateDefault();
                data.name="LAN smoke "+Arg("--ua-port");
                var asset=ScriptableObject.CreateInstance<BuildingAsset>();asset.SetData(data);app.Building=asset;
                app.Lan.CreateRoom(requestedRole,int.Parse(Arg("--ua-port","17881")));
            }
            else app.Lan.JoinRoom("127.0.0.1",int.Parse(Arg("--ua-port","17881")));
        }
        void Update()
        {
            if(!app) return;
            try
            {
                float now=Time.realtimeSinceStartup;
                if(readyToExit>=0)
                {
                    if(now>=readyToExit) End(null);
                    return;
                }
                if(now-started>100) { End("Timed out: "+app.Lan.Status);return; }
                if(!app.Lan.Active) { End("Disconnected: "+app.Lan.Status);return; }
                if(app.Lan.Phase==LanPhase.Lobby)
                {
                    inMatch=false;
                    if(app.Lan.PeerJoined&&!app.Lan.LocalReady) app.Lan.SetReady(true);
                    if(app.Lan.CanStart) app.Lan.StartMatch();
                }
                if(app.Lan.Phase!=LanPhase.Playing || !app.Session) return;
                var match=app.Session;
                if(!inMatch)
                {
                    inMatch=true;rounds++;roundStarted=now;
                    app.Lan.CommandProvider=Command;
                    initialReporter=match.Reporter.transform.position;lockedStreamer=match.Streamer.transform.position;
                    report.role=match.Role.ToString();report.map=match.World.Data.name;
                    if(rounds==2)
                    {
                        report.rematch=!match.Finished && match.StreamerPhotos==0 && match.Streamer.Controller.Level.Score==0 &&
                            match.Streamer.State==StreamerState.Streaming;
                        readyToExit=now+1;return;
                    }
                }
                report.noAI=match.ReporterAI==null && match.Streamer.Controller.Driver==null;
                report.cameraCorrect=match.Role==TestRole.Reporter?match.Reporter.View.enabled&&!match.Streamer.View.enabled:
                    !match.Reporter.View.enabled&&match.Streamer.View.enabled&&match.Streamer.View.targetTexture==null;
                float elapsed=now-roundStarted;
                if(elapsed<2.5f)
                {
                    report.moved|=Vector3.Distance(match.Reporter.transform.position,initialReporter)>.25f;
                    report.crouched|=match.Reporter.Crouched;
                    if(Vector3.Distance(match.Streamer.transform.position,lockedStreamer)>.15f)
                        throw new InvalidOperationException("Streaming player moved before completing Flappy Bird.");
                }
                if(host && elapsed>3 && !preparedDoor)
                {
                    var door=match.World.Items.First(i=>i.Floor==0&&i.Data.type==ItemType.Door);
                    match.Reporter.Teleport(door.transform.position-door.transform.forward*1.4f);
                    match.Reporter.SetLookAt(door.transform.position+Vector3.up*1.2f);
                    preparedDoor=true;
                }
                report.doorOpened|=match.World.Items.Any(i=>i.Data.type==ItemType.Door&&i.IsOpen);
                if(host && elapsed>4.7f && !preparedPhoto)
                {
                    match.Reporter.Teleport(match.Streamer.transform.position-match.Streamer.transform.forward*2);
                    match.Reporter.SetLookAt(match.Streamer.PhotoPoint);preparedPhoto=true;
                }
                report.photo|=match.StreamerPhotos==1;
                if(match.StreamerPhotos>1) throw new InvalidOperationException("More than one photo in a broadcast.");
                report.heardNoise|=match.Pulses.Any(p=>p.Kind==NoiseKind.Door||p.Kind==NoiseKind.Shutter);
                report.completedFifteen|=match.Streamer.Controller.Level.Score==15;
                if(match.Finished)
                {
                    report.result=match.Result;report.captured=match.Result.Contains("主播获胜");
                    if(finishedAt<0) finishedAt=now;
                    if(host && now-finishedAt>1) app.Lan.ReturnToLobby();
                }
            }
            catch(Exception e) { End(e.ToString()); }
        }
        PlayerCommand Command()
        {
            var command=new PlayerCommand();
            if(rounds!=1||!app.Session||app.Session.Finished) return command;
            var match=app.Session;float elapsed=Time.realtimeSinceStartup-roundStarted;
            if(match.Role==TestRole.Reporter)
            {
                if(elapsed<1.1f) command.x=1;
                else if(elapsed<2.1f) {command.crouch=true;command.sneak=true;command.x=-.5f;}
                if(elapsed>3.5f && !interactSent) {command.interact=true;interactSent=true;}
                if(elapsed>5.2f && !phoneSent) {command.phone=true;phoneSent=true;}
                if(elapsed>5.7f && !shotSent) {command.action=true;shotSent=true;}
            }
            else
            {
                var brain=match.Streamer.Controller;
                if(match.Streamer.State==StreamerState.Streaming)
                {
                    if(elapsed<2) {command.y=1;command.lookX=5;}
                    if(elapsed>6) command.action=brain.Level.WantsAutoFlap;
                }
                else if(match.Streamer.State==StreamerState.Roaming)
                {
                    Vector3 local=match.Streamer.transform.InverseTransformDirection(match.Reporter.transform.position-match.Streamer.transform.position);
                    Vector2 direction=new Vector2(local.x,local.z).normalized;command.x=direction.x;command.y=direction.y;
                }
            }
            return command;
        }
        void End(string error)
        {
            enabled=false;report.error=error;
            report.frames=app?app.Lan.ReceivedFrames:0;
            report.roundTripMs=app?app.Lan.RoundTripMs:0;
            report.passed=string.IsNullOrEmpty(error)&&report.moved&&report.crouched&&report.doorOpened&&report.photo&&report.heardNoise&&
                report.completedFifteen&&report.captured&&report.rematch&&report.noAI&&report.cameraCorrect;
            if(!string.IsNullOrEmpty(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path,JsonUtility.ToJson(report,true));
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit(report.passed?0:1);
#endif
        }
    }
}
#endif
