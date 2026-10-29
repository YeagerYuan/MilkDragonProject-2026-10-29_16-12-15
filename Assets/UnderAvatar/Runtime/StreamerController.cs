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
        public bool OnStairs => stairRoute!=null;

        public void Initialize(StreamerActor actor)
        {
            Actor=actor; Navigation=new BuildingNavigation(Session.World.Data);
            body=gameObject.AddComponent<CharacterController>();
            body.height=1.8f; body.center=Vector3.up*.9f; body.radius=.28f; body.stepOffset=.3f; body.slopeLimit=45;
            body.skinWidth=.025f;
            posture=new CharacterPosture(body,Actor.View.transform,Actor.Model,1.7f);
            patrolCells=Navigation.ReachableFrom(transform.position).ToList();
            Level=new MinigameLevel(Environment.TickCount);
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
            if(!Session.IsAuthority || Session.Finished) return;
            if(Actor.State==StreamerState.Streaming && !Level.Complete) return;
            Actor.State=StreamerState.Returning; Actor.Progress=0;
            if(IsHuman) { path=null; Session.Notice("搜捕时间结束，请回到电脑前按 E 开始下一场直播。",6); return; }
            if(!SetDestination(Actor.Computer.Front)) Session.Notice("主播无法抵达电脑，请检查门和楼梯连接。",6);
        }

        public void BeginStreaming()
        {
            if(!Session.IsAuthority || Session.Finished) return;
            if(Actor.State==StreamerState.Streaming && Level.TargetScore>0) return;
            path=null; stairRoute=null; pendingDestination=null;
            body.enabled=false; transform.position=Actor.Computer.Front; body.enabled=true; vertical=0;
            transform.rotation=Actor.Computer.transform.rotation;
            posture.SetCrouched(false); pitch=0; Actor.View.transform.localRotation=Quaternion.identity;
            Actor.State=StreamerState.Streaming;
            Level.Start(round,Session.Settings.flappyTargetScore); Actor.Progress=0;
            Session.Notice("主播开始第 "+round+" 关直播。注意手机上的通关进度。");
        }

        public void Flap()
        {
            if(Session.IsAuthority && Actor.State==StreamerState.Streaming && !Session.Finished) Level.Flap();
        }
        public void AdvanceMinigame(float dt)
        {
            if(!Session.IsAuthority || Actor.State!=StreamerState.Streaming || Session.Finished) return;
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
            if(!Session.IsAuthority || Session.Finished || Session.Reporter.Hidden || Actor.State!=StreamerState.Roaming && Actor.State!=StreamerState.Ambush) return false;
            if(Vector3.Distance(transform.position,Session.Reporter.transform.position)>=1.3f) return false;
            if(Physics.Linecast(Actor.View.transform.position,Session.Reporter.View.transform.position,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore)) return false;
            Session.Finish("主播获胜：狗仔被灭口"); return true;
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
            if(body.isGrounded && vertical<0) vertical=-2;
            vertical+=Physics.gravity.y*dt;
            body.Move(Vector3.up*vertical*dt);
            if(Actor.State==StreamerState.Roaming)
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
        { position=transform.position,yaw=transform.eulerAngles.y,pitch=pitch,vertical=vertical,crouched=Crouched };
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
            Actor.State=snapshot.state;Actor.Progress=snapshot.progress;round=snapshot.broadcast;
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
            MoveInput(new Vector2(command.x,command.y),command.sneak,dt);
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
        public void MoveInput(Vector2 input,bool sneak,float dt)
        {
            if(Session.Finished || Actor.State!=StreamerState.Roaming && Actor.State!=StreamerState.Returning) return;
            input=Vector2.ClampMagnitude(input,1);
            Vector3 before=transform.position;
            body.Move((transform.right*input.x+transform.forward*input.y)*Session.Settings.streamerSpeed*(sneak || Crouched?.45f:1)*dt);
            Vector3 moved=transform.position-before; moved.y=0;
            if(Session.IsAuthority && !sneak && !Crouched && moved.magnitude>.002f && body.isGrounded && Time.time>=nextFootstep)
            { nextFootstep=Time.time+.48f; Session.PlayLocalFootstep(transform.position); }
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
                if(item && item.Data.type==ItemType.Door)
                {
                    item.SetDoorOpen(!item.IsOpen);
                    Session.EmitNoise(transform.position,NoiseKind.Door); return;
                }
            }
            var openDoor=Session.World.Items.FirstOrDefault(i=>i.Data.type==ItemType.Door && i.IsOpen &&
                Vector3.Distance(i.transform.position,transform.position)<2.3f &&
                !Physics.Linecast(Actor.View.transform.position,i.transform.position+Vector3.up,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore));
            if(openDoor) { openDoor.SetDoorOpen(false); Session.EmitNoise(transform.position,NoiseKind.Door); }
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
                if(!door.IsOpen) { door.SetDoorOpen(true); Physics.SyncTransforms(); }
            }
            if(MoveTowards(Navigation.Position(cell),dt)) pathIndex++;
        }

        bool MoveTowards(Vector3 target,float dt)
        {
            Vector3 delta=target-transform.position; delta.y=0;
            if(delta.magnitude<.09f) return Mathf.Abs(target.y-transform.position.y)<.4f;
            float step=Mathf.Min(delta.magnitude,Session.Settings.streamerSpeed*dt);
            body.Move(delta.normalized*step);
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
                if(Vector3.Distance(actor.transform.position,lastSound.Value)>1) controller.SetDestination(lastSound.Value);
                else lastSound=null;
                return;
            }
            if(!controller.HasPath && controller.PatrolCells.Count>0)
                controller.SetDestination(controller.Navigation.Position(controller.PatrolCells[UnityEngine.Random.Range(0,controller.PatrolCells.Count)]));
        }
    }
}
