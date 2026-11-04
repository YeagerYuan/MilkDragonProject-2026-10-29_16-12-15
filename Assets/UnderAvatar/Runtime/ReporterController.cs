using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    public sealed class ReporterController : MonoBehaviour
    {
        public Camera View { get; private set; }
        public VisionLight Vision { get; private set; }
        public bool PhoneOpen { get; private set; }
        public bool Hidden { get; private set; }
        public string HidingPlaceId { get; private set; }
        public PlacedItemView HidingPlace => string.IsNullOrEmpty(HidingPlaceId)?null:session.World.Find(HidingPlaceId);
        public bool Sneaking { get; private set; }
        public bool Crouched => posture.Crouched;
        public bool IsHuman => session.Role==TestRole.Reporter;
        public int Floor => Mathf.Clamp(Mathf.FloorToInt((transform.position.y+.15f)/session.World.Data.floorHeight),0,session.World.Data.floors.Count-1);
        MatchSession session;
        public MatchSession Session => session;
        public ReporterInteraction Documents { get; private set; }
        CharacterController body;
        public CharacterAppearance Appearance { get; private set; }
        readonly CharacterMotion motion=new CharacterMotion();
        CharacterPosture posture;
        float pitch,vertical,nextFootstep;
        Vector3 hidingExit;
        ActorSnapshot replica;
        float lastLocalMovement=float.NegativeInfinity;
        public bool MovedLocallyRecently => Time.unscaledTime-lastLocalMovement<.08f;

        public void Initialize(MatchSession match)
        {
            session=match;
            gameObject.layer=PrototypeLayers.Get("UA_Reporter");
            body=gameObject.AddComponent<CharacterController>();
            body.height=1.8f; body.radius=.3f; body.center=Vector3.up*.9f; body.stepOffset=.3f;
            var model=new GameObject("ReporterVisual"); model.transform.SetParent(transform,false);
            Appearance=model.AddComponent<CharacterAppearance>();Appearance.Initialize(match,TestRole.Reporter,CapturePose);
            var head=new GameObject("ReporterCamera"); head.transform.SetParent(transform,false); head.transform.localPosition=Vector3.up*1.65f;
            View=head.AddComponent<Camera>(); View.nearClipPlane=.05f; View.farClipPlane=150; View.fieldOfView=75;
            View.cullingMask=~LayerMask.GetMask("UA_Sound","UA_Reporter");
            View.clearFlags=CameraClearFlags.SolidColor;View.backgroundColor=Color.black;
            Vision=VisionLight.Create(View,()=>session.Settings.reporterVision,"UA_Reporter",()=>!Hidden);
            head.AddComponent<AudioListener>();
            posture=new CharacterPosture(body,head.transform,null,1.65f);
            SetCursor(false);
            Documents=gameObject.AddComponent<ReporterInteraction>();
        }

        public void SetPhone(bool open)
        {
            if(Hidden) return;
            if(open && session.TaskWorld?.Held) {session.Notice("请先放下道具再打开手机。");return;}
            bool changed=PhoneOpen!=open;PhoneOpen=open;
            if(changed && IsHuman) session.SetPhoneMenu(open);
        }
        public void SetCursor(bool released)
        {
            Cursor.lockState=released?CursorLockMode.None:CursorLockMode.Locked;
            Cursor.visible=released;
        }

        public void Teleport(Vector3 position)
        {
            body.enabled=false; transform.position=position+Vector3.up*.02f; body.enabled=!Hidden; vertical=0;
            motion.Reset();Physics.SyncTransforms();
        }

        public void SetLookAt(Vector3 target)
        {
            Vector3 delta=target-View.transform.position;
            transform.rotation=Quaternion.Euler(0,Quaternion.LookRotation(delta).eulerAngles.y,0);
            float angle=Quaternion.LookRotation(delta).eulerAngles.x;
            pitch=angle>180?angle-360:angle;
            View.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }

        void Update()
        {
            if(session.IsNetwork)
            {
                if(!session.IsAuthority && !IsHuman && replica!=null) SmoothReplica();
                return;
            }
            if(!IsHuman || session.Finished || session.DebugOpen || !session.InputCaptured || DocumentManager.BlocksGameplayInput) return;
            var keys=Keyboard.current;
            if(keys==null) return;
            if(keys.eKey.wasPressedThisFrame) { Interact(); if(Hidden) return; }
            if(Hidden) return;
            Vector2 look=Mouse.current?.delta.ReadValue()??Vector2.zero;
            transform.Rotate(0,look.x*.12f,0);
            pitch=Mathf.Clamp(pitch-look.y*.12f,-85,85);
            View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            Vector2 input=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
            input=Vector2.ClampMagnitude(input,1);
            SetCrouched(keys.ctrlKey.isPressed);
            MoveInput(input,keys.shiftKey.isPressed,Time.deltaTime);
            if(keys.fKey.wasPressedThisFrame) SetPhone(!PhoneOpen);
            if(PhoneOpen && (Mouse.current?.leftButton.wasPressedThisFrame==true || keys.spaceKey.wasPressedThisFrame)) session.TakePhoto();
        }

        public void ApplyCommand(PlayerCommand command,float dt)
        {
            if(!session.IsAuthority || session.Finished) return;
            View.aspect=command.aspect;
            if(command.interact) Interact();
            if(Hidden) return;
            transform.Rotate(0,command.lookX*.12f,0);
            pitch=Mathf.Clamp(pitch-command.lookY*.12f,-85,85);
            View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            SetCrouched(command.crouch);
            MoveInput(new Vector2(command.x,command.y),command.sneak,dt);
            if(command.phone) SetPhone(!PhoneOpen);
            session.ApplyPhoneCommand(command);
            if(command.action && PhoneOpen) session.TakePhoto();
        }
        public ActorSnapshot CapturePose() => new ActorSnapshot
        {
            position=transform.position,yaw=transform.eulerAngles.y,pitch=pitch,vertical=vertical,
            crouched=Crouched,hidden=Hidden,phone=PhoneOpen,sneaking=Sneaking,hidingPlaceId=HidingPlaceId,planarVelocity=motion.Velocity
        };
        public void ApplyPose(ActorSnapshot value,float serverTime=0)
        {
            if(value==null) return;
            body.enabled=false;
            if(IsHuman)
            {
                transform.position=value.position;transform.rotation=Quaternion.Euler(0,value.yaw,0);
                pitch=value.pitch;vertical=value.vertical;View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            }
            if(replica==null || value.hidden!=Hidden || Vector3.Distance(transform.position,value.position)>3)
                transform.position=value.position;
            replica=value;Hidden=value.hidden;PhoneOpen=value.phone;Sneaking=value.sneaking;HidingPlaceId=value.hidingPlaceId;
            posture.SetCrouched(value.crouched,true);Appearance.SetVisible(!Hidden);Appearance.Receive(value,serverTime);
            if(IsHuman) body.enabled=!Hidden;
        }
        public void PredictLook(Vector2 look)
        {
            if(Hidden || session.Finished) return;
            transform.Rotate(0,look.x*.12f,0);pitch=Mathf.Clamp(pitch-look.y*.12f,-85,85);
            View.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }
        public void PredictCommand(PlayerCommand command,float dt,bool replay)
        {
            if(!IsHuman || session.IsAuthority || Hidden || session.Finished) return;
            if(replay) PredictLook(new Vector2(command.lookX,command.lookY));
            SetCrouched(command.crouch);MoveInput(new Vector2(command.x,command.y),command.sneak,dt,replay);
            if(command.phone) {if(replay) PhoneOpen=!PhoneOpen;else SetPhone(!PhoneOpen);}
            if(!replay && command.interact) session.TaskWorld?.Interact(true,command.sequence);
            if(!replay && command.action && PhoneOpen) {session.CapturePreview();session.SetPhoneMenu(true);}
        }
        void SmoothReplica()
        {
            float blend=1-Mathf.Exp(-35*Time.deltaTime);
            transform.position=Vector3.Lerp(transform.position,replica.position,blend);
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(0,replica.yaw,0),blend);
            pitch=Mathf.LerpAngle(pitch,replica.pitch,blend);
            View.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }

        public void Interact()
        {
            if(Hidden)
            {
                if(Physics.CheckCapsule(hidingExit+Vector3.up*.4f,hidingExit+Vector3.up*1.5f,.3f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
                { if(IsHuman) session.Notice("藏身点出口被阻挡，暂时无法离开。"); return; }
                Hidden=false;HidingPlaceId=null; Appearance.SetVisible(true); Teleport(hidingExit); if(IsHuman) session.Notice("已离开藏身点。"); return;
            }
            if(Documents && Documents.TryRead()) return;
            if(session.TaskWorld?.Held && Physics.Raycast(View.transform.position,View.transform.forward,out var doorHit,3f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
            {
                var heldDoor=doorHit.collider.GetComponentInParent<PlacedItemView>();
                if(heldDoor && heldDoor.Data.type==ItemType.Door) {ToggleDoor(heldDoor);return;}
            }
            if(session.TaskWorld!=null && session.TaskWorld.Interact()) return;
            if(Physics.Raycast(View.transform.position,View.transform.forward,out var hit,3f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
            {
                var item=hit.collider.GetComponentInParent<PlacedItemView>();
                if(item && item.Data.type==ItemType.Door) { ToggleDoor(item); return; }
                if(item && item.IsHidingPlace) { Hide(item); return; }
            }
            var door=session.World.Items.Where(i=>i.Floor==Floor && i.Data.type==ItemType.Door && i.IsOpen)
                .Where(i=>Vector3.Distance(i.transform.position,transform.position)<2.3f)
                .OrderBy(i=>Vector3.Distance(i.transform.position,transform.position))
                .FirstOrDefault(i=>!Physics.Linecast(View.transform.position,i.transform.position+Vector3.up,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore));
            if(door) { ToggleDoor(door); return; }
            session.Notice("靠近并看向门、衣柜或床后按 E；楼梯直接步行上下。");
        }

        public bool SetCrouched(bool crouch) => posture.SetCrouched(crouch);
        public float MovementSpeed(bool sneak) => session.Settings.reporterSpeed*(sneak || Crouched?.45f:1);
        public void MoveInput(Vector2 input,bool sneak,float deltaTime,bool replay=false)
        {
            if(Hidden || session.Finished || !body.enabled) return;
            input=Vector2.ClampMagnitude(input,1);
            Sneaking=(sneak || Crouched) && input.sqrMagnitude>.01f;
            if(body.isGrounded && vertical<0) vertical=-2;
            vertical+=Physics.gravity.y*deltaTime;
            Vector3 previous=transform.position;
            body.Move(((transform.right*input.x+transform.forward*input.y)*MovementSpeed(sneak)+Vector3.up*vertical)*deltaTime);
            Vector3 displacement=transform.position-previous; displacement.y=0;
            motion.Record(displacement,deltaTime,replay);
            if(IsHuman && !replay)
                lastLocalMovement=input.sqrMagnitude>.01f && displacement.magnitude>.002f && body.isGrounded?
                    Time.unscaledTime:float.NegativeInfinity;
            if(session.IsAuthority && !Sneaking && input.sqrMagnitude>.01f && displacement.magnitude>.002f && body.isGrounded && Time.time>=nextFootstep)
            { nextFootstep=Time.time+session.WalkStepInterval; session.EmitNoise(transform.position,NoiseKind.Footstep); }
        }

        void ToggleDoor(PlacedItemView door)
        {
            if(door.IsOpen && Vector3.Distance(door.transform.position,transform.position)<.7f)
            { session.Notice("请先离开门口，再关门。"); return; }
            door.SetDoorOpen(!door.IsOpen); session.EmitNoise(door.transform.position,door.IsOpen?NoiseKind.Door:NoiseKind.DoorClose);
            session.Notice(door.IsOpen?"门已打开。":"门已关闭。");
        }

        public void Hide(PlacedItemView wardrobe)
        {
            if(!session.IsAuthority || !wardrobe || !wardrobe.IsHidingPlace || session.Finished || Hidden) return;
            if(session.TaskWorld?.Held) {session.Notice("请先放下道具再躲藏。");return;}
            if(wardrobe.IsOpen) { if(IsHuman) session.Notice("藏身点正在被翻查，稍后再躲藏。");return; }
            hidingExit=transform.position; PhoneOpen=false; Sneaking=false; Hidden=true;
            HidingPlaceId=wardrobe.Data.id;
            Appearance.SetVisible(false);motion.Reset();
            body.enabled=false; transform.position=wardrobe.transform.position;
            session.EmitNoise(wardrobe.transform.position,wardrobe.Data.type==ItemType.Bed?NoiseKind.HideBed:NoiseKind.Door);
            if(IsHuman) session.Notice(wardrobe.Data.type==ItemType.Bed?"已躲到床下，挪动床铺的声音很大。按 E 离开。":"已躲入衣柜。无法拍照，按 E 离开。",5);
        }
    }
}
