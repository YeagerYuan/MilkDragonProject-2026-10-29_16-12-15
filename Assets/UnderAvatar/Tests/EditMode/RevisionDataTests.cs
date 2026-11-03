using NUnit.Framework;
using UnityEngine;

namespace UnderAvatar.Tests
{
    public sealed class RevisionDataTests
    {
        [Test] public void PrefabReplacesDoorVisualWithoutReplacingGameplayColliderOrSourceAsset()
        {
            var catalog=LevelObjectCatalog.Default;Assert.That(catalog,Is.Not.Null);
            var model=GameObject.CreatePrimitive(PrimitiveType.Cube);var root=new GameObject("Prefab test door");var palette=new PrototypeVisuals();
            model.transform.localScale=new Vector3(2,1,.5f);
            var definition=new LevelObjectDefinition{id="test_door_visual",type=ItemType.Door,prefab=model,rotation=new Vector3(0,30,0)};catalog.objects.Add(definition);
            try
            {
                var item=root.AddComponent<PlacedItemView>();item.Initialize(new PlacedItemData{type=ItemType.Door,definitionId=definition.id,rotationY=90},0,2,4,palette);
                var visual=root.transform.Find("DoorHinge/CustomVisual_新物体");Assert.That(visual,Is.Not.Null);
                Assert.That(visual.GetComponentInChildren<Collider>().enabled,Is.False);Assert.That(model.GetComponent<Collider>().enabled,Is.True);
                var leaf=root.transform.Find("DoorHinge/DoorLeaf");Assert.That(leaf.GetComponent<Collider>().enabled,Is.True);
                Assert.That(Vector3.Distance(visual.GetComponentInChildren<Renderer>().bounds.size,leaf.GetComponent<Renderer>().bounds.size),Is.LessThan(.001f),"Fit rotated prefab bounds in the door's coordinates.");
                Assert.That(visual.GetChild(0).localScale,Is.EqualTo(model.transform.localScale),"Keep the prefab's authored scale.");
                Assert.That(leaf.GetComponent<Renderer>().enabled,Is.False);item.SetDoorOpen(true);
                Assert.That(item.IsOpen,Is.True);Assert.That(leaf.GetComponent<Collider>().enabled,Is.False);
                Assert.That(Quaternion.Angle(visual.parent.localRotation,Quaternion.Euler(0,-100,0)),Is.LessThan(.01f));
            }
            finally { catalog.objects.Remove(definition);Object.DestroyImmediate(root);Object.DestroyImmediate(model);palette.Dispose(); }
        }
        [Test] public void LegacyWardrobeJsonRetainsIdAndBehaviourWithoutMigration()
        {
            const string json="{\"version\":1,\"name\":\"旧关卡\",\"floorHeight\":4,\"gridSize\":2,\"halfWidth\":8,\"halfDepth\":6,\"floors\":[{\"floorIndex\":0,\"items\":[{\"id\":\"old_wardrobe\",\"type\":2,\"gridX\":1,\"gridZ\":2,\"rotationY\":90,\"hideable\":true}]}]}";
            var data=BuildingData.FromJson(json);var item=data.Copy().floors[0].items[0];
            Assert.That(item.id,Is.EqualTo("old_wardrobe"));Assert.That(item.IsHidingPlace,Is.True);Assert.That(item.rotationY,Is.EqualTo(90));
            Assert.That((int)ItemType.Stairs,Is.EqualTo(5));Assert.That((int)ItemType.Bed,Is.EqualTo(6));
        }
        [Test] public void CustomObjectsAndTuningSurviveJsonWhileRoundTargetsDoNotChangePhysics()
        {
            var map=BuildingData.CreateDefault();map.floors[0].items.Add(new PlacedItemData{id="custom",type=ItemType.Custom,definitionId="lamp",displayName="落地灯",gridX=-2,gridZ=-2,customSize=new Vector3(.5f,1.7f,.5f)});
            var copy=map.Copy().At(0,-2,-2);Assert.That(copy.definitionId,Is.EqualTo("lamp"));Assert.That(copy.displayName,Is.EqualTo("落地灯"));Assert.That(copy.customSize.y,Is.EqualTo(1.7f));
            var rules=new MatchSettings();rules.broadcastTargets.Add(new BroadcastTarget{broadcast=3,score=8});rules.flappyRules.pipeSpacing=.6f;
            var received=JsonUtility.FromJson<MatchSettings>(JsonUtility.ToJson(rules));
            Assert.That(received.TargetForBroadcast(2),Is.EqualTo(15));Assert.That(received.TargetForBroadcast(3),Is.EqualTo(8));Assert.That(received.flappyRules.pipeSpacing,Is.EqualTo(.6f));
        }
    }
}
