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
        public int maxScore=3;
        public float patrolSeconds=30f;
        [Min(0)] public int flappyTargetScore=15;
        public bool allowEmergencyTurn;
    }

    public sealed class QuestProgress
    {
        public string Id;
        public string Description;
        public bool Complete;
    }

    public enum NoiseKind { Footstep, Door, Shutter, Chair }
    public sealed class SoundPulse
    {
        public Vector3 Position;
        public float Created;
        public NoiseKind Kind;
    }

    [DefaultExecutionOrder(-100)]
    public sealed class MatchSession : MonoBehaviour
    {
        public PrototypeApp App { get; private set; }
        public BuildingWorld World => App.World;
        public MatchSettings Settings => App.RuntimeSettings??App.Settings;
        public bool IsNetwork { get; private set; }
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
        int photographedRound=-1;
        public bool StreamerPhotoAvailable => Streamer.State==StreamerState.Streaming && StreamerPhotos<3 &&
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
        float nextShutterTime;
        int messageId,noiseId,lastRemoteNoise;
        readonly List<NoiseSnapshot> pendingNoises=new List<NoiseSnapshot>();

        public void Initialize(PrototypeApp app,Vector3 spawn,bool networked=false,bool authority=true)
        {
            App=app;Role=app.Role;IsNetwork=networked;IsAuthority=authority;Score=Settings.maxScore;
            foreach(var item in World.Items.Where(i=>i.Data.type==ItemType.QuestItem))
                Quests.Add(new QuestProgress{Id=item.Data.questId,Description=item.Data.questDescription});
            var pc=World.Items.Single(i=>i.Data.type==ItemType.StreamerPC);
            var navigation=new BuildingNavigation(World.Data);
            if(navigation.FindPath(spawn,pc.Front)==null)
                throw new InvalidOperationException("出生区域无法抵达主播电脑。请检查门和折返楼梯。");
            audioBank=gameObject.AddComponent<PrototypeAudio>();
            var player=new GameObject("Reporter"); player.transform.SetParent(transform,false); player.transform.position=spawn;
            Reporter=player.AddComponent<ReporterController>(); Reporter.Initialize(this);
            var monster=new GameObject("Streamer"); monster.transform.SetParent(transform,false); monster.transform.position=pc.Front;
            Streamer=monster.AddComponent<StreamerActor>(); Streamer.Initialize(this,pc);
            if(IsStreamerPlayer)
            {
                Reporter.View.enabled=false; Reporter.View.GetComponent<AudioListener>().enabled=false;
                Streamer.UsePlayerView();
                Streamer.Controller.Driver=null;
                if(!IsNetwork) { ReporterAI=player.AddComponent<AiReporterDriver>(); ReporterAI.Initialize(this); }
                if(IsAuthority) Streamer.Controller.BeginStreaming();
            }
            if(IsNetwork)
            {
                Streamer.Controller.Driver=null;
                if(!IsStreamerPlayer) Streamer.View.enabled=false;
                if(IsAuthority) Streamer.Controller.BeginStreaming();
                else { Reporter.GetComponent<CharacterController>().enabled=false;Streamer.GetComponent<CharacterController>().enabled=false; }
            }
            Notice(IsStreamerPlayer?"空格 / 左键拍翅。Flappy Bird 达到 "+Settings.flappyTargetScore+" 分后可下播搜捕 AI 狗仔。":
                "完成物证任务和 3 张直播证据；每场直播只能取证主播一次。F 手机，Tab 调试。",6);
        }

        public void Notice(string text,float seconds=3) { Message=text; MessageUntil=Time.time+seconds;messageId++; App.Report(text); }
        public void Finish(string result)
        {
            if(Finished) return;
            Result=result; WonOrLost?.Invoke();
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }

        public void SetMaxScore(int value)
        {
            value=Mathf.Clamp(value,1,10);
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
        {
            audioBank.Play(position,NoiseKind.Footstep);
            if(IsNetwork && IsAuthority) pendingNoises.Add(new NoiseSnapshot{id=++noiseId,position=position,kind=NoiseKind.Footstep,audibleOnly=true});
        }

        public bool IsPhotographable(Camera camera,Vector3 point,Collider targetCollider=null)
        {
            if(Vector3.Distance(camera.transform.position,point)>=Settings.maxPhotoDistance) return false;
            Vector3 screen=camera.WorldToViewportPoint(point);
            if(screen.z<=0 || screen.x<.22f || screen.x>.64f || screen.y<.23f || screen.y>.77f) return false;
            Vector3 ray=point-camera.transform.position;
            foreach(var hit in Physics.RaycastAll(camera.transform.position,ray.normalized,ray.magnitude-.025f,
                PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore))
                if(hit.collider!=targetCollider) return false;
            return true;
        }

        public List<QuestProgress> ValidQuests(Camera camera)
        {
            var valid=new List<QuestProgress>();
            foreach(var item in World.Items)
            {
                if(item.Data.type!=ItemType.QuestItem) continue;
                var quest=Quests.Find(q=>q.Id==item.Data.questId && !q.Complete);
                if(quest!=null && IsPhotographable(camera,item.PhotoPoint)) valid.Add(quest);
            }
            return valid;
        }

        public bool StreamerInFrame(Camera camera) => Streamer && Streamer.State==StreamerState.Streaming &&
            IsPhotographable(camera,Streamer.PhotoPoint);
        public bool HasValidTarget => !Reporter.Hidden && (ValidQuests(Reporter.View).Count>0 ||
            StreamerPhotoAvailable && StreamerInFrame(Reporter.View));

        public void TakePhoto()
        {
            if(!IsAuthority || Finished || Reporter.Hidden || !Reporter.PhoneOpen || Time.time<nextShutterTime) return;
            nextShutterTime=Time.time+.25f;
            EmitNoise(Reporter.transform.position,NoiseKind.Shutter);
            FlashUntil=Time.time+.3f;
            var lightObject=new GameObject("ShutterFlash");
            lightObject.transform.SetParent(transform,false); lightObject.transform.position=Reporter.View.transform.position;
            var flash=lightObject.AddComponent<Light>(); flash.type=LightType.Point; flash.color=Color.white; flash.range=5; flash.intensity=8;
            Destroy(lightObject,.3f);
            var valid=ValidQuests(Reporter.View);
            bool seesStreamer=StreamerInFrame(Reporter.View);
            if(seesStreamer) Streamer.OnPhotographed();
            bool newStreamer=seesStreamer && StreamerPhotoAvailable;
            if(valid.Count==0 && !newStreamer)
            {
                Score--;
                Notice(seesStreamer && !StreamerPhotoAvailable ? "本场直播已拍过主播，请等待下一场；重复上传，容错 -1。" : "错误上传：画面中没有新的有效证据，容错 -1。");
                if(Score<=0) Finish("主播获胜：狗仔任务失败，失误过多");
                return;
            }
            foreach(var quest in valid) quest.Complete=true;
            if(newStreamer) { StreamerPhotos++; photographedRound=Streamer.Controller.BroadcastNumber; }
            audioBank.Success();
            Notice((IsStreamerPlayer?"狗仔上传了新证据：":"上传成功：")+(valid.Count>0?valid.Count+" 件物证 ":"")+(newStreamer?"直播证据 "+StreamerPhotos+"/3":""));
            if(Quests.All(q=>q.Complete) && StreamerPhotos==3) Finish("狗仔获胜：全部证据已上传");
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
                reporter=Reporter.CapturePose(),streamer=brain.CapturePose(),state=Streamer.State,broadcast=brain.BroadcastNumber,
                progress=Streamer.Progress,roamRemaining=brain.RoamRemaining,stateRemaining=brain.StateRemaining,
                score=Score,photos=StreamerPhotos,photographedRound=photographedRound,result=Result,message=Message,messageId=messageId,
                messageRemaining=Mathf.Max(0,MessageUntil-Time.time),alertRemaining=Mathf.Max(0,Streamer.AlertUntil-Time.time),
                flashRemaining=Mathf.Max(0,FlashUntil-Time.time),flappy=brain.Level.Capture(),
                completedQuests=Quests.Where(q=>q.Complete).Select(q=>q.Id).ToArray(),
                doors=World.Items.Where(i=>i.Data.type==ItemType.Door).Select(i=>new DoorSnapshot{id=i.Data.id,open=i.IsOpen}).ToArray(),
                noises=pendingNoises.ToArray()
            };
            pendingNoises.Clear();return snapshot;
        }
        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            if(IsAuthority || !IsNetwork || snapshot==null) return;
            foreach(var door in snapshot.doors)
            { var item=World.Find(door.id);if(item && item.IsOpen!=door.open) item.SetDoorOpen(door.open); }
            Reporter.ApplyPose(snapshot.reporter);Streamer.Controller.ApplySnapshot(snapshot);
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
            Pulses.RemoveAll(p=>Time.time-p.Created>2.8f);
            InputCaptured=false;
            // The document owns ESC and pointer clicks while reading and on its closing frame.
            if (DocumentManager.BlocksGameplayInput) return;
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
