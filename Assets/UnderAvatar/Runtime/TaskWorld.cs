using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    public sealed class TaskProp : MonoBehaviour
    {
        public TaskPropData Data { get; internal set; }
        public BoxCollider Body { get; internal set; }
        public Vector3 PhotoPoint => transform.TransformPoint(Body.center);
        internal Vector3 goalPosition,goalRotation;
    }
    public sealed class TaskClue : MonoBehaviour { public string Id; }
    public sealed class TaskWorld : MonoBehaviour
    {
        public readonly List<TaskProp> Props=new List<TaskProp>();
        public string HeldId { get; private set; }
        public TaskProp Held => Props.Find(p=>p.Data.id==HeldId);
        MatchSession session;
        BuildingData map;
        PrototypeVisuals palette;
        readonly List<DocumentData> documents=new List<DocumentData>();
        bool editing;
        GameObject placementPreview;
        int predictionSequence;
        public void Initialize(BuildingData data,MatchSession match=null)
        {
            map=data;session=match;editing=!match;palette=new PrototypeVisuals();
            foreach(var p in map.taskProps)
            {
                var go=new GameObject(p.name);go.transform.SetParent(transform,false);go.transform.position=p.position;go.transform.eulerAngles=p.rotation;
                go.layer=PrototypeLayers.Get("UA_Target");
                var prop=go.AddComponent<TaskProp>();prop.Data=p;prop.goalPosition=p.position;prop.goalRotation=p.rotation;
                prop.Body=go.AddComponent<BoxCollider>();prop.Body.size=p.size;prop.Body.center=Vector3.up*p.size.y*.5f;
                var model=LevelObjectCatalog.Default?.objects.Find(d=>d.id==p.definitionId)?.prefab;
                if(model)
                {
                    var visual=Instantiate(model,go.transform,false);visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
                    foreach(var b in visual.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
                    foreach(var b in visual.GetComponentsInChildren<Collider>(true)) b.enabled=false;
                    foreach(var b in visual.GetComponentsInChildren<Rigidbody>(true)) {b.isKinematic=true;b.useGravity=false;}
                    foreach(var b in visual.GetComponentsInChildren<Camera>(true)) b.enabled=false;
                    foreach(var b in visual.GetComponentsInChildren<AudioListener>(true)) b.enabled=false;
                    var rs=visual.GetComponentsInChildren<Renderer>();
                    if(rs.Length>0)
                    {
                        var bounds=rs[0].bounds;foreach(var r in rs.Skip(1)) bounds.Encapsulate(r.bounds);
                        float scale=Mathf.Min(p.size.x/Mathf.Max(.001f,bounds.size.x),p.size.y/Mathf.Max(.001f,bounds.size.y),p.size.z/Mathf.Max(.001f,bounds.size.z));
                        visual.transform.localScale*=scale;
                        bounds=rs[0].bounds;foreach(var r in rs.Skip(1)) bounds.Encapsulate(r.bounds);
                        visual.transform.position+=go.transform.TransformPoint(prop.Body.center)-bounds.center;
                    }
                    foreach(var node in visual.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=go.layer;
                }
                else palette.Box(go.transform,"道具外观",prop.Body.center,p.size,new Color(.67f,.45f,.22f),"UA_Target",false);
                Props.Add(prop);
            }
            foreach(var clue in map.taskClues)
            {
                var go=new GameObject(clue.title);go.transform.SetParent(transform,false);go.transform.position=clue.position;go.transform.eulerAngles=clue.rotation;
                go.layer=PrototypeLayers.Get("UA_Props");go.AddComponent<TaskClue>().Id=clue.id;
                palette.Box(go.transform,"纸张",Vector3.zero,new Vector3(.45f,.55f,.012f),new Color(.85f,.78f,.56f),"UA_Props",true);
                var doc=ScriptableObject.CreateInstance<DocumentData>();doc.documentName=clue.title;doc.text=clue.text;documents.Add(doc);
                go.AddComponent<DocumentInteractable>().Initialize(doc);
            }
        }
        public void SetEditorFloor(int floor)
        {
            foreach(Transform child in transform)
            {
                int f=Mathf.Max(0,Mathf.FloorToInt((child.position.y+.01f)/map.floorHeight));
                child.gameObject.SetActive(f<=floor && f<map.floors.Count);
            }
        }
        public TaskProp AimProp()
        {
            var camera=session.Reporter.View;
            return Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,3f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore)?hit.collider.GetComponentInParent<TaskProp>():null;
        }
        public bool Interact(bool predict=false,int sequence=0)
        {
            if(!session || session.Finished || session.Reporter.Hidden) return false;
            if(Held)
            {
                if(TryPlacement(out var position,out var yaw))
                {
                    var held=Held;held.transform.position=position;held.transform.rotation=Quaternion.Euler(0,yaw,0);
                    held.goalPosition=position;held.goalRotation=held.transform.eulerAngles;HeldId=null;held.Body.enabled=true;
                    if(predict) predictionSequence=sequence;
                    return true;
                }
                if(!predict) session.Notice("无法放置：请对准三米内的地板或桌面，留出足够空间。");
                return true;
            }
            if(session.Reporter.PhoneOpen) return false;
            var prop=AimProp();if(!prop || !prop.Data.portable) return false;
            HeldId=prop.Data.id;prop.Body.enabled=false;
            if(predict) predictionSequence=sequence;
            return true;
        }
        public bool TryPlacement(out Vector3 bottom,out float yaw)
        {
            bottom=Vector3.zero;yaw=session.Reporter.transform.eulerAngles.y;
            if(!Held) return false;
            var camera=session.Reporter.View;
            if(!Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,3.2f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore) || hit.normal.y<.85f) return false;
            bottom=hit.point+Vector3.up*.015f;
            Vector3 half=Held.Data.size*.5f;
            var overlaps=Physics.OverlapBox(bottom+Vector3.up*half.y,half*.97f,Quaternion.Euler(0,yaw,0),PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore);
            if(overlaps.Any(c=>c!=Held.Body)) return false;
            // Do not permit placing an obstacle through either player's body.
            foreach(var actor in new[]{session.Reporter.transform,session.Streamer.transform})
                if(Vector3.Distance(new Vector3(bottom.x,actor.position.y,bottom.z),actor.position)<Mathf.Max(half.x,half.z)+.35f && Mathf.Abs(bottom.y-actor.position.y)<1.8f) return false;
            return true;
        }
        public TaskPropSnapshot[] Capture() => Props.Select(p=>new TaskPropSnapshot{id=p.Data.id,position=p.transform.position,rotation=p.transform.eulerAngles}).ToArray();
        public void Apply(TaskPropSnapshot[] props,string held,int ack)
        {
            if(props==null) return;
            bool predicting=predictionSequence>ack && session.Role==TestRole.Reporter;
            if(!predicting) {HeldId=held;predictionSequence=0;}
            foreach(var state in props)
            {
                var prop=Props.Find(p=>p.Data.id==state.id);if(!prop) continue;
                if(predicting && (state.id==held || state.id==HeldId)) continue;
                prop.goalPosition=state.position;prop.goalRotation=state.rotation;prop.Body.enabled=state.id!=HeldId;
            }
        }
        void LateUpdate()
        {
            if(editing || !session) return;
            bool show=Held && session.Role==TestRole.Reporter && !session.Finished;
            if(show)
            {
                bool valid=TryPlacement(out var bottom,out var yaw);
                if(!placementPreview) placementPreview=palette.Box(transform,"本地放置预览",Vector3.zero,Vector3.one,Color.green,"UA_Target",false,true);
                placementPreview.SetActive(true);placementPreview.transform.localScale=Held.Data.size;
                placementPreview.transform.position=(valid?bottom:session.Reporter.View.transform.position+session.Reporter.View.transform.forward*2)+Vector3.up*Held.Data.size.y*.5f;
                placementPreview.transform.rotation=Quaternion.Euler(0,yaw,0);
                placementPreview.GetComponent<Renderer>().sharedMaterial=palette.Material(valid?new Color(.2f,1,.4f,.3f):new Color(1,.2f,.2f,.3f),true);
            }
            else if(placementPreview) placementPreview.SetActive(false);
            if(Held)
            {
                var view=session.Reporter.View.transform;
                Held.transform.position=view.position+view.forward*.85f-view.up*.3f;
                Held.transform.rotation=Quaternion.Euler(0,view.eulerAngles.y,0);
                Held.Body.enabled=false;
            }
            if(!session.IsAuthority) foreach(var p in Props.Where(p=>p.Data.id!=HeldId))
            {
                p.transform.position=Vector3.Lerp(p.transform.position,p.goalPosition,1-Mathf.Exp(-20*Time.deltaTime));
                p.transform.rotation=Quaternion.Slerp(p.transform.rotation,Quaternion.Euler(p.goalRotation),1-Mathf.Exp(-20*Time.deltaTime));
            }
        }
        void OnDestroy()
        {
            palette?.Dispose();foreach(var doc in documents) if(doc) PrototypeVisuals.Release(doc);
        }
    }
}
