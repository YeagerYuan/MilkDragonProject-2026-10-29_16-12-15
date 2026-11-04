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
            public bool characterModels,reporterBonesAnimated,remoteReporterAnimated;
            public int highestFlappyScore,flappyTarget,flappyCrashes;
            public float lowestFps=999;
            public float highestAnimationSpeed,largestLegAngle;
            public int animationParameters;
            public bool hallVisited,hallNoGameplay,hallDoorSynced,hallHiddenSynced,freshMatch,darknessRestored;
            public bool taskCarryPicked,taskCarryPlaced;
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
        Transform reporterLeg;
        Quaternion initialLegRotation;
        bool inHall,hallDoorInput,hallHideInput,hallExitInput,hallPositioned;
        bool carryPrepared,dropPrepared,carryInput,dropInput;
        bool carryComplete;
        float hallStarted;
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
                // A deterministic difficulty fixture for the input bot, independent of authored level tuning.
                // This runner only exists with the explicit --ua-smoke argument; no settings asset is saved.
                app.Settings.flappyRules=new FlappyRules();app.Settings.flappyTargetScore=15;app.Settings.broadcastTargets.Clear();
                var data=app.Building?app.Building.CreateRuntimeCopy():BuildingData.CreateDefault();
                data.name="LAN smoke "+Arg("--ua-port");
                if(args.Contains("--ua-task-smoke")) data.taskProps.Add(new TaskPropData{id="lan_carry_fixture",position=new Vector3(-4,.03f,-4)});
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
                    inMatch=false;inHall=false;
                    if(app.Lan.PeerJoined&&!app.Lan.LocalReady) app.Lan.SetReady(true);
                    if(app.Lan.CanStart) app.Lan.StartMatch();
                }
                if(app.Lan.Phase==LanPhase.Training && app.Session) { CheckHall(now);return; }
                if(app.Lan.Phase!=LanPhase.Playing || !app.Session) return;
                var match=app.Session;
                if(!inMatch)
                {
                    inMatch=true;rounds++;roundStarted=now;
                    app.Lan.CommandProvider=Command;
                    initialReporter=match.Reporter.transform.position;lockedStreamer=match.Streamer.transform.position;
                    report.role=match.Role.ToString();report.map=match.World.Data.name;
                    report.freshMatch=!match.IsTraining && !match.Reporter.Hidden && match.Score==match.Settings.maxScore && match.StreamerPhotos==0 &&
                        match.Quests.All(q=>!q.Complete) && match.World.Items.Where(i=>i.Data.type==ItemType.Door).All(i=>!i.IsOpen) && match.Streamer.Controller.Fans==match.Settings.initialFans;
                    report.darknessRestored=RenderSettings.ambientIntensity==0 && match.Reporter.Vision.enabled && match.Streamer.Vision.enabled;
                    if(rounds==2)
                    {
                        report.rematch=!match.Finished && match.StreamerPhotos==0 && match.Streamer.Controller.Level.Score==0 &&
                            match.Streamer.State==StreamerState.Streaming;
                        readyToExit=now+1;return;
                    }
                }
                if(args.Contains("--ua-task-smoke") && rounds==1 && !carryComplete) {CheckCarry(now);return;}
                report.noAI=match.ReporterAI==null && match.Streamer.Controller.Driver==null;
                report.cameraCorrect=match.Role==TestRole.Reporter?match.Reporter.View.enabled&&!match.Streamer.View.enabled:
                    !match.Reporter.View.enabled&&match.Streamer.View.enabled&&match.Streamer.View.targetTexture==null;
                float elapsed=now-roundStarted;
                var appearance=match.Reporter.Appearance;
                report.highestFlappyScore=Mathf.Max(report.highestFlappyScore,match.Streamer.Controller.Level.Score);
                report.flappyTarget=match.Streamer.Controller.Level.TargetScore;report.flappyCrashes=match.Streamer.Controller.Level.Crashes;
                if(elapsed>1) report.lowestFps=Mathf.Min(report.lowestFps,app.Lan.LocalFps);
                report.characterModels=appearance.GetComponentInChildren<SkinnedMeshRenderer>() && match.Streamer.Appearance.GetComponentInChildren<Renderer>();
                if(!reporterLeg)
                {
                    reporterLeg=appearance.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="mixamorig:LeftUpLeg");
                    if(reporterLeg) initialLegRotation=reporterLeg.localRotation;
                }
                if(appearance.Animator)
                {
                    report.animationParameters=appearance.Animator.parameterCount;
                    report.highestAnimationSpeed=Mathf.Max(report.highestAnimationSpeed,appearance.Animator.GetFloat("Speed"));
                    if(reporterLeg) report.largestLegAngle=Mathf.Max(report.largestLegAngle,Quaternion.Angle(initialLegRotation,reporterLeg.localRotation));
                }
                if(reporterLeg && appearance.Animator && appearance.Animator.GetFloat("Speed")>.2f && Quaternion.Angle(initialLegRotation,reporterLeg.localRotation)>8)
                { report.reporterBonesAnimated=true;if(match.Role==TestRole.Streamer && appearance.Remote) report.remoteReporterAnimated=true; }
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
        void CheckCarry(float now)
        {
            var match=app.Session;var fixture=match.TaskWorld.Props.Find(p=>p.Data.id=="lan_carry_fixture");
            if(!fixture) throw new InvalidOperationException("Carry fixture missing from synchronized map.");
            if(host && !carryPrepared)
            {match.Reporter.Teleport(new Vector3(-4,0,-6));match.Reporter.SetLookAt(fixture.PhotoPoint);Physics.SyncTransforms();carryPrepared=true;}
            report.taskCarryPicked|=match.TaskWorld.HeldId==fixture.Data.id;
            if(host && report.taskCarryPicked && !dropPrepared)
            {match.Reporter.Teleport(new Vector3(-4,0,-6));match.Reporter.SetLookAt(new Vector3(-2,0,-5));dropPrepared=true;}
            report.taskCarryPlaced|=report.taskCarryPicked && match.TaskWorld.HeldId==null && Vector3.Distance(fixture.transform.position,new Vector3(-2,.015f,-5))<.2f;
            if(report.taskCarryPlaced) {carryComplete=true;roundStarted=now;return;}
            if(now-roundStarted>12) throw new InvalidOperationException("Carry confirmation timed out: picked="+report.taskCarryPicked+", held="+match.TaskWorld.HeldId+", aim="+(match.TaskWorld.AimProp()?match.TaskWorld.AimProp().name:"none"));
        }
        PlayerCommand Command()
        {
            var command=new PlayerCommand();
            if(rounds!=1||!app.Session||app.Session.Finished) return command;
            var match=app.Session;float elapsed=Time.realtimeSinceStartup-roundStarted;
            if(args.Contains("--ua-task-smoke") && !carryComplete)
            {
                if(match.Role!=TestRole.Reporter) return command;
                if(match.Reporter.PhoneOpen && !match.TaskWorld.Held) {command.phone=true;return command;}
                if(!carryInput && match.TaskWorld.AimProp()!=null && !match.TaskWorld.Held)
                {command.interact=true;carryInput=true;}
                if(report.taskCarryPicked && !dropInput && match.TaskWorld.Held && match.TaskWorld.TryPlacement(out var bottom,out var yaw) && Vector3.Distance(bottom,new Vector3(-2,.015f,-5))<.2f)
                {command.interact=true;dropInput=true;}
                return command;
            }
            if(match.Role==TestRole.Reporter)
            {
                if(elapsed<1.1f) command.x=1;
                else if(elapsed<2.1f) {command.crouch=true;command.sneak=true;command.x=-.5f;}
                if(elapsed>3.5f && !interactSent) {command.interact=true;interactSent=true;}
                if(elapsed>5.2f && !phoneSent) {command.phone=!match.Reporter.PhoneOpen;phoneSent=true;}
                if(elapsed>5.7f && !shotSent) {command.taskId=InvestigationData.LiveTask;command.action=true;shotSent=true;}
                if(match.PendingPhoto!=null) command.uploadPhoto=match.PendingPhoto.id;
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
        void CheckHall(float now)
        {
            var match=app.Session;
            if(!inHall)
            {
                inHall=true;hallStarted=now;hallDoorInput=hallHideInput=hallExitInput=hallPositioned=false;
                app.Lan.CommandProvider=HallCommand;report.hallVisited=match.IsTraining;
                if(host)
                {
                    var door=match.World.Items.First(i=>i.Data.type==ItemType.Door);
                    match.Reporter.Teleport(door.Front);match.Reporter.SetLookAt(door.transform.position+Vector3.up*1.2f);
                }
            }
            report.hallNoGameplay=match.IsTraining && !match.Finished && match.Streamer.State==StreamerState.Roaming && match.Streamer.Controller.Level.TargetScore==0 &&
                !match.Streamer.Controller.Level.Running && match.StreamerPhotos==0 && match.Score==match.Settings.maxScore && RenderSettings.ambientIntensity>.5f;
            if(!report.hallNoGameplay) throw new InvalidOperationException("Hall started gameplay or lost its normal lighting.");
            report.hallDoorSynced|=match.World.Items.Any(i=>i.Data.type==ItemType.Door && i.IsOpen);
            report.hallHiddenSynced|=match.Reporter.Hidden;
            float elapsed=now-hallStarted;
            if(host && elapsed>1.3f && !hallPositioned)
            {
                var wardrobe=match.World.Items.First(i=>i.IsHidingPlace && i.Data.type==ItemType.Furniture);
                match.Reporter.Teleport(wardrobe.Front);match.Reporter.SetLookAt(wardrobe.transform.position+Vector3.up*1.2f);hallPositioned=true;
            }
            if(elapsed>3.3f && !app.Lan.LocalReady) app.Lan.SetReady(true);
            if(elapsed>3.5f && app.Lan.CanStart) app.Lan.StartMatch();
        }
        PlayerCommand HallCommand()
        {
            var c=new PlayerCommand();float elapsed=Time.realtimeSinceStartup-hallStarted;
            if(app.Session.Role==TestRole.Streamer) { c.interact=elapsed>.5f && elapsed<.6f;return c; }
            if(elapsed>.5f && !hallDoorInput) {hallDoorInput=true;c.interact=true;}
            if(elapsed>1.9f && !hallHideInput) {hallHideInput=true;c.interact=true;}
            if(elapsed>2.7f && !hallExitInput) {hallExitInput=true;c.interact=true;}
            return c;
        }
        void End(string error)
        {
            enabled=false;report.error=error;
            report.frames=app?app.Lan.ReceivedFrames:0;
            report.roundTripMs=app?app.Lan.RoundTripMs:0;
            report.passed=string.IsNullOrEmpty(error)&&report.moved&&report.crouched&&report.doorOpened&&report.photo&&report.heardNoise&&
                report.completedFifteen&&report.captured&&report.rematch&&report.noAI&&report.cameraCorrect&&report.characterModels&&
                (report.role!="Streamer" || report.reporterBonesAnimated && report.remoteReporterAnimated)&&report.hallVisited&&report.hallNoGameplay&&
                report.hallDoorSynced&&report.hallHiddenSynced&&report.freshMatch&&report.darknessRestored && (!args.Contains("--ua-task-smoke") || report.taskCarryPicked&&report.taskCarryPlaced);
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
