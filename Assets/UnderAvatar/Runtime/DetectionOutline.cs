using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    // Draw a separate silhouette from the animated mesh, even while its normal renderer is invisible.
    public sealed class DetectionOutline : MonoBehaviour
    {
        public sealed class Part
        {
            public Renderer renderer;
            public Mesh mesh;
            public bool baked;
            public Func<bool> show;
            public bool Visible => show==null || show();
            public Matrix4x4 Matrix => renderer.localToWorldMatrix;
        }
        public readonly List<Part> Parts=new List<Part>();
        MatchSession session;
        Func<bool> revealed;
        public bool Visible => session && session.IsStreamerPlayer && !session.Finished && Parts.Exists(p=>p.renderer && p.Visible);
        public void Initialize(MatchSession match,Func<bool> reveal)
        {
            session=match;revealed=reveal;
            AddTarget(match.Reporter.Appearance.Model,()=>!match.Reporter.Hidden && !match.ReporterDying && revealed!=null && revealed());
        }
        public List<Part> AddTarget(Transform model,Func<bool> show)
        {
            var added=new List<Part>();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled) continue;
                if(renderer is SkinnedMeshRenderer skin && skin.sharedMesh)
                    added.Add(new Part{renderer=renderer,mesh=new Mesh{name="Detection silhouette"},baked=true,show=show});
                else if(renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                    added.Add(new Part{renderer=renderer,mesh=filter.sharedMesh,show=show});
            }
            Parts.AddRange(added);return added;
        }
        public void RemoveTarget(List<Part> parts)
        {
            if(parts==null) return;
            foreach(var part in parts) {Parts.Remove(part);if(part.baked && part.mesh) Destroy(part.mesh);}
        }
        public void Prepare()
        {
            foreach(var part in Parts)
                if(part.baked && part.renderer) ((SkinnedMeshRenderer)part.renderer).BakeMesh(part.mesh);
        }
        void OnDestroy() {foreach(var part in Parts) if(part.baked && part.mesh) Destroy(part.mesh);}
    }
}
