using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    public sealed class ReporterController : MonoBehaviour
    {
        public Camera View { get; private set; }
        public bool PhoneOpen { get; private set; }
        public bool Hidden { get; private set; }
        public bool Sneaking { get; private set; }
        public bool Crouched => posture.Crouched;
        public bool IsHuman => session.Role==TestRole.Reporter;
        public int Floor => Mathf.Clamp(Mathf.FloorToInt((transform.position.y+.15f)/session.World.Data.floorHeight),0,session.World.Data.floors.Count-1);
        MatchSession session;
        CharacterController body;
        // Renderer bodyVisual;
        CharacterPosture posture;
        float pitch,vertical,nextFootstep;
        Vector3 hidingExit;
        ActorSnapshot replica;
        GameObject modelVisual;
        Animator animator;

        public void Initialize(MatchSession match)
        {
            session=match;
            gameObject.layer=PrototypeLayers.Get("UA_Reporter");
            body=gameObject.AddComponent<CharacterController>();
            body.height=1.8f; body.radius=.3f; body.center=Vector3.up*.9f; body.stepOffset=.3f;

            // Create a container for the character model
            var model = new GameObject("ReporterVisual");
            model.transform.SetParent(transform, false);

            // Instantiate the rigged paparazzi prefab
            if (session.App.ReporterPrefab == null)
            {
                throw new System.InvalidOperationException(
                    "Reporter Prefab is not assigned in PrototypeApp."
                );
            }

            modelVisual = Instantiate(session.App.ReporterPrefab, model.transform);
            modelVisual.name = "PaparazziModel";

            animator = modelVisual.GetComponentInChildren<Animator>();

            modelVisual.transform.localPosition = Vector3.zero;
            modelVisual.transform.localRotation = Quaternion.identity;

            // Set the entire character hierarchy to the reporter layer
            int reporterLayer = PrototypeLayers.Get("UA_Reporter");

            foreach (Transform child in modelVisual.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = reporterLayer;
            }

            // Disable visual-model colliders.
            // The parent Reporter already has its own CharacterController.
            foreach (Collider col in modelVisual.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            var head=new GameObject("ReporterCamera"); head.transform.SetParent(transform,false); head.transform.localPosition=Vector3.up*1.65f;
            View=head.AddComponent<Camera>(); View.nearClipPlane=.05f; View.farClipPlane=150; View.fieldOfView=75;
            View.cullingMask=~LayerMask.GetMask("UA_Sound","UA_Reporter");
            head.AddComponent<AudioListener>();
            posture=new CharacterPosture(body,head.transform,model.transform,1.65f);
            SetCursor(false);
        }

        public void SetPhone(bool open) { if(!Hidden) PhoneOpen=open; }
        public void SetCursor(bool released)
        {
            Cursor.lockState=released?CursorLockMode.None:CursorLockMode.Locked;
            Cursor.visible=released;
        }

        public void Teleport(Vector3 position)
        {
            body.enabled=false; transform.position=position+Vector3.up*.02f; body.enabled=!Hidden; vertical=0;
            Physics.SyncTransforms();
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
                if (IsHuman && !Hidden && !session.Finished && !session.DebugOpen && session.InputCaptured &&
                    !DocumentManager.BlocksGameplayInput && Mouse.current?.leftButton.wasPressedThisFrame == true)
                    InteractDocument();
                if(!session.IsAuthority && !IsHuman && replica!=null) SmoothReplica();
                return;
            }
            if(!IsHuman || session.Finished || session.DebugOpen || !session.InputCaptured) return;
            var keys=Keyboard.current;
            if(keys==null) return;
            if (DocumentManager.BlocksGameplayInput) return;
            if(keys.eKey.wasPressedThisFrame)
            {
                Interact(); 
                if(Hidden) return;  
            } 
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                InteractDocument();
            }
            if (DocumentManager.BlocksGameplayInput) return;
            if(Hidden) return;
            Vector2 look=Mouse.current?.delta.ReadValue()??Vector2.zero;
            transform.Rotate(0,look.x*.12f,0);
            pitch=Mathf.Clamp(pitch-look.y*.12f,-85,85);
            View.transform.localRotation=Quaternion.Euler(pitch,0,0);
            Vector2 input=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
            input=Vector2.ClampMagnitude(input,1);
            SetCrouched(keys.ctrlKey.isPressed);
            MoveInput(input,keys.shiftKey.isPressed,Time.deltaTime);
            if(keys.fKey.wasPressedThisFrame) PhoneOpen=!PhoneOpen;
            if(PhoneOpen && (Mouse.current?.leftButton.wasPressedThisFrame==true || keys.spaceKey.wasPressedThisFrame)) session.TakePhoto();
        }

        void LateUpdate()
        {
            if (animator == null || body == null || !body.enabled)
                return;

            Vector3 velocity = body.velocity;
            velocity.y = 0f; 

            float speed = velocity.magnitude;

            float normalizedSpeed = session.Settings.reporterSpeed > 0f
                ? speed / session.Settings.reporterSpeed
                : 0f;

            animator.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
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
            if(command.phone) PhoneOpen=!PhoneOpen;
            if(command.action && PhoneOpen) session.TakePhoto();
        }
        public ActorSnapshot CapturePose() => new ActorSnapshot
        {
            position=transform.position,yaw=transform.eulerAngles.y,pitch=pitch,vertical=vertical,
            crouched=Crouched,hidden=Hidden,phone=PhoneOpen,sneaking=Sneaking
        };
        public void ApplyPose(ActorSnapshot value)
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
            replica=value;Hidden=value.hidden;PhoneOpen=value.phone;Sneaking=value.sneaking;
            posture.SetCrouched(value.crouched,true);modelVisual.SetActive(!Hidden);
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
            SetCrouched(command.crouch);MoveInput(new Vector2(command.x,command.y),command.sneak,dt);
            if(command.phone) PhoneOpen=!PhoneOpen;
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
                { if(IsHuman) session.Notice("衣柜出口被阻挡，暂时无法离开。"); return; }
                Hidden=false; modelVisual.SetActive(true); Teleport(hidingExit); if(IsHuman) session.Notice("已离开衣柜。"); return;
            }
            if(Physics.Raycast(View.transform.position,View.transform.forward,out var hit,3f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
            {
                var item=hit.collider.GetComponentInParent<PlacedItemView>();
                if(item && item.Data.type==ItemType.Door) { ToggleDoor(item); return; }
                if(item && item.Data.type==ItemType.Furniture && item.Data.hideable) { Hide(item); return; }
            }
            var door=session.World.Items.Where(i=>i.Floor==Floor && i.Data.type==ItemType.Door && i.IsOpen)
                .Where(i=>Vector3.Distance(i.transform.position,transform.position)<2.3f)
                .OrderBy(i=>Vector3.Distance(i.transform.position,transform.position))
                .FirstOrDefault(i=>!Physics.Linecast(View.transform.position,i.transform.position+Vector3.up,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore));
            if(door) { ToggleDoor(door); return; }
            session.Notice("靠近并看向门或衣柜后按 E；楼梯直接步行上下。");
        }

        public void InteractDocument()
        {
            if (DocumentManager.BlocksGameplayInput || !IsHuman || Hidden || session.Finished || session.DebugOpen || !session.InputCaptured) return;
            int layerMask = ~LayerMask.GetMask("UA_Reporter");

            if (Physics.Raycast(
                View.transform.position,
                View.transform.forward,
                out var hit,
                3f,
                layerMask,
                QueryTriggerInteraction.Ignore))
            {
                Debug.Log("Hit object: " + hit.collider.name);

                var document =
                    hit.collider.GetComponentInParent<DocumentInteractable>();

                if (document != null)
                {
                    Debug.Log("Document found!");
                    document.Interact();
                }
                else
                {
                    Debug.Log("Hit object is not a document");
                }
            }
            else
            {
                Debug.Log("Raycast hit nothing!");
            }
        }

        public bool SetCrouched(bool crouch) => posture.SetCrouched(crouch);
        public float MovementSpeed(bool sneak) => session.Settings.reporterSpeed*(sneak || Crouched?.45f:1);
        public void MoveInput(Vector2 input,bool sneak,float deltaTime)
        {
            if(Hidden || session.Finished || !body.enabled) return;
            input=Vector2.ClampMagnitude(input,1);
            Sneaking=(sneak || Crouched) && input.sqrMagnitude>.01f;
            if(body.isGrounded && vertical<0) vertical=-2;
            vertical+=Physics.gravity.y*deltaTime;
            Vector3 previous=transform.position;
            body.Move(((transform.right*input.x+transform.forward*input.y)*MovementSpeed(sneak)+Vector3.up*vertical)*deltaTime);
            Vector3 displacement=transform.position-previous; displacement.y=0;
            if(session.IsAuthority && !Sneaking && input.sqrMagnitude>.01f && displacement.magnitude>.002f && body.isGrounded && Time.time>=nextFootstep)
            { nextFootstep=Time.time+.48f; session.EmitNoise(transform.position,NoiseKind.Footstep); }
        }

        void ToggleDoor(PlacedItemView door)
        {
            if(door.IsOpen && Vector3.Distance(door.transform.position,transform.position)<.7f)
            { session.Notice("请先离开门口，再关门。"); return; }
            door.SetDoorOpen(!door.IsOpen); session.EmitNoise(transform.position,NoiseKind.Door);
            session.Notice(door.IsOpen?"门已打开。":"门已关闭。");
        }

        public void Hide(PlacedItemView wardrobe)
        {
            if(wardrobe.Data.type!=ItemType.Furniture || !wardrobe.Data.hideable || session.Finished) return;
            hidingExit=transform.position; PhoneOpen=false; Sneaking=false; Hidden=true;
            modelVisual.SetActive(false);
            body.enabled=false; transform.position=wardrobe.transform.position;
            if(IsHuman) session.Notice("已躲入衣柜。无法拍照，按 E 离开。",5);
        }
    }
}
