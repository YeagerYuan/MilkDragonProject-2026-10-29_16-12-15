using System;
using System.IO;
using NUnit.Framework;
using UnderAvatar.Editor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace UnderAvatar.Tests
{
    public sealed class InvestigationDataTests
    {
        [Test] public void TaskEditorMenuOpensTheWindowAndLiveRequirementSurvivesMapCopies()
        {
            Assert.That(Attribute.IsDefined(typeof(TaskEditorWindow).GetMethod("Open"),typeof(MenuItem)),Is.True);
            Assert.That(Attribute.IsDefined(typeof(TaskEditorWindow).GetMethod("EndSceneEditing"),typeof(MenuItem)),Is.False);
            var map=BuildingData.CreateDefault();Assert.That(map.requireLiveEvidence,Is.True);
            map.requireLiveEvidence=false;Assert.That(map.Copy().requireLiveEvidence,Is.False);
            var current=AssetDatabase.LoadAssetAtPath<BuildingAsset>("Assets/UnderAvatar/Levels/DefaultBuilding.asset");
            Assert.That(current.Data.requireLiveEvidence,Is.False);
        }

        [Test] public void DecorationsShareGridCoordinatesWithoutBecomingTasksAndSurviveCopy()
        {
            var map=BuildingData.CreateDefault();int questCount=InvestigationData.Tasks(map).Count;
            var cabinet=map.floors[0].items.Find(i=>i.type==ItemType.Furniture);
            var position=map.Position(0,cabinet)+Vector3.up*2.5f;
            map.decorations.Add(new FreePlacedObjectData{definitionId="decoration-model",position=position,size=new Vector3(.2f,.3f,.4f),rotation=new Vector3(12,27,4)});
            map.decorations.Add(new FreePlacedObjectData{definitionId="decoration-model",position=position+Vector3.right*.3f,solid=false});
            Assert.DoesNotThrow(()=>PrototypeApp.ValidateMap(map));
            var copy=map.Copy();Assert.That(copy.decorations.Count,Is.EqualTo(2));
            Assert.That(copy.decorations[0].rotation,Is.EqualTo(new Vector3(12,27,4)));
            Assert.That(copy.decorations[0].position,Is.EqualTo(position));Assert.That(copy.decorations[1].solid,Is.False);
            Assert.That(InvestigationData.Tasks(copy).Count,Is.EqualTo(questCount));
            Assert.That(copy.At(0,cabinet.gridX,cabinet.gridZ).id,Is.EqualTo(cabinet.id));
            map.useTaskEditor=true;map.tasks.Add(new InvestigationTaskData{targetId=map.decorations[0].id});
            Assert.Throws<ArgumentException>(()=>map.Validate(),"Decorations are not automatically photo targets.");
            map.tasks.Clear();map.decorations[0].position=new Vector3(float.NaN,1,0);Assert.Throws<ArgumentException>(()=>map.Validate());
        }

        [Test] public void DecorationExportImportUndoAndFloorResizingPreservePlacement()
        {
            var asset=ScriptableObject.CreateInstance<BuildingAsset>();
            string path=Path.Combine(Application.temporaryCachePath,"ua-decor-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                BuildingEditorModel.Commit(asset,"添加装饰",d=>{foreach(var f in d.floors) f.items.RemoveAll(i=>i.type==ItemType.Stairs);d.decorations.Add(new FreePlacedObjectData{definitionId="model",position=new Vector3(2,4.7f,2)});});
                BuildingEditorModel.Export(asset,path);
                BuildingEditorModel.SetDimensions(asset,6,asset.Data.WidthCells,asset.Data.DepthCells);
                Assert.That(asset.Data.decorations[0].position.y,Is.EqualTo(6.7f).Within(.001f));
                Assert.Throws<InvalidOperationException>(()=>BuildingEditorModel.DeleteFloor(asset,1));
                BuildingEditorModel.Import(asset,path);Assert.That(asset.Data.decorations[0].position.y,Is.EqualTo(4.7f));
                Undo.IncrementCurrentGroup();BuildingEditorModel.Commit(asset,"移动装饰",d=>d.decorations[0].position+=Vector3.right);Undo.IncrementCurrentGroup();
                Undo.PerformUndo();Assert.That(asset.Data.decorations[0].position.x,Is.EqualTo(2));
                Undo.PerformRedo();Assert.That(asset.Data.decorations[0].position.x,Is.EqualTo(3));
            }
            finally {Undo.ClearUndo(asset);Object.DestroyImmediate(asset);File.Delete(path);}
        }

        [Test] public void DocumentTargetsSurviveCopyButCannotBeUsedAsCarryProps()
        {
            var map=BuildingData.CreateDefault();map.useTaskEditor=true;
            var task=new InvestigationTaskData{targetId="document:dog_photo"};map.tasks.Add(task);
            Assert.DoesNotThrow(()=>PrototypeApp.ValidateMap(map));
            Assert.That(map.Copy().tasks[0].targetId,Is.EqualTo(task.targetId));
            task.kind=InvestigationKind.PlaceAndPhotograph;Assert.Throws<ArgumentException>(()=>map.Validate());
            task.kind=InvestigationKind.Photograph;task.targetId="document:";Assert.Throws<ArgumentException>(()=>map.Validate());
        }

        [Test] public void AuthoredDogTaskUsesExistingReadableDocumentWithoutDuplicateProp()
        {
            var map=AssetDatabase.LoadAssetAtPath<BuildingAsset>("Assets/UnderAvatar/Levels/DefaultBuilding.asset");
            Assert.That(map.Data.tasks.Find(t=>t.id=="item_medicine").targetId,Is.EqualTo("document:dog_photo"));
            Assert.That(map.Data.taskProps.Exists(p=>p.id=="6479236095104e639bad77a17d127ce3"),Is.False);
            var prefab=Resources.Load<GameObject>("DeveloperDocuments");
            var documents=prefab.GetComponentsInChildren<DocumentInteractable>(true);
            var dog=System.Array.FindAll(documents,d=>d.PhotoTargetId=="document:dog_photo");
            Assert.That(dog.Length,Is.EqualTo(1));Assert.That(dog[0].Data,Is.Not.Null);
            for(var node=dog[0].transform;node!=null;node=node.parent) Assert.That(node.gameObject.activeSelf,Is.True,"Dog document must be enabled in the document layout.");
        }

        [Test] public void MovingStreamerAndComputerBindingsSurviveCopyAndRejectInvalidExtraTargets()
        {
            var map=BuildingData.CreateDefault();map.useTaskEditor=true;
            var task=new InvestigationTaskData{targetId=InvestigationData.StreamerTarget};
            task.additionalTargetIds.Add(InvestigationData.ComputerTarget);map.tasks.Add(task);
            Assert.DoesNotThrow(()=>PrototypeApp.ValidateMap(map));
            var copy=map.Copy().tasks[0];
            Assert.That(copy.targetId,Is.EqualTo(InvestigationData.StreamerTarget));
            Assert.That(copy.additionalTargetIds,Is.EqualTo(new[]{InvestigationData.ComputerTarget}));
            task.additionalTargetIds.Add("missing");Assert.Throws<ArgumentException>(()=>map.Validate());
            task.additionalTargetIds.Remove("missing");
            task.additionalTargetIds.Add(InvestigationData.StreamerTarget);Assert.Throws<ArgumentException>(()=>map.Validate());
        }

        BuildingData Example()
        {
            var data=BuildingData.CreateDefault();data.useTaskEditor=true;
            var prop=new TaskPropData{position=new Vector3(-4,.03f,-4)};data.taskProps.Add(prop);
            var clue=new TaskClueData{title="食物线索",text="第一段提示。\n第二段提示。"};data.taskClues.Add(clue);
            var task=new InvestigationTaskData{kind=InvestigationKind.PlaceAndPhotograph,targetId=prop.id};task.clueIds.Add(clue.id);data.tasks.Add(task);return data;
        }
        [Test] public void TaskBindingsRegionsAndClueTextSurviveMapJsonAndNativeUndo()
        {
            var asset=ScriptableObject.CreateInstance<BuildingAsset>();asset.SetData(Example());
            string path=Path.Combine(Application.temporaryCachePath,"ua-task-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                string before=asset.Data.ToJson();BuildingEditorModel.Export(asset,path);
                BuildingEditorModel.Commit(asset,"临时修改",d=>{d.tasks.Clear();d.taskProps.Clear();d.taskClues.Clear();d.useTaskEditor=false;});
                BuildingEditorModel.Import(asset,path);Assert.That(asset.Data.ToJson(),Is.EqualTo(before));
                Undo.IncrementCurrentGroup();BuildingEditorModel.Commit(asset,"移动任务区域",d=>d.tasks[0].regionPosition=new Vector3(5,1,2));Undo.IncrementCurrentGroup();
                Undo.PerformUndo();Assert.That(asset.Data.ToJson(),Is.EqualTo(before));
                Undo.PerformRedo();Assert.That(asset.Data.tasks[0].regionPosition,Is.EqualTo(new Vector3(5,1,2)));
            }
            finally {Undo.ClearUndo(asset);Object.DestroyImmediate(asset);File.Delete(path);}
        }
        [Test] public void RotatedRegionUsesTheObjectsBottomAndInvalidBindingsAreRejected()
        {
            var map=Example();var t=map.tasks[0];t.regionSize=new Vector3(4,1,1);t.regionPosition=Vector3.zero;t.regionYaw=90;
            Assert.That(t.Contains(new Vector3(0,.2f,1.5f)),Is.True);Assert.That(t.Contains(new Vector3(1.5f,.2f,0)),Is.False);
            map.taskProps[0].portable=false;Assert.Throws<ArgumentException>(()=>map.Validate());map.taskProps[0].portable=true;
            t.targetId="missing";Assert.Throws<ArgumentException>(()=>map.Validate());t.targetId="";
            Assert.DoesNotThrow(()=>map.Validate());Assert.Throws<InvalidOperationException>(()=>PrototypeApp.ValidateMap(map));
        }
        [Test] public void OldTaskImportKeepsObjectBindingsAndFloorHeightMovesAuthoredContent()
        {
            var asset=ScriptableObject.CreateInstance<BuildingAsset>();
            try
            {
                var imported=InvestigationData.Tasks(asset.Data);Assert.That(imported.Count,Is.EqualTo(3));
                BuildingEditorModel.Commit(asset,"导入任务",d=>{d.tasks=InvestigationData.Tasks(d);d.useTaskEditor=true;foreach(var f in d.floors) f.items.RemoveAll(i=>i.type==ItemType.Stairs);d.taskProps.Add(new TaskPropData{position=new Vector3(2,4.5f,2)});});
                BuildingEditorModel.SetDimensions(asset,6,asset.Data.WidthCells,asset.Data.DepthCells);
                Assert.That(asset.Data.taskProps[0].position.y,Is.EqualTo(6.5f));
                Assert.Throws<InvalidOperationException>(()=>BuildingEditorModel.DeleteFloor(asset,1));
                Assert.That(asset.Data.floors.Count,Is.EqualTo(2));
            }
            finally {Undo.ClearUndo(asset);Object.DestroyImmediate(asset);}
        }
    }
}
