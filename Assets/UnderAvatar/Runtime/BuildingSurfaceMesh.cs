using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    // Geometry is still a unit box with the original BoxCollider. UVs use metres, not stretched cube UVs.
    public static class BuildingSurfaceMesh
    {
        public static Mesh Create(Transform box,BuildingSurfaceKind kind,BuildingSurfaceProfile profile,float floorBase,float floorHeight)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();
            var triangles=new List<int>[profile.Materials(kind).Length];
            for(int i=0;i<triangles.Length;i++) triangles[i]=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,int slot)
            {
                int start=vertices.Count;float metres=profile.Meters(kind,slot);
                var worldNormal=box.TransformDirection(normal);
                foreach(var point in new[]{a,b,c,d})
                {
                    vertices.Add(point);normals.Add(normal);var world=box.TransformPoint(point);
                    uv.Add(Mathf.Abs(worldNormal.y)>.5f?new Vector2(world.x,world.z)/metres:
                        new Vector2(Mathf.Abs(worldNormal.x)>.5f?world.z:world.x,world.y-floorBase)/metres);
                }
                triangles[slot].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            int top=0,bottom=kind==BuildingSurfaceKind.Floor?1:0,side=kind==BuildingSurfaceKind.Floor?2:0;
            Face(new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(.5f,.5f,-.5f),Vector3.up,top);
            Face(new Vector3(-.5f,-.5f,.5f),new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,-.5f,.5f),Vector3.down,bottom);
            void Sides(float low,float high,int slot)
            {
                if(high-low<.00001f) return;
                Face(new Vector3(-.5f,low,-.5f),new Vector3(-.5f,high,-.5f),new Vector3(.5f,high,-.5f),new Vector3(.5f,low,-.5f),Vector3.back,slot);
                Face(new Vector3(.5f,low,.5f),new Vector3(.5f,high,.5f),new Vector3(-.5f,high,.5f),new Vector3(-.5f,low,.5f),Vector3.forward,slot);
                Face(new Vector3(.5f,low,-.5f),new Vector3(.5f,high,-.5f),new Vector3(.5f,high,.5f),new Vector3(.5f,low,.5f),Vector3.right,slot);
                Face(new Vector3(-.5f,low,.5f),new Vector3(-.5f,high,.5f),new Vector3(-.5f,high,-.5f),new Vector3(-.5f,low,-.5f),Vector3.left,slot);
            }
            if(kind==BuildingSurfaceKind.Wall)
            {
                float division=box.InverseTransformPoint(new Vector3(box.position.x,floorBase+floorHeight*profile.wainscotFraction,box.position.z)).y;
                division=Mathf.Clamp(division,-.5f,.5f);Sides(-.5f,division,1);Sides(division,.5f,0);
            }
            else Sides(-.5f,.5f,side);
            var mesh=new Mesh{name="BuildingSurface_"+kind,hideFlags=HideFlags.DontSave};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
    }
}
