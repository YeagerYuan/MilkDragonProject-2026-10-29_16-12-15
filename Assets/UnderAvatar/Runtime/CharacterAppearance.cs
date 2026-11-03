using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    [DefaultExecutionOrder(150)]
    public sealed class CharacterAppearance : MonoBehaviour
    {
        MatchSession session;
        TestRole role;
        CharacterVisualProfile profile;
        CharacterVisualSettings settings;
        Func<ActorSnapshot> capture;
        readonly RemoteVisualBuffer buffer=new RemoteVisualBuffer();
        readonly Dictionary<string,AnimatorControllerParameterType> parameters=new Dictionary<string,AnimatorControllerParameterType>();
        Renderer[] renderers;
        bool[] originalVisibility;
        Transform postureRoot;
        Vector3 smoothVelocity;
        Vector3 renderedPosition;
        Quaternion renderedRotation;
        bool rendered;
        bool visible=true;
        public Animator Animator { get; private set; }
        public Transform Model { get; private set; }
        public bool Remote => session.IsNetwork && session.Role!=role;
        public void Initialize(MatchSession match,TestRole playerRole,Func<ActorSnapshot> getPose)
        {
            session=match;role=playerRole;capture=getPose;profile=match.App.CharacterVisuals;
            settings=profile?(role==TestRole.Reporter?profile.reporter:profile.streamer):new CharacterVisualSettings();
            postureRoot=new GameObject("Posture").transform;postureRoot.SetParent(transform,false);
            var mount=new GameObject("ModelOffset").transform;mount.SetParent(postureRoot,false);
            mount.gameObject.SetActive(false);
            mount.localPosition=settings.offset;mount.localRotation=Quaternion.Euler(settings.rotation);mount.localScale=settings.scale;
            GameObject model;
            if(settings.prefab) model=Instantiate(settings.prefab,mount,false);
            else
            {
                model=GameObject.CreatePrimitive(PrimitiveType.Capsule);model.transform.SetParent(mount,false);
                model.transform.localPosition=Vector3.up*.9f;model.transform.localScale=new Vector3(.6f,.9f,.6f);
                model.GetComponent<Renderer>().sharedMaterial=match.World.Palette.Material(role==TestRole.Reporter?new Color(.19f,.65f,.68f):new Color(.52f,.17f,.24f));
            }
            Model=model.transform;
            foreach(var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=PrototypeLayers.Get(role==TestRole.Reporter?"UA_Reporter":"UA_Player");
            foreach(var c in model.GetComponentsInChildren<Collider>(true)) c.enabled=false;
            foreach(var b in model.GetComponentsInChildren<Rigidbody>(true)) { b.isKinematic=true;b.useGravity=false; }
            foreach(var b in model.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
            foreach(var c in model.GetComponentsInChildren<Camera>(true)) c.enabled=false;
            foreach(var c in model.GetComponentsInChildren<AudioListener>(true)) c.enabled=false;
            foreach(var c in model.GetComponentsInChildren<AudioSource>(true)) c.enabled=false;
            foreach(var c in model.GetComponentsInChildren<Light>(true)) c.enabled=false;
            foreach(var c in model.GetComponentsInChildren<Animation>(true)) c.enabled=false;
            Animator=model.GetComponentInChildren<Animator>(true);
            if(!Animator && settings.animatorController) Animator=model.AddComponent<Animator>();
            if(Animator)
            {
                if(settings.animatorController) Animator.runtimeAnimatorController=settings.animatorController;
                Animator.applyRootMotion=false;Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            }
            renderers=model.GetComponentsInChildren<Renderer>(true);originalVisibility=new bool[renderers.Length];
            for(int i=0;i<renderers.Length;i++) originalVisibility[i]=renderers[i].enabled;
            mount.gameObject.SetActive(true);
            // Player builds initialize the Animator graph on activation. Reading parameters while the
            // prefab is inactive can return an empty list, leaving every mapped parameter undriven.
            if(Animator && Animator.runtimeAnimatorController)
            {
                Animator.Rebind();
                foreach(var p in Animator.parameters) parameters[p.name]=p.type;
            }
        }
        public void Receive(ActorSnapshot pose,float time)
        {
            if(!Remote) return;
            buffer.Add(pose,time,Time.unscaledTime,profile?profile.interpolationDelay:.1f);
            SetVisible(!pose.hidden); // Hide immediately; never leave a delayed silhouette outside a wardrobe.
        }
        public void SetVisible(bool value)
        {
            if(value==visible) return;visible=value;
            for(int i=0;i<renderers.Length;i++) if(renderers[i]) renderers[i].enabled=value && originalVisibility[i];
        }
        bool Has(string name,AnimatorControllerParameterType type) => Animator && !string.IsNullOrEmpty(name) && parameters.TryGetValue(name,out var found) && found==type;
        void Float(string name,float value,float dt) { if(Has(name,AnimatorControllerParameterType.Float)) Animator.SetFloat(name,value,Mathf.Max(.01f,settings.animationDamping),dt); }
        void Bool(string name,bool value) { if(Has(name,AnimatorControllerParameterType.Bool)) Animator.SetBool(name,value); }
        void LateUpdate()
        {
            if(session==null || capture==null) return;
            var actual=capture();var pose=actual;float dt=Time.unscaledDeltaTime;
            if(Remote)
            {
                if(session.IsAuthority) Receive(actual,Time.unscaledTime);
                pose=buffer.Evaluate(dt,Time.unscaledTime,profile?profile.interpolationDelay:.1f,profile?profile.maxExtrapolation:.08f,profile?profile.staleAfter:.35f)??actual;
                if(!rendered || buffer.JustSnapped || actual.hidden || Vector3.Distance(renderedPosition,pose.position)>3)
                { renderedPosition=pose.position;renderedRotation=Quaternion.Euler(0,pose.yaw,0);smoothVelocity=Vector3.zero;buffer.ConsumeSnap();rendered=true; }
                else renderedPosition=Vector3.SmoothDamp(renderedPosition,pose.position,ref smoothVelocity,profile?profile.positionSmoothing:.04f,20,dt);
                renderedRotation=Quaternion.Slerp(renderedRotation,Quaternion.Euler(0,pose.yaw,0),1-Mathf.Exp(-25*dt));
                transform.SetPositionAndRotation(renderedPosition,renderedRotation);
            }
            else { transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;SetVisible(!actual.hidden); }
            var velocity=session.Finished || actual.hidden?Vector3.zero:pose.planarVelocity;
            var local=Quaternion.Inverse(Quaternion.Euler(0,pose.yaw,0))*velocity;
            Float(settings.speedParameter,velocity.magnitude,dt);Float(settings.moveXParameter,local.x,dt);Float(settings.moveZParameter,local.z,dt);
            Bool(settings.crouchedParameter,pose.crouched);Bool(settings.sneakingParameter,pose.sneaking);
            postureRoot.localScale=new Vector3(1,pose.crouched && !Has(settings.crouchedParameter,AnimatorControllerParameterType.Bool)?1f/1.8f:1,1);
        }
    }
}
