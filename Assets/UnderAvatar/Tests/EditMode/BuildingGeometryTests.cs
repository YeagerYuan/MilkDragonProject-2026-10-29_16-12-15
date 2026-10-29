using System;
using System.Linq;
using NUnit.Framework;
using UnderAvatar.Editor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace UnderAvatar.Tests
{
    public sealed class BuildingGeometryTests
    {
        [TestCase(3.5f)]
        [TestCase(4f)]
        [TestCase(6f)]
        public void WallsStairsLandingsAndFloorOpeningsShareTheConfiguredHeight(float height)
        {
            var data=new BuildingData{floorHeight=height};
            data.floors.Add(new FloorData{floorIndex=0});
            data.floors.Add(new FloorData{floorIndex=1});
            data.floors[0].items.Add(new PlacedItemData{type=ItemType.Stairs});
            data.floors[0].items.Add(new PlacedItemData{type=ItemType.Wall,gridX=-4});
            var root=new GameObject("GeometryTest"); var world=root.AddComponent<BuildingWorld>();
            try
            {
                world.Rebuild(data); world.SetMode(false,0);
                var stair=world.Stairs[0];
                var view=world.Find(stair.Item.id);
                var wall=world.Items.First(i=>i.Data.type==ItemType.Wall);
                Assert.That(wall.GetComponentInChildren<Renderer>().bounds.size.y,Is.EqualTo(height).Within(.001f));
                Assert.That(view.transform.Find("UpperLanding").GetComponent<Collider>().bounds.max.y,Is.EqualTo(height).Within(.001f));
                Assert.That(Physics.RaycastAll(stair.WorldPoint(data.gridSize,height+.4f,0),Vector3.down,.6f,PrototypeLayers.SolidMask).Length,Is.EqualTo(1),"Landing must not overlap a second floor slab");
                Assert.That(view.transform.Find("MiddleLanding").GetComponent<Collider>().bounds.max.y,Is.EqualTo(height*.5f).Within(.001f));
                var steps=view.GetComponentsInChildren<Collider>().Where(c=>c.name.StartsWith("LowerStep") || c.name.StartsWith("UpperStep")).ToArray();
                Assert.That(steps.All(c=>c.enabled),Is.True);
                Assert.That(steps.Max(c=>c.bounds.max.y),Is.EqualTo(height).Within(.001f));
                // A downward ray in the opening reaches the flight below, not an uncut upper slab.
                Vector3 opening=stair.WorldPoint(0,height-.01f,data.gridSize*1.5f);
                Assert.That(Physics.Raycast(opening,Vector3.down,out var hit,height,PrototypeLayers.SolidMask),Is.True);
                Assert.That(hit.point.y,Is.LessThan(height-.4f));
                Assert.That(world.FloorSlabs(1).All(r=>r.enabled),Is.True);
                Assert.That(Physics.Raycast(new Vector3(-4,height+.5f,-4),Vector3.down,out hit,1,PrototypeLayers.SolidMask),Is.True);
                Assert.That(hit.point.y,Is.EqualTo(height).Within(.01f));
            }
            finally { world.ReleaseMaterials(); Object.DestroyImmediate(root); }
        }

        [Test] public void NewFloorHasVisibleFullSizeFloorAndDimensionsSupportUndo()
        {
            var asset=ScriptableObject.CreateInstance<BuildingAsset>();
            var root=new GameObject("NewFloorTest"); var world=root.AddComponent<BuildingWorld>();
            try
            {
                Undo.IncrementCurrentGroup();
                BuildingEditorModel.SetDimensions(asset,5,21,17);
                BuildingEditorModel.AddFloor(asset);
                world.Rebuild(asset.CreateRuntimeCopy()); world.SetMode(true,2);
                var slabs=world.FloorSlabs(2);
                Assert.That(slabs.All(r=>r.enabled && r.gameObject.activeInHierarchy),Is.True);
                var bounds=slabs[0].bounds; foreach(var slab in slabs) bounds.Encapsulate(slab.bounds);
                Assert.That(bounds.size.x,Is.EqualTo(42).Within(.001f));
                Assert.That(bounds.size.z,Is.EqualTo(34).Within(.001f));
                Assert.That(bounds.max.y,Is.EqualTo(10).Within(.001f));
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup()); Undo.IncrementCurrentGroup(); Undo.PerformUndo();
                Assert.That(asset.Data.floorHeight,Is.EqualTo(4));
                Assert.That(asset.Data.floors.Count,Is.EqualTo(2));
                string before=asset.Data.ToJson();
                Assert.Throws<ArgumentException>(()=>BuildingEditorModel.SetDimensions(asset,4,7,7));
                Assert.That(asset.Data.ToJson(),Is.EqualTo(before));
            }
            finally { Undo.ClearUndo(asset); world.ReleaseMaterials(); Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }

        [Test] public void UpperStairOpeningCannotBeFilledByAnotherItem()
        {
            var asset=ScriptableObject.CreateInstance<BuildingAsset>();
            try
            {
                string before=asset.Data.ToJson();
                var layout=StairLayout.All(asset.Data)[0]; var cell=layout.Opening().First();
                Assert.Throws<ArgumentException>(()=>BuildingEditorModel.Place(asset,cell.y,cell.x,cell.z,ItemType.Furniture,0,false));
                Assert.That(asset.Data.ToJson(),Is.EqualTo(before));
            }
            finally { Undo.ClearUndo(asset); Object.DestroyImmediate(asset); }
        }
    }
}
