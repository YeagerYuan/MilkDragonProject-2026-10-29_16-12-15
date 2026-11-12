#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // Opt-in validation only: drive real protocol inputs with a delayed peer.
    public sealed class AbilityNetworkSmokeRunner : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public bool passed,host,hall,invisible,oneCharge,revealed,laughing,energyUsed,correctViewer,bodyHidden,recovered;
            public bool hallPhoto,hallUpload,hallSkill,hallModel;
            public bool phoneMap,noMapBoards,death,respawn,protectedPhotoBlocked,threeLivesEnded;
            public string role,error;
            public float rtt;
        }
        public string[] EditorArguments;
        string[] args;
        string path;
        PrototypeApp app;
        Report report=new Report();
        float started,matchStarted=-1,hallStarted=-1;
        bool requested,shot,hallInvisible,hallPhone,hallPhoto,hallUpload,mapRequested,mapShot,deathShot;
        int captured;
        bool RevisionFive => args.Contains("--ua-revision5-check");
        string Arg(string key,string fallback="") {int i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length?args[i+1]:fallback;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            if(Environment.GetCommandLineArgs().Contains("--ua-ability-smoke"))
                new GameObject("Ability network verification").AddComponent<AbilityNetworkSmokeRunner>();
        }
        void Start()
        {
            args=EditorArguments??Environment.GetCommandLineArgs();
            app=UnityEngine.Object.FindFirstObjectByType<PrototypeApp>();path=Arg("--ua-report");
            report.host=Arg("--ua-ability-smoke")=="host";started=Time.realtimeSinceStartup;
            app.Lan.TestReceiveDelay=.12f;
            int port=int.Parse(Arg("--ua-port","17897"));
            if(report.host) app.Lan.CreateRoom(Arg("--ua-role")=="reporter"?TestRole.Reporter:TestRole.Streamer,port);
            else app.Lan.JoinRoom("127.0.0.1",port);
        }
        void Update()
        {
            try
            {
                float now=Time.realtimeSinceStartup;
                if(now-started>120) {End("Timed out: "+app.Lan.Status);return;}
                if(!app.Lan.Active) {End(report.host && matchStarted>=0 && now-matchStarted>12?null:"Disconnected: "+app.Lan.Status);return;}
                if(app.ComicOpen) app.Comic.Finish();
                if(app.Lan.Phase==LanPhase.Training && args.Contains("--ua-training-check"))
                {
                    var hall=app.Session;
                    if(hallStarted<0) {hallStarted=now;app.Lan.CommandProvider=Command;}
                    float t=now-hallStarted;
                    report.hall=true;report.hallPhoto|=hall.PendingPhoto!=null;
                    report.hallUpload|=hall.UploadFeedbackId>0 && hall.UploadFeedbackSuccess;
                    report.hallSkill|=hall.Abilities.Invisible && hall.Abilities.Revealed;
                    report.hallModel=hall.Streamer.Appearance.Model.name.Contains("MysteryHallStreamer");
                    if(!hall.IsStreamerPlayer && t>2.5f && t<3) hall.SetPhoneMenu(false);
                    if(!hall.IsStreamerPlayer && t>5 && hall.PendingPhoto!=null && !hallUpload)
                    {hallUpload=true;hall.QueuePhoneAction(upload:hall.PendingPhoto.id);}
                    if(RevisionFive)
                    {
                        report.noMapBoards=hall.GetComponentsInChildren<HouseMapBoard>().Length==0;
                        if(!hall.IsStreamerPlayer && t>6.8f && !mapRequested) {mapRequested=true;hall.OpenPhonePage(PhonePage.Map);}
                        if(!hall.IsStreamerPlayer && t>8 && !mapShot)
                        {mapShot=true;report.phoneMap=hall.HouseMap.IsOpen;ScreenCapture.CaptureScreenshot(Path.ChangeExtension(path,null)+"-phone-map.png");}
                        if(hall.IsStreamerPlayer) report.phoneMap=true;
                    }
                    if(t<13) return;
                }
                if(app.Lan.Phase==LanPhase.Lobby || app.Lan.Phase==LanPhase.Training)
                {
                    report.hall|=app.Lan.Phase==LanPhase.Training;
                    if(app.Lan.PeerJoined && !app.Lan.LocalReady) app.Lan.SetReady(true);
                    if(app.Lan.CanStart) app.Lan.StartMatch();
                    return;
                }
                if(app.Lan.Phase!=LanPhase.Playing) return;
                var match=app.Session;var ability=match.Abilities;
                if(matchStarted<0)
                {
                    matchStarted=now;report.role=app.Role.ToString();app.Lan.CommandProvider=Command;
                    var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Verification ground";ground.transform.SetParent(match.transform);
                    ground.transform.position=new Vector3(100,-.5f,103);ground.transform.localScale=new Vector3(20,1,20);
                    ground.GetComponent<Renderer>().sharedMaterial=match.World.Palette.Material(new Color(.25f,.25f,.25f));
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Verification occluder";wall.transform.SetParent(match.transform);
                    wall.layer=PrototypeLayers.Get("UA_Wall");wall.transform.position=new Vector3(100,1.5f,103);wall.transform.localScale=new Vector3(8,4,.2f);
                    wall.GetComponent<Renderer>().sharedMaterial=match.World.Palette.Material(new Color(.35f,.35f,.35f));
                    if(report.host)
                    {
                        var body=match.Streamer.GetComponent<CharacterController>();body.enabled=false;
                        match.Streamer.transform.SetPositionAndRotation(new Vector3(100,.05f,100),Quaternion.identity);body.enabled=true;
                        match.Streamer.State=StreamerState.Roaming;
                        match.Reporter.Teleport(new Vector3(100,.05f,106));match.Reporter.SetLookAt(new Vector3(100,1.7f,100));
                    }
                    Physics.SyncTransforms();
                }
                float elapsed=now-matchStarted;
                report.invisible|=ability.Invisible;report.oneCharge|=ability.InvisibleCharges==1;
                report.revealed|=ability.Invisible && ability.Revealed;report.laughing|=ability.Laughing;report.energyUsed|=ability.LaughEnergy<4.5f;
                var outline=match.Streamer.View.GetComponent<DetectionOutline>();
                report.correctViewer=match.IsStreamerPlayer?outline!=null:outline==null;
                if(ability.Invisible && elapsed>2) report.bodyHidden|=match.Reporter.Appearance.Model.GetComponentsInChildren<Renderer>().All(r=>!r.enabled);
                if(elapsed>4 && ability.Invisible && ability.Revealed && !shot)
                {shot=true;ScreenCapture.CaptureScreenshot(Path.ChangeExtension(path,"png"));}
                if(elapsed>12) report.recovered|=!ability.Invisible && ability.LaughEnergy>4.5f;
                if(RevisionFive)
                {
                    if(report.host && captured<3 && elapsed>18+captured*8)
                    {
                        match.Reporter.Teleport(match.Streamer.transform.position+match.Streamer.transform.forward*1.1f);
                        if(match.CaptureReporter()) captured++;
                    }
                    report.death|=match.ReporterDying && match.ReporterLives==2;
                    if(match.ReporterProtected)
                    {
                        report.respawn|=Vector3.Distance(match.Reporter.transform.position,match.ReporterSpawn)<.25f;
                        report.protectedPhotoBlocked|=match.PendingPhoto==null && !match.ReporterDying;
                    }
                    if(match.ReporterDying && elapsed>19 && !deathShot)
                    {deathShot=true;ScreenCapture.CaptureScreenshot(Path.ChangeExtension(path,null)+"-death.png");}
                    report.threeLivesEnded|=match.Finished && match.ReporterLives==0 && match.Result.Contains("主播获胜");
                    if(elapsed>(report.host?41:39)) End(null);
                }
                else if(elapsed>(report.host?17:15)) End(null);
            }
            catch(Exception error) {End(error.ToString());}
        }
        PlayerCommand Command()
        {
            if(app.Lan.Phase==LanPhase.Training)
            {
                float h=Time.realtimeSinceStartup-hallStarted;bool reporter=app.Role==TestRole.Reporter;
                bool invis=reporter && h>1 && !hallInvisible;if(invis) hallInvisible=true;
                bool phone=reporter && h>2 && !hallPhone;if(phone) hallPhone=true;
                bool photo=reporter && h>3 && !hallPhoto;if(photo) hallPhoto=true;
                return new PlayerCommand{invisible=invis,phone=phone,action=photo,laugh=!reporter && h>3.5f && h<5};
            }
            float t=Time.realtimeSinceStartup-matchStarted;
            bool edge=app.Role==TestRole.Reporter && t>1 && !requested;
            if(edge) requested=true;
            return new PlayerCommand{invisible=edge,laugh=app.Role==TestRole.Streamer && t>3 && t<5,
                y=RevisionFive && app.Role==TestRole.Reporter && t>17 && t<18.2f?1:0,
                action=RevisionFive && app.Role==TestRole.Reporter && app.Session.ReporterProtected};
        }
        void End(string error)
        {
            if(!enabled) return;enabled=false;report.error=error;report.rtt=app.Lan.RoundTripMs;
            report.passed=error==null && report.hall && report.invisible && report.oneCharge && report.revealed && report.laughing && report.energyUsed && report.correctViewer && report.bodyHidden && report.recovered;
            if(args.Contains("--ua-training-check")) report.passed&=report.hallPhoto && report.hallUpload && report.hallSkill && report.hallModel;
            if(RevisionFive) report.passed&=report.phoneMap && report.noMapBoards && report.death && report.respawn && report.protectedPhotoBlocked && report.threeLivesEnded;
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
