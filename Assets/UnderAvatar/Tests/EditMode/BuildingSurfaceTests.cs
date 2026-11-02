using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace UnderAvatar.Tests
{
    public sealed class BuildingSurfaceTests
    {
        [Test] public void WallBandAndTextureDensityFollowMetresAcrossFloorsAndDifferentWallLengths()
        {
            var profile=BuildingSurfaceProfile.Default;Assert.That(profile && profile.Ready,Is.True);
            var root=new GameObject("Surface wall test");var palette=new PrototypeVisuals();
            try
            {
                root.transform.position=Vector3.up*6;
                var wall=palette.SurfaceBox(root.transform,"Wall",new Vector3(0,3,0),new Vector3(8,6,2),Color.white,BuildingSurfaceKind.Wall,6,6);
                var mesh=wall.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
                float bandTop=mesh.GetTriangles(1).Select(i=>wall.transform.TransformPoint(vertices[i]).y).Max();
                Assert.That(bandTop,Is.EqualTo(6+6*profile.wainscotFraction).Within(.001));
                // A front-facing 8 metre wall must span 8 metres of texture coordinates, not one stretched tile.
                var front=Enumerable.Range(0,vertices.Length).Where(i=>mesh.normals[i]==Vector3.back).ToArray();
                var uv=mesh.uv;float span=front.Max(i=>uv[i].x)-front.Min(i=>uv[i].x);
                Assert.That(span,Is.EqualTo(8/profile.wallMeters).Within(.001));
                Assert.That(wall.GetComponent<BoxCollider>().size,Is.EqualTo(Vector3.one));
                Physics.SyncTransforms();
                Assert.That(wall.GetComponent<Collider>().bounds.size,Is.EqualTo(new Vector3(8,6,2)));
            }
            finally { Object.DestroyImmediate(root);palette.Dispose(); }
        }
        [Test] public void FloorAndCeilingMaterialsSurviveEditorFloorSwitchAndRoofOnlyShowsInPlay()
        {
            var root=new GameObject("Floor surface test");var world=root.AddComponent<BuildingWorld>();
            try
            {
                world.Rebuild(BuildingData.CreateDefault());world.SetMode(false,0);
                var floor=world.FloorSlabs(0)[0];var originals=floor.sharedMaterials;
                Assert.That(originals[0],Is.EqualTo(BuildingSurfaceProfile.Default.floor));
                Assert.That(originals[1],Is.EqualTo(BuildingSurfaceProfile.Default.ceiling));
                var roof=root.transform.Find("TopCeiling");Assert.That(roof.gameObject.activeSelf,Is.True);
                Assert.That(roof.GetComponent<Collider>().enabled,Is.False,"Ceiling skin must not change navigation colliders");
                world.SetMode(true,1);Assert.That(roof.gameObject.activeSelf,Is.False);
                Assert.That(floor.sharedMaterials[0].mainTexture,Is.EqualTo(originals[0].mainTexture));
                world.SetMode(false,0);Assert.That(floor.sharedMaterials,Is.EqualTo(originals));
                var wall=world.Items.First(i=>i.Data.type==ItemType.Wall).GetComponentInChildren<Renderer>();
                Assert.That(wall.sharedMaterials,Is.EqualTo(new[]{BuildingSurfaceProfile.Default.upperWall,BuildingSurfaceProfile.Default.lowerWall}));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void SurfaceFacesPointOutwardAndFloorTilesAlignAcrossAdjacentStrips()
        {
            var root=new GameObject("Surface seam test");var palette=new PrototypeVisuals();
            try
            {
                var a=palette.SurfaceBox(root.transform,"A",new Vector3(0,-.12f,0),new Vector3(20,.24f,2),Color.white,BuildingSurfaceKind.Floor);
                var b=palette.SurfaceBox(root.transform,"B",new Vector3(0,-.12f,2),new Vector3(20,.24f,2),Color.white,BuildingSurfaceKind.Floor);
                var am=a.GetComponent<MeshFilter>().sharedMesh;var bm=b.GetComponent<MeshFilter>().sharedMesh;
                for(int i=0;i<4;i++)
                    for(int j=0;j<4;j++)
                        if(Vector3.Distance(a.transform.TransformPoint(am.vertices[i]),b.transform.TransformPoint(bm.vertices[j]))<.001f)
                            Assert.That(am.uv[i],Is.EqualTo(bm.uv[j]));
                var indices=am.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var normal=Vector3.Cross(am.vertices[indices[i+1]]-am.vertices[indices[i]],am.vertices[indices[i+2]]-am.vertices[indices[i]]).normalized;
                    Assert.That(Vector3.Dot(normal,am.normals[indices[i]]),Is.GreaterThan(.99f));
                }
            }
            finally { Object.DestroyImmediate(root);palette.Dispose(); }
        }
    }
}
