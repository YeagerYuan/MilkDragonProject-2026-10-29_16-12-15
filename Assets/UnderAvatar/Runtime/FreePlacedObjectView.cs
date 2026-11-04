using System;
using UnityEngine;

namespace UnderAvatar
{
    // Static decoration: no task, pickup, hiding or grid occupancy behaviour.
    public sealed class FreePlacedObjectView : MonoBehaviour
    {
        public FreePlacedObjectData Data { get; private set; }
        public int Floor { get; private set; }
        public void Initialize(FreePlacedObjectData data,int floor)
        {
            Data=data;Floor=floor;gameObject.name=data.name;
            var catalog=LevelObjectCatalog.Default;
            var definition=catalog?catalog.objects.Find(d=>d.id==data.definitionId):null;
            if(definition==null || !definition.prefab) throw new InvalidOperationException("自由摆放物体缺少模型："+data.name+"。请在自由摆放窗口重新指定模型。");
            var layer=PrototypeLayers.Get("UA_Props");gameObject.layer=layer;
            var visual=Instantiate(definition.prefab,transform,false);
            visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=definition.prefab.transform.localRotation;visual.transform.localScale=definition.prefab.transform.localScale;
            foreach(var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
            foreach(var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            foreach(var body in visual.GetComponentsInChildren<Rigidbody>(true)) {body.isKinematic=true;body.useGravity=false;}
            foreach(var source in visual.GetComponentsInChildren<AudioSource>(true)) source.enabled=false;
            foreach(var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled=false;
            foreach(var listener in visual.GetComponentsInChildren<AudioListener>(true)) listener.enabled=false;
            foreach(var light in visual.GetComponentsInChildren<Light>(true)) light.enabled=false;
            foreach(var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
            foreach(var animation in visual.GetComponentsInChildren<Animation>(true)) animation.enabled=false;
            foreach(var node in visual.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=layer;
            var renderers=visual.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) throw new InvalidOperationException("自由摆放模型没有可见网格："+data.name);
            var bounds=BoundsOf(renderers);
            // Uniform fit preserves model proportions; size defines the enclosing box.
            float scale=Mathf.Min(data.size.x/Mathf.Max(.001f,bounds.size.x),data.size.y/Mathf.Max(.001f,bounds.size.y),data.size.z/Mathf.Max(.001f,bounds.size.z));
            visual.transform.localScale*=scale;
            bounds=BoundsOf(renderers);
            visual.transform.localPosition+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
            if(data.solid)
            {
                bounds=BoundsOf(renderers);
                var box=gameObject.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
            }
        }
        Bounds BoundsOf(Renderer[] renderers)
        {
            var result=new Bounds();bool first=true;
            foreach(var renderer in renderers)
            {
                var b=renderer.localBounds;
                for(int i=0;i<8;i++)
                {
                    var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point=transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if(first) {result=new Bounds(point,Vector3.zero);first=false;} else result.Encapsulate(point);
                }
            }
            return result;
        }
    }
}
