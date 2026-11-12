using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    [Serializable] public sealed class DecoySnapshot
    {
        public int id;
        public Vector3 position;
        public float yaw,revealed;
        public bool crouched;
    }
    public sealed partial class PlayerAbilities
    {
        sealed class DecoyView
        {
            public DecoySnapshot state;
            public GameObject model;
            public List<DetectionOutline.Part> outline;
        }
        readonly List<DecoyView> decoys=new List<DecoyView>();
        int nextDecoyId;
        DetectionOutline detectionOutline;
        public int DecoyCharges {get;private set;}
        public int PlacedDecoyCount => decoys.Count;
        public bool TryPlaceDecoy()
        {
            if(!session.IsAuthority || session.Finished || session.ReporterDying || session.Reporter.Hidden || session.Reporter.PhoneOpen || session.TaskWorld?.Held || (!session.IsTraining && DecoyCharges<=0)) return false;
            if(!session.IsTraining) DecoyCharges--;
            CreateDecoy(new DecoySnapshot{id=++nextDecoyId,position=session.Reporter.transform.position,yaw=session.Reporter.transform.eulerAngles.y,crouched=session.Reporter.Crouched});
            session.Notice(session.IsTraining?"假人已放置，大厅练习不消耗次数。":"假人已放置，剩余 "+DecoyCharges+" 次。");return true;
        }
        void CreateDecoy(DecoySnapshot state)
        {
            var source=session.Reporter.Appearance.gameObject;
            var model=Instantiate(source,session.transform);model.name="狗仔假人 "+state.id;
            foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
            foreach(var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)) {body.isKinematic=true;body.useGravity=false;}
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.enabled=true;
            foreach(var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=PrototypeLayers.Get("UA_Props");
            model.transform.SetPositionAndRotation(state.position,Quaternion.Euler(0,state.yaw,0));
            var posture=model.transform.Find("Posture");if(posture) {posture.localPosition=Vector3.zero;posture.localRotation=Quaternion.identity;}
            var view=new DecoyView{state=state,model=model};decoys.Add(view);
            if(detectionOutline) view.outline=detectionOutline.AddTarget(model.transform,()=>view.model && view.state.revealed>0);
        }
        public DecoySnapshot[] CaptureDecoys() => decoys.Select(v=>v.state).ToArray();
        void RemoveDecoy(DecoyView view)
        {
            if(detectionOutline) detectionOutline.RemoveTarget(view.outline);
            if(view.model) Destroy(view.model);decoys.Remove(view);
        }
        void ApplyDecoys(MatchSnapshot state)
        {
            DecoyCharges=Mathf.Max(0,state.decoyCharges);
            var received=state.decoys??Array.Empty<DecoySnapshot>();
            foreach(var view in decoys.ToArray()) if(!received.Any(s=>s.id==view.state.id)) RemoveDecoy(view);
            foreach(var value in received)
            {
                if(value.id<=0) continue;
                var view=decoys.Find(v=>v.state.id==value.id);
                if(view==null) CreateDecoy(value);else view.state=value;
            }
        }
        void TickDecoys(float dt,bool detecting)
        {
            foreach(var view in decoys.ToArray())
            {
                view.state.revealed=Mathf.Max(0,view.state.revealed-dt);
                if(!session.IsAuthority) continue;
                if(detecting && InCone(view.state.position+Vector3.up*(view.state.crouched?.7f:1.3f))) view.state.revealed=Rules.outlineSeconds;
            }
            ConsumeTouchingDecoys();
        }
        public bool ConsumeTouchingDecoys()
        {
            if(!session.IsAuthority || session.Finished) return false;
            bool touched=false;
            foreach(var view in decoys.ToArray())
            {
                Vector3 delta=session.Streamer.transform.position-view.state.position;
                if(Mathf.Abs(delta.y)>.9f || new Vector2(delta.x,delta.z).magnitude>1.1f ||
                    Physics.Linecast(session.Streamer.View.transform.position,view.state.position+Vector3.up,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore)) continue;
                touched=true;RemoveDecoy(view);
                if(!session.IsTraining && (session.Streamer.State==StreamerState.Roaming || session.Streamer.State==StreamerState.Ambush))
                    session.Streamer.Controller.ExhaustHuntByDecoy();
                session.Notice("主播碰到了假人，假人已消失。",3);
            }
            return touched;
        }
        bool InCone(Vector3 point)
        {
            var eye=session.Streamer.View.transform;Vector3 delta=point-eye.position;
            return delta.magnitude<=DetectionDistance && Vector3.Angle(eye.forward,delta)<=session.Settings.streamerVision.outerAngle*.5f;
        }
        public void RewardTask(TaskReward reward=TaskReward.None)
        {
            if(!session.IsAuthority || session.IsTraining) return;
            if(reward==TaskReward.Invisibility && InvisibleCharges<int.MaxValue) InvisibleCharges++;
            if(reward==TaskReward.Decoy && DecoyCharges<int.MaxValue) DecoyCharges++;
        }
    }
}
