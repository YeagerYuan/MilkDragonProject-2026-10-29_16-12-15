using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    [Serializable]
    public sealed class MatchSettings
    {
        public float reporterSpeed=2.2f;
        public float streamerSpeed=4.2f;
        public float maxPhotoDistance=8f;
        [Min(1)] public int maxScore=3;
        public float patrolSeconds=30f;
        [Min(1)] public int flappyTargetScore=15;
        public List<BroadcastTarget> broadcastTargets=new List<BroadcastTarget>();
        public FlappyRules flappyRules=new FlappyRules();
        public int TargetForBroadcast(int number)
        {
            var value=broadcastTargets?.LastOrDefault(t=>t!=null && t.broadcast==number);
            return Mathf.Clamp(value?.score??flappyTargetScore,1,100);
        }
        public bool allowEmergencyTurn;
        [Min(0)] public float wrongWardrobePenalty=5;
        [Min(0)] public float wrongBedPenalty=5;
        [Min(1)] public int initialFans=5000;
        [Min(1)] public int lateFanLoss=1000;
        [Min(0)] public float returnGraceSeconds=10;
        [Min(.1f)] public float lateFanLossInterval=10;
        public VisionLightSettings streamerVision=new VisionLightSettings();
        public VisionLightSettings reporterVision=VisionLightSettings.Reporter();
    }

    public sealed class QuestProgress
    {
        public string Id;
        public string Description;
        public string Title;
        public bool Complete;
    }

    public enum NoiseKind { Footstep, Door, Shutter, Chair, ChaseFootstep, DoorClose, HideBed }
    public sealed class SoundPulse
    {
        public Vector3 Position;
        public float Created;
        public NoiseKind Kind;
    }

    [DefaultExecutionOrder(-100)]
    public sealed partial class MatchSession : MonoBehaviour
    {
        public PrototypeApp App { get; private set; }
        public BuildingWorld World => App.World;
        public MatchSettings Settings => App.RuntimeSettings??App.Settings;
        public bool IsNetwork { get; private set; }
        public bool IsTraining { get; private set; }
        public bool IsAuthority { get; private set; }=true;
        public TestRole Role { get; private set; }
        public bool IsStreamerPlayer => Role==TestRole.Streamer;
        public bool InputCaptured { get; private set; }
        public AiReporterDriver ReporterAI { get; private set; }
        public ReporterController Reporter { get; private set; }
        public StreamerActor Streamer { get; private set; }
        public List<QuestProgress> Quests { get; } = new List<QuestProgress>();
        public List<SoundPulse> Pulses { get; } = new List<SoundPulse>();
        public int Score { get; private set; }
        public int StreamerPhotos { get; private set; }
        public bool RequiresLiveEvidence => World.Data.requireLiveEvidence;
        int photographedRound=-1;
        public bool StreamerPhotoAvailable => RequiresLiveEvidence && Streamer.State==StreamerState.Streaming && StreamerPhotos<3 &&
            photographedRound!=Streamer.Controller.BroadcastNumber;
        public string Result { get; private set; }
        public bool Finished => Result!=null;
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public float FlashUntil { get; private set; }
        public bool DebugOpen { get; set; }
        public event Action<SoundPulse> NoiseEmitted;
        public event Action WonOrLost;
        PrototypeAudio audioBank;
        public float WalkStepInterval => audioBank.WalkInterval;
        public float ChaseStepInterval => audioBank.ChaseInterval;
        float nextShutterTime;
        int messageId,noiseId,lastRemoteNoise;
        readonly List<NoiseSnapshot> pendingNoises=new List<NoiseSnapshot>();

        public void Initialize(PrototypeApp app,Vector3 spawn,bool networked=false,bool authority=true,bool training=false)
        {
            App=app;Role=app.Role;IsNetwork=networked;IsAuthority=authority;IsTraining=training;Score=Settings.maxScore;
            InitializeInvestigation();
            var pc=World.Items.Single(i=>i.Data.type==ItemType.StreamerPC);
            var navigation=new BuildingNavigation(World.Data);
            if(navigation.FindPath(spawn,pc.Front)==null)
                throw new InvalidOperationException("出生区域无法抵达主播电脑。请检查门和折返楼梯。");
            audioBank=gameObject.AddComponent<PrototypeAudio>();
            audioBank.Initialize(this,app.AudioProfile);
            var player=new GameObject("Reporter"); player.transform.SetParent(transform,false); player.transform.position=spawn;
            Reporter=player.AddComponent<ReporterController>(); Reporter.Initialize(this);
            // At the right-side (6, 3) spawn, start facing into the room along -X.
            if(!training) Reporter.SetLookAt(Reporter.View.transform.position+Vector3.left);
            var monster=new GameObject("Streamer"); monster.transform.SetParent(transform,false); monster.transform.position=pc.Front;
            Streamer=monster.AddComponent<StreamerActor>(); Streamer.Initialize(this,pc);
            if(IsStreamerPlayer)
            {
                Reporter.View.enabled=false; Reporter.View.GetComponent<AudioListener>().enabled=false;
                Streamer.UsePlayerView();
                Streamer.Controller.Driver=null;
                if(!IsNetwork && !IsTraining) { ReporterAI=player.AddComponent<AiReporterDriver>(); ReporterAI.Initialize(this); }
                if(IsAuthority && !IsTraining) Streamer.Controller.BeginStreaming();
            }
            if(IsNetwork)
            {
                Streamer.Controller.Driver=null;
                if(!IsStreamerPlayer) Streamer.View.enabled=false;
                if(IsAuthority && !IsTraining) Streamer.Controller.BeginStreaming();
                else if(!IsAuthority) { Reporter.GetComponent<CharacterController>().enabled=false;Streamer.GetComponent<CharacterController>().enabled=false; }
            }
            if(IsTraining)
            {
                Streamer.Controller.Driver=null;
                if(!IsStreamerPlayer) Streamer.View.enabled=false;
                gameObject.AddComponent<TrainingHall>().Initialize(this);
                Notice("教学大厅：自由交互，没有小游戏、计时或胜负。Tab 打开准备菜单。",8);return;
            }
            if(IsNetwork || !IsStreamerPlayer) Reporter.SetPhone(true);
            Notice(IsStreamerPlayer?"空格 / 左键拍翅。Flappy Bird 达到 "+Settings.TargetForBroadcast(1)+" 分后可下播搜捕狗仔。":
                RequiresLiveEvidence?"完成物证任务和 3 张直播证据；每场直播只能取证主播一次。F 手机，Tab 调试。":"完成手机中配置的拍摄任务。F 手机，Tab 调试。",6);
        }

        public void Notice(string text,float seconds=3) { Message=text; MessageUntil=Time.time+seconds;messageId++; App.Report(text); }
        public void Finish(string result)
        {
            if(Finished || IsTraining) return;
            Result=result; WonOrLost?.Invoke();
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }

        public void SetMaxScore(int value)
        {
            value=Mathf.Clamp(value,1,999);
            int spent=Settings.maxScore-Score;
            Settings.maxScore=value;
            Score=Mathf.Max(0,value-spent);
            if(Score==0) Finish("主播获胜：狗仔任务失败，失误过多");
        }

        public void EmitNoise(Vector3 position,NoiseKind kind)
        {
            var pulse=new SoundPulse{Position=position,Created=Time.time,Kind=kind};
            Pulses.Add(pulse); NoiseEmitted?.Invoke(pulse); audioBank.Play(position,kind);
            if(IsNetwork && IsAuthority) pendingNoises.Add(new NoiseSnapshot{id=++noiseId,position=position,kind=kind});
        }
        public void PlayLocalFootstep(Vector3 position)
            => PlayWorldSound(position,NoiseKind.ChaseFootstep);
        public void PlayWorldSound(Vector3 position,NoiseKind kind)
        {
            audioBank.Play(position,kind);
            if(IsNetwork && IsAuthority) pendingNoises.Add(new NoiseSnapshot{id=++noiseId,position=position,kind=kind,audibleOnly=true});
        }

        public bool IsPhotographable(Camera camera,Vector3 point,Collider targetCollider=null,Transform targetRoot=null)
        {
            if(Vector3.Distance(camera.transform.position,point)>=Settings.maxPhotoDistance) return false;
            Vector3 screen=camera.WorldToViewportPoint(point);
            if(screen.z<=0 || (IsTraining
                ?screen.x<.22f || screen.x>.64f || screen.y<.23f || screen.y>.77f
                :screen.x<.025f || screen.x>.975f || screen.y<.165f || screen.y>.95f)) return false;
            Vector3 ray=point-camera.transform.position;
            foreach(var hit in Physics.RaycastAll(camera.transform.position,ray.normalized,ray.magnitude-.025f,
                PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore))
                if(hit.collider!=targetCollider && (!targetRoot || !hit.collider.transform.IsChildOf(targetRoot))) return false;
            return true;
        }

        public List<QuestProgress> ValidQuests(Camera camera)
        {
            var valid=new List<QuestProgress>();
            foreach(var task in InvestigationData.Tasks(World.Data))
            {
                var quest=Quests.Find(q=>q.Id==task.id && !q.Complete);
                if(quest!=null && TaskInPhoto(task))
                {
                    var prop=TaskWorld?.Props.Find(p=>p.Data.id==task.targetId);
                    if(task.kind==InvestigationKind.Photograph || prop && TaskWorld.HeldId!=prop.Data.id && task.Contains(prop.transform.position)) valid.Add(quest);
                }
            }
            return valid;
        }

        public bool StreamerInFrame(Camera camera) => Streamer && Streamer.State==StreamerState.Streaming &&
            IsPhotographable(camera,Streamer.PhotoPoint);
        public bool HasValidTarget => !Reporter.Hidden && (ValidQuests(Reporter.View).Count>0 ||
            StreamerPhotoAvailable && StreamerInFrame(Reporter.View));

        public void TakePhoto()
        {
            if(DocumentManager.BlocksGameplayInput || !IsAuthority || Finished || Reporter.Hidden || !Reporter.PhoneOpen || TaskWorld?.Held || Time.time<nextShutterTime) return;
            nextShutterTime=Time.time+.25f;
            EmitNoise(Reporter.transform.position,NoiseKind.Shutter);
            FlashUntil=Time.time+.3f;
            var lightObject=new GameObject("ShutterFlash");
            lightObject.transform.SetParent(transform,false); lightObject.transform.position=Reporter.View.transform.position;
            var flash=lightObject.AddComponent<Light>(); flash.type=LightType.Point; flash.color=Color.white; flash.range=5; flash.intensity=8;
            Destroy(lightObject,.3f);
            if(IsTraining) { Notice("拍照练习完成。大厅照片不上传、不计分。");return; }
            CaptureEvidence();
        }
        public void SimulateNetwork(PlayerCommand reporter,PlayerCommand streamer,float dt)
        {
            if(!IsNetwork || !IsAuthority || Finished || reporter==null || streamer==null || !reporter.Valid || !streamer.Valid) return;
            Reporter.ApplyCommand(reporter,dt);
            Streamer.Controller.TickNetwork(streamer,dt);
        }
        public void SimulateNetworkActor(TestRole role,PlayerCommand input,float dt)
        {
            if(!IsNetwork || !IsAuthority || Finished || input==null || !input.Valid) return;
            if(role==TestRole.Reporter) Reporter.ApplyCommand(input,dt);
            else Streamer.Controller.TickNetwork(input,dt);
        }
        public MatchSnapshot CaptureSnapshot()
        {
            var brain=Streamer.Controller;
            var snapshot=new MatchSnapshot
            {
                serverTime=Time.unscaledTime,
                taskProps=TaskWorld?.Capture(),heldProp=TaskWorld?.HeldId,selectedTask=SelectedTaskId,pendingPhoto=PendingPhoto,lastSubmittedPhoto=LastSubmittedPhoto,
                reporter=Reporter.CapturePose(),streamer=brain.CapturePose(),state=Streamer.State,broadcast=brain.BroadcastNumber,
                progress=Streamer.Progress,roamRemaining=brain.RoamRemaining,stateRemaining=brain.StateRemaining,
                score=Score,photos=StreamerPhotos,photographedRound=photographedRound,result=Result,message=Message,messageId=messageId,
                fans=brain.Fans,returnElapsed=brain.ReturnElapsed,
                messageRemaining=Mathf.Max(0,MessageUntil-Time.time),alertRemaining=Mathf.Max(0,Streamer.AlertUntil-Time.time),
                flashRemaining=Mathf.Max(0,FlashUntil-Time.time),flappy=brain.Level.Capture(),
                completedQuests=Quests.Where(q=>q.Complete).Select(q=>q.Id).ToArray(),
                doors=World.Items.Where(i=>i.Data.type==ItemType.Door).Select(i=>new DoorSnapshot{id=i.Data.id,open=i.IsOpen}).ToArray(),
                hidingPlaces=World.Items.Where(i=>i.IsHidingPlace).Select(i=>new DoorSnapshot{id=i.Data.id,open=i.IsOpen}).ToArray(),
                noises=pendingNoises.ToArray()
            };
            pendingNoises.Clear();return snapshot;
        }
        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            if(IsAuthority || !IsNetwork || snapshot==null) return;
            foreach(var door in snapshot.doors)
            { var item=World.Find(door.id);if(item && item.IsOpen!=door.open) item.SetDoorOpen(door.open); }
            if(snapshot.hidingPlaces!=null) foreach(var place in snapshot.hidingPlaces)
            { var item=World.Find(place.id);if(item && item.IsOpen!=place.open) item.SetSearchOpen(place.open); }
            Reporter.ApplyPose(snapshot.reporter,snapshot.serverTime);Streamer.Controller.ApplySnapshot(snapshot);
            TaskWorld?.Apply(snapshot.taskProps,snapshot.heldProp,snapshot.inputAck);
            bool newPhoto=snapshot.pendingPhoto!=null && snapshot.pendingPhoto.id!=(PendingPhoto?.id??0);
            PendingPhoto=snapshot.pendingPhoto;SelectedTaskId=snapshot.selectedTask;LastSubmittedPhoto=snapshot.lastSubmittedPhoto;
            if(newPhoto && !IsStreamerPlayer) SetPhoneMenu(true);
            if(!Reporter.PhoneOpen && !IsStreamerPlayer) SetPhoneMenu(false);
            Score=snapshot.score;StreamerPhotos=snapshot.photos;photographedRound=snapshot.photographedRound;
            foreach(var quest in Quests) quest.Complete=Array.IndexOf(snapshot.completedQuests,quest.Id)>=0;
            Streamer.ApplyAlert(snapshot.alertRemaining);FlashUntil=Time.time+snapshot.flashRemaining;
            if(snapshot.messageId!=messageId)
            { messageId=snapshot.messageId;Message=snapshot.message;MessageUntil=Time.time+snapshot.messageRemaining; }
            foreach(var noise in snapshot.noises)
            {
                if(noise.id<=lastRemoteNoise) continue;
                lastRemoteNoise=noise.id;
                if(noise.audibleOnly) audioBank.Play(noise.position,noise.kind);
                else EmitNoise(noise.position,noise.kind);
            }
            if(!string.IsNullOrEmpty(snapshot.result) && !Finished) Finish(snapshot.result);
        }

        void Update()
        {
            UpdatePhone();
            Pulses.RemoveAll(p=>Time.time-p.Created>2.8f);
            InputCaptured=false;
            if(DocumentManager.BlocksGameplayInput) return;
            if(!IsTraining && !IsStreamerPlayer && !Reporter.Hidden && !Finished && !DebugOpen && Keyboard.current?.qKey.wasPressedThisFrame==true)
            {
                if(CameraMode) QueuePhoneAction(close:true);else EnterPhotoMode();
                return;
            }
            if(PhoneMenuOpen && Reporter.PhoneOpen)
            {
                var phoneKeys=Keyboard.current;
                if(PendingPhoto==null && (phoneKeys?.escapeKey.wasPressedThisFrame==true || phoneKeys?.fKey.wasPressedThisFrame==true)) QueuePhoneAction(close:true);
                if(IsTraining && phoneKeys?.qKey.wasPressedThisFrame==true) SetPhoneMenu(false);
                return;
            }
            if(IsTraining && Reporter.PhoneOpen && !IsStreamerPlayer && Keyboard.current?.qKey.wasPressedThisFrame==true) {SetPhoneMenu(true);return;}
            var keys=Keyboard.current;
            if(keys==null) return;
            if(keys.escapeKey.wasPressedThisFrame)
            {
                if(DebugOpen) { DebugOpen=false; Reporter.SetCursor(false); }
                else App.ExitPlay();
                return;
            }
            if(Finished) return;
            if(keys.tabKey.wasPressedThisFrame) { DebugOpen=!DebugOpen; Reporter.SetCursor(DebugOpen); return; }
            if(DebugOpen) return;
            if(Cursor.lockState!=CursorLockMode.Locked)
            {
                if(Mouse.current?.leftButton.wasPressedThisFrame==true) Reporter.SetCursor(false);
                return;
            }
            InputCaptured=true;
        }
    }
}
