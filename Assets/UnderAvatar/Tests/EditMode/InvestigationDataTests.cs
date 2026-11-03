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
