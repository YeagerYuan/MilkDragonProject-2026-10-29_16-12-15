using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    public interface IStreamerDriver
    {
        void Tick(StreamerController controller,float deltaTime);
        void Hear(SoundPulse pulse);
    }

    public sealed class StreamerController : MonoBehaviour
    {
        public StreamerActor Actor { get; private set; }
        public MatchSession Session => Actor.Session;
        public BuildingNavigation Navigation { get; private set; }
        public MinigameLevel Level { get; private set; }
        public IStreamerDriver Driver { get; set; }
        public float RoamRemaining { get; private set; }
        public float StateRemaining { get; private set; }
        public bool IsHuman => Session.IsStreamerPlayer || Session.IsNetwork;
        public int BroadcastNumber => round;
        public bool Crouched => posture.Crouched;
        public bool HasPath => path!=null && pathIndex<path.Count;
        public IReadOnlyList<Vector3Int> PatrolCells => patrolCells;
        List<Vector3Int> path;
        List<Vector3Int> patrolCells;
        int pathIndex,round=1,stairIndex;
        List<Vector3> stairRoute;
        Vector3? pendingDestination;
        CharacterController body;
        CharacterPosture posture;
        float vertical,pitch,nextFootstep;
        ActorSnapshot replica;
        readonly CharacterMotion motion=new CharacterMotion();
        bool sneaking;
        readonly HashSet<string> searchedEmpty=new HashSet<string>();
        public int Fans { get; private set; }
        public float ReturnElapsed { get; private set; }
        int lateDeductions;
        public float NextFanLossIn => Mathf.Max(0,Session.Settings.returnGraceSeconds+(lateDeductions+1)*Mathf.Max(.1f,Session.Settings.lateFanLossInterval)-ReturnElapsed);
        public bool OnStairs => stairRoute!=null;

        public void Initialize(StreamerActor actor)
        {
            Actor=actor; Navigation=new BuildingNavigation(Session.World.Data);
            Fans=Mathf.Max(1,Session.Settings.initialFans);
            body=gameObject.AddComponent<CharacterController>();
            body.height=1.8f; body.center=Vector3.up*.9f; body.radius=.28f; body.stepOffset=.3f; body.slopeLimit=45;
            body.skinWidth=.025f;
            posture=new CharacterPosture(body,Actor.View.transform,null,1.7f);
            patrolCells=Navigation.ReachableFrom(transform.position).ToList();
            Level=new MinigameLevel(Environment.TickCount);
            Level.Configure(Session.Settings.flappyRules);
            Actor.State=StreamerState.Roaming; RoamRemaining=Session.Settings.patrolSeconds;
            Driver=new AiStreamerDriver(); Session.NoiseEmitted+=OnNoise;
        }

        void OnNoise(SoundPulse pulse) { Driver?.Hear(pulse); }
        public bool SetDestination(Vector3 target)
        {
            if(stairRoute!=null) { pendingDestination=target; return true; }
            var next=Navigation.FindPath(transform.position,target);
            if(next==null) return false;
            path=next; pathIndex=0;
            if(path.Count>1 && Vector3.Distance(transform.position,Navigation.Position(path[0]))<.15f) pathIndex=1;
            return true;
        }

        public void BeginReturn()
        {
            if(!Session.IsAuthority || Session.Finished || Session.IsTraining) return;
            if(Actor.State==StreamerState.Returning) return;
            if(Actor.State==StreamerState.Streaming && !Level.Complete) return;
            Actor.State=StreamerState.Returning; Actor.Progress=0;
            ReturnElapsed=0;lateDeductions=0;
            if(IsHuman) { path=null; Session.Notice("搜捕时间结束，请回到电脑前按 E 开始下一场直播。",6); return; }
            if(!SetDestination(Actor.Computer.Front)) Session.Notice("主播无法抵达电脑，请检查门和楼梯连接。",6);
        }

        public void BeginStreaming()
        {
            if(Session.IsTraining) { if(Session.IsAuthority) Session.Notice("电脑交互练习完成；大厅不启动小游戏。");return; }
            if(!Session.IsAuthority || Session.Finished) return;
            if(Actor.State==StreamerState.Streaming && Level.TargetScore>0) return;
            path=null; stairRoute=null; pendingDestination=null;
            motion.Reset();sneaking=false;
            body.enabled=false; transform.position=Actor.Computer.Front; body.enabled=true; vertical=0;
            transform.rotation=Actor.Computer.transform.rotation;
            posture.SetCrouched(false); pitch=0; Actor.View.transform.localRotation=Quaternion.identity;
            Actor.State=StreamerState.Streaming;
            ReturnElapsed=0;lateDeductions=0;searchedEmpty.Clear();
            Level.Configure(Session.Settings.flappyRules);
            Level.Start(round,Session.Settings.TargetForBroadcast(round)); Actor.Progress=0;
            Session.Notice("主播开始第 "+round+" 关直播。注意手机上的通关进度。");
        }

        public void Flap()
        {
            if(Session.IsAuthority && Actor.State==StreamerState.Streaming && !Session.Finished) Level.Flap();
        }
        public void AdvanceMinigame(float dt)
        {
            if(!Session.IsAuthority || Actor.State!=StreamerState.Streaming || Session.Finished || Session.IsTraining) return;
            Level.Tick(dt); Actor.Progress=Level.Progress;
            if(Level.Complete)
            {
                Actor.State=StreamerState.Roaming; RoamRemaining=Session.Settings.patrolSeconds;
                round++; path=null;
                Session.Notice("主播已经通关，恢复搜捕！",3);
            }
        }

        public bool TryEmergencyTurn()
        {
            if(IsHuman || !Session.Settings.allowEmergencyTurn || Actor.State!=StreamerState.Streaming || Session.Finished) return false;
            Actor.State=StreamerState.Turning; StateRemaining=.6f; path=null;
            Session.EmitNoise(transform.position,NoiseKind.Chair);
            return true;
        }

        public bool CanSeeReporter()
        {
            var reporter=Session.Reporter;
            if(reporter.Hidden || reporter.Floor!=Actor.Floor) return false;
            Vector3 delta=reporter.View.transform.position-Actor.View.transform.position;
            return delta.magnitude<6.5f && Vector3.Angle(transform.forward,delta)<50 &&
                !Physics.Linecast(Actor.View.transform.position,reporter.View.transform.position,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore);
        }

        public bool TryCapture()
        {
            if(Session.IsTraining) return false;
            if(!Session.IsAuthority || Session.Finished || Session.Reporter.Hidden || Actor.State!=StreamerState.Roaming && Actor.State!=StreamerState.Ambush) return false;
            if(Vector3.Distance(transform.position,Session.Reporter.transform.position)>=1.3f) return false;
            if(Physics.Linecast(Actor.View.transform.position,Session.Reporter.View.transform.position,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore)) return false;
            Session.Finish("主播获胜：狗仔被灭口"); return true;
        }

        public bool SearchHidingPlace(PlacedItemView place)
        {
            if(!Session.IsAuthority || Session.Finished || Actor.State!=StreamerState.Roaming || !place || !place.IsHidingPlace || place.IsOpen) return false;
            if(Vector3.Distance(Actor.View.transform.position,place.transform.position+Vector3.up*.6f)>3.5f) return false;
            Vector3 point=place.transform.position+Vector3.up*.6f;
            if(Physics.Linecast(Actor.View.transform.position,point,out var hit,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<PlacedItemView>()!=place) return false;
            place.SetSearchOpen(true);
            Session.PlayWorldSound(place.transform.position,place.Data.type==ItemType.Bed?NoiseKind.HideBed:NoiseKind.Door);
            if(Session.IsTraining) { Session.Notice("翻查练习完成："+place.DisplayName+"。大厅不抓捕、不扣分。");return false; }
            if(Session.Reporter.Hidden && Session.Reporter.HidingPlaceId==place.Data.id)
            { Session.Finish("主播获胜：在"+place.DisplayName+"中发现了狗仔");return true; }
            if(searchedEmpty.Add(place.Data.id))
            {
                float penalty=Mathf.Max(0,place.Data.type==ItemType.Bed?Session.Settings.wrongBedPenalty:Session.Settings.wrongWardrobePenalty);
                RoamRemaining=Mathf.Max(0,RoamRemaining-penalty);
                Session.Notice("搜错了"+place.DisplayName+"，搜捕时间减少 "+penalty+" 秒。");
                if(RoamRemaining<=0) BeginReturn();
            }
            else Session.Notice("这里是空的，本轮已扣过搜捕时间。");
            return false;
        }

        public void AdvanceReturnDeadline(float dt)
        {
            if(!Session.IsAuthority || Session.Finished || Actor.State!=StreamerState.Returning || Session.IsTraining) return;
            ReturnElapsed+=Mathf.Max(0,dt);
            float interval=Mathf.Max(.1f,Session.Settings.lateFanLossInterval);
            int due=Mathf.Max(0,Mathf.FloorToInt((ReturnElapsed-Mathf.Max(0,Session.Settings.returnGraceSeconds))/interval));
            if(due<=lateDeductions) return;
            int loss=(due-lateDeductions)*Mathf.Max(1,Session.Settings.lateFanLoss);lateDeductions=due;
            Fans=Mathf.Max(0,Fans-loss);
            if(Fans==0) Session.Finish("狗仔获胜：主播逾期开播，粉丝已经流失完了");
            else Session.Notice("开播逾期！粉丝减少 "+loss+"，当前粉丝 "+Fans+"。请尽快回电脑开播。");
        }

        void Update()
        {
            if(Session.IsNetwork)
            {
                if(!Session.IsAuthority && !Session.IsStreamerPlayer && replica!=null)
                {
                    float blend=1-Mathf.Exp(-35*Time.deltaTime);
                    transform.position=Vector3.Lerp(transform.position,replica.position,blend);
                    transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(0,replica.yaw,0),blend);
                    pitch=Mathf.LerpAngle(pitch,replica.pitch,blend);Actor.View.transform.localRotation=Quaternion.Euler(pitch,0,0);
                }
                return;
            }
            Tick(Time.deltaTime,null);
        }
        public void TickNetwork(PlayerCommand command,float dt)
        { if(Session.IsAuthority) Tick(dt,command); }
        void Tick(float dt,PlayerCommand command)
        {
            if(!Actor || Session.Finished) return;
            AdvanceReturnDeadline(dt);
            if(Session.Finished) return;
            if(body.isGrounded && vertical<0) vertical=-2;
            vertical+=Physics.gravity.y*dt;
            body.Move(Vector3.up*vertical*dt);
            if(Actor.State==StreamerState.Roaming && !Session.IsTraining)
            { RoamRemaining-=dt; if(RoamRemaining<=0) BeginReturn(); }
            else if(Actor.State==StreamerState.Turning)
            {
                transform.Rotate(0,180/.6f*dt,0); StateRemaining-=dt;
                if(StateRemaining<=0) { Actor.State=StreamerState.Ambush; StateRemaining=3; }
            }
            else if(Actor.State==StreamerState.Ambush)
            {
                StateRemaining-=dt;
                if(StateRemaining<=0)
                {
                    Actor.State=StreamerState.Stunned; StateRemaining=3; path=null;
                    Level.Reset(); Actor.Progress=0;
                    Session.Notice("主播回头扑空：进度清零，并眩晕 3 秒。",3);
                }
            }
            else if(Actor.State==StreamerState.Stunned)
            { StateRemaining-=dt; if(StateRemaining<=0) BeginReturn(); }
            if(command!=null) ApplyCommand(command,dt);
            else if(IsHuman) HumanInput(dt); else Driver?.Tick(this,dt);
            if(Session.IsNetwork || !IsHuman || Session.InputCaptured) AdvanceMinigame(dt);
            if(!IsHuman && (Actor.State==StreamerState.Roaming || Actor.State==StreamerState.Returning || Actor.State==StreamerState.Ambush)) Move(dt);
            if(!IsHuman && Actor.State==StreamerState.Returning && !HasPath && Vector3.Distance(transform.position,Actor.Computer.Front)<2)
                BeginStreaming();
            TryCapture();
        }
        void ApplyCommand(PlayerCommand command,float dt)
        {
            if(Actor.State==StreamerState.Streaming) { if(command.action) Flap();return; }
            if(Actor.State!=StreamerState.Roaming && Actor.State!=StreamerState.Returning) return;
            LookInput(new Vector2(command.lookX,command.lookY));posture.SetCrouched(command.crouch);
            MoveInput(new Vector2(command.x,command.y),command.sneak,dt);
            if(command.interact) Interact();
        }
        public ActorSnapshot CapturePose() => new ActorSnapshot
        { position=transform.position,yaw=transform.eulerAngles.y,pitch=pitch,vertical=vertical,crouched=Crouched,sneaking=sneaking,planarVelocity=motion.Velocity };
        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            var value=snapshot.streamer;
            body.enabled=false;
            if(Session.IsStreamerPlayer)
            {
                transform.position=value.position;transform.rotation=Quaternion.Euler(0,value.yaw,0);
                pitch=value.pitch;vertical=value.vertical;Actor.View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            }
            if(replica==null || Vector3.Distance(transform.position,value.position)>3) transform.position=value.position;
            replica=value;posture.SetCrouched(value.crouched,true);
            Actor.Appearance.Receive(value,snapshot.serverTime);
            Actor.State=snapshot.state;Actor.Progress=snapshot.progress;round=snapshot.broadcast;
            Fans=snapshot.fans;ReturnElapsed=snapshot.returnElapsed;
            lateDeductions=Mathf.Max(0,Mathf.FloorToInt((ReturnElapsed-Mathf.Max(0,Session.Settings.returnGraceSeconds))/Mathf.Max(.1f,Session.Settings.lateFanLossInterval)));
            RoamRemaining=snapshot.roamRemaining;StateRemaining=snapshot.stateRemaining;
            Level.Apply(snapshot.flappy);
            if(Session.IsStreamerPlayer) body.enabled=true;
        }
        public void PredictCommand(PlayerCommand command,float dt,bool replay)
        {
            if(Session.IsAuthority || !Session.IsStreamerPlayer || Session.Finished) return;
            if(Actor.State==StreamerState.Streaming)
            {
                // Presentation only: the authority still decides score, state transitions and victory.
                if(command.action) Level.Flap();
                Level.Tick(dt);return;
            }
            if(Actor.State!=StreamerState.Roaming && Actor.State!=StreamerState.Returning) return;
            if(replay) LookInput(new Vector2(command.lookX,command.lookY));
            posture.SetCrouched(command.crouch);
            if(body.isGrounded && vertical<0) vertical=-2;
            vertical+=Physics.gravity.y*dt;body.Move(Vector3.up*vertical*dt);
            MoveInput(new Vector2(command.x,command.y),command.sneak,dt,replay);
        }

        void HumanInput(float dt)
        {
            if(Session.DebugOpen || !Session.InputCaptured) return;
            var keys=Keyboard.current; if(keys==null) return;
            if(Actor.State==StreamerState.Streaming)
            {
                if(keys.spaceKey.wasPressedThisFrame || Mouse.current?.leftButton.wasPressedThisFrame==true) Flap();
                return;
            }
            if(Actor.State!=StreamerState.Roaming && Actor.State!=StreamerState.Returning) return;
            Vector2 look=Mouse.current?.delta.ReadValue()??Vector2.zero;
            LookInput(look);
            posture.SetCrouched(keys.ctrlKey.isPressed);
            var input=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
            MoveInput(input,keys.shiftKey.isPressed,dt);
            if(keys.eKey.wasPressedThisFrame) Interact();
        }
        public void LookInput(Vector2 input)
        {
            if(Actor.State==StreamerState.Streaming || Session.Finished) return;
            transform.Rotate(0,input.x*.12f,0);
            pitch=Mathf.Clamp(pitch-input.y*.12f,-85,85);
            Actor.View.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }
        public void MoveInput(Vector2 input,bool sneak,float dt,bool replay=false)
        {
            if(Session.Finished || Actor.State!=StreamerState.Roaming && Actor.State!=StreamerState.Returning) return;
            input=Vector2.ClampMagnitude(input,1);
            Vector3 before=transform.position;
            body.Move((transform.right*input.x+transform.forward*input.y)*Session.Settings.streamerSpeed*(sneak || Crouched?.45f:1)*dt);
            Vector3 moved=transform.position-before; moved.y=0;
            motion.Record(moved,dt,replay);sneaking=(sneak || Crouched) && input.sqrMagnitude>.01f;
            if(Session.IsAuthority && !sneak && !Crouched && moved.magnitude>.002f && body.isGrounded && Time.time>=nextFootstep)
            { nextFootstep=Time.time+Session.ChaseStepInterval; Session.PlayLocalFootstep(transform.position); }
        }
        public void Interact()
        {
            if(Session.Finished || Actor.State!=StreamerState.Roaming && Actor.State!=StreamerState.Returning) return;
            if(Vector3.Distance(transform.position,Actor.Computer.Front)<2.3f &&
                !Physics.Linecast(Actor.View.transform.position,Actor.Computer.Front+Vector3.up,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore))
            { BeginStreaming(); return; }
            if(Physics.Raycast(Actor.View.transform.position,Actor.View.transform.forward,out var hit,3,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
            {
                var item=hit.collider.GetComponentInParent<PlacedItemView>();
                if(item && item.IsHidingPlace) { SearchHidingPlace(item);return; }
                if(item && item.Data.type==ItemType.Door)
                {
                    item.SetDoorOpen(!item.IsOpen);
                    Session.EmitNoise(item.transform.position,item.IsOpen?NoiseKind.Door:NoiseKind.DoorClose); return;
                }
            }
            var openDoor=Session.World.Items.FirstOrDefault(i=>i.Data.type==ItemType.Door && i.IsOpen &&
                Vector3.Distance(i.transform.position,transform.position)<2.3f &&
                !Physics.Linecast(Actor.View.transform.position,i.transform.position+Vector3.up,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore));
            if(openDoor) { openDoor.SetDoorOpen(false); Session.EmitNoise(openDoor.transform.position,NoiseKind.DoorClose); }
        }

        void Move(float dt)
        {
            if(stairRoute!=null)
            {
                if(MoveTowards(stairRoute[stairIndex],dt)) stairIndex++;
                if(stairIndex>=stairRoute.Count)
                {
                    stairRoute=null; pathIndex++;
                    if(pendingDestination.HasValue)
                    {
                        Vector3 target=pendingDestination.Value; pendingDestination=null; SetDestination(target);
                    }
                }
                return;
            }
            if(!HasPath) return;
            Vector3Int cell=path[pathIndex];
            if(pathIndex>0 && cell.y!=path[pathIndex-1].y)
            {
                var stair=Navigation.StairBetween(path[pathIndex-1],cell);
                if(stair==null) { path=null; return; }
                stairRoute=stair.Waypoints(cell.y>path[pathIndex-1].y); stairIndex=0;
                return;
            }
            var item=Session.World.Data.At(cell.y,cell.x,cell.z);
            if(item?.type==ItemType.Door)
            {
                var door=Session.World.Find(item.id);
                if(!door.IsOpen) { door.SetDoorOpen(true); Physics.SyncTransforms(); Session.PlayWorldSound(door.transform.position,NoiseKind.Door); }
            }
            if(MoveTowards(Navigation.Position(cell),dt)) pathIndex++;
        }

        bool MoveTowards(Vector3 target,float dt)
        {
            Vector3 delta=target-transform.position; delta.y=0;
            if(delta.magnitude<.09f) return Mathf.Abs(target.y-transform.position.y)<.4f;
            float step=Mathf.Min(delta.magnitude,Session.Settings.streamerSpeed*dt);
            var before=transform.position;
            body.Move(delta.normalized*step);
            motion.Record(transform.position-before,dt);sneaking=false;
            if(Session.IsAuthority && Vector3.Distance(before,transform.position)>.002f && body.isGrounded && Time.time>=nextFootstep)
            { nextFootstep=Time.time+Session.ChaseStepInterval;Session.PlayLocalFootstep(transform.position); }
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),360*dt);
            return false;
        }

        void OnDestroy() { if(Actor && Session) Session.NoiseEmitted-=OnNoise; }
    }

    public sealed class AiStreamerDriver : IStreamerDriver
    {
        Vector3? lastSound;
        float soundUntil,nextDecision;
        public void Hear(SoundPulse pulse)
        {
            if(pulse.Kind==NoiseKind.Chair) return;
            lastSound=pulse.Position; soundUntil=Time.time+8;
            nextDecision=0;
        }

        public void Tick(StreamerController controller,float deltaTime)
        {
            var actor=controller.Actor;
            var session=controller.Session;
            if(actor.State==StreamerState.Streaming)
            {
                if(session.Settings.allowEmergencyTurn && Time.time<actor.AlertUntil && Time.time>=nextDecision)
                { nextDecision=Time.time+5; controller.TryEmergencyTurn(); return; }
                if(controller.Level.WantsAutoFlap) controller.Flap();
                return;
            }
            if(actor.State!=StreamerState.Roaming && actor.State!=StreamerState.Ambush) return;
            if(Time.time<nextDecision) return;
            nextDecision=Time.time+.55f;
            if(controller.CanSeeReporter()) { controller.SetDestination(session.Reporter.transform.position); return; }
            if(lastSound.HasValue && Time.time<soundUntil)
            {
                var hiding=session.World.Items.FirstOrDefault(i=>i.IsHidingPlace && Vector3.Distance(i.transform.position,lastSound.Value)<.8f);
                if(hiding)
                {
                    if(Vector3.Distance(actor.transform.position,hiding.Front)<1.6f) { controller.SearchHidingPlace(hiding);lastSound=null; }
                    else controller.SetDestination(hiding.Front);
                    return;
                }
                if(Vector3.Distance(actor.transform.position,lastSound.Value)>1) controller.SetDestination(lastSound.Value);
                else lastSound=null;
                return;
            }
            if(!controller.HasPath && controller.PatrolCells.Count>0)
                controller.SetDestination(controller.Navigation.Position(controller.PatrolCells[UnityEngine.Random.Range(0,controller.PatrolCells.Count)]));
        }
    }
}
