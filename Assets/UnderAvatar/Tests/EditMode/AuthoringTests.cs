using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnderAvatar.Editor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace UnderAvatar.Tests
{
    public sealed class AuthoringTests
    {
        BuildingAsset asset;
        string folder;
        [SetUp] public void SetUp()
        {
            asset=ScriptableObject.CreateInstance<BuildingAsset>();
            folder="Assets/UnderAvatar/Tests/Temporary_"+Guid.NewGuid().ToString("N");
        }
        [TearDown] public void TearDown()
        {
            if(asset) Undo.ClearUndo(asset);
            if(AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            else if(asset) Object.DestroyImmediate(asset);
        }
        [Test] public void PlacementPropertiesFloorChangesAndUndoRedoAreNativeEditorOperations()
        {
            string before=asset.Data.ToJson();
            Undo.IncrementCurrentGroup();
            BuildingEditorModel.AddFloor(asset);
            string id=BuildingEditorModel.Place(asset,2,1,1,ItemType.QuestItem,90,false);
            BuildingEditorModel.EditItem(asset,id,item=>{item.questId="editor_evidence";item.questDescription="拍摄作者编辑的道具";});
            Assert.That(asset.Data.At(2,1,1).questId,Is.EqualTo("editor_evidence"));
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            Assert.That(asset.Data.ToJson(),Is.EqualTo(before));
            Undo.PerformRedo();
            Assert.That(asset.Data.At(2,1,1).questDescription,Is.EqualTo("拍摄作者编辑的道具"));
            BuildingEditorModel.DeleteFloor(asset,1);
            Assert.That(asset.Data.floors[1].floorIndex,Is.EqualTo(1));
            Assert.That(asset.Data.At(1,1,1).id,Is.EqualTo(id));
            BuildingEditorModel.Delete(asset,id);
            Assert.That(asset.Data.At(1,1,1),Is.Null);
        }
        [Test] public void InvalidPlacementOrDuplicateQuestLeavesAuthoredMapIntact()
        {
            string before=asset.Data.ToJson();
            Assert.Throws<ArgumentException>(()=>BuildingEditorModel.Place(asset,0,0,0,ItemType.Wall,0,false));
            var quest=asset.Data.floors.SelectMany(f=>f.items).First(i=>i.type==ItemType.QuestItem);
            var duplicate=asset.Data.floors.SelectMany(f=>f.items).Last(i=>i.type==ItemType.QuestItem);
            Assert.Throws<ArgumentException>(()=>BuildingEditorModel.EditItem(asset,quest.id,i=>i.questId=duplicate.questId));
            Assert.That(asset.Data.ToJson(),Is.EqualTo(before));
        }
        [Test] public void ScenePreviewExistsWithoutPlayFiltersFloorsAndCleansUp()
        {
            var previous=BuildingEditorPreview.Target;
            int floor=BuildingEditorPreview.Floor;
            var root=new GameObject("EditorPreviewTest");
            try
            {
                var app=root.AddComponent<PrototypeApp>(); app.Building=asset;
                BuildingEditorPreview.Bind(app);
                Assert.That(Application.isPlaying,Is.False);
                Assert.That(BuildingEditorPreview.Error,Is.Null);
                var world=BuildingEditorPreview.World;
                Assert.That(world.GetComponent<BuildingPreviewMarker>(),Is.Not.Null);
                Assert.That(world.Items.Where(i=>i.Floor==1).All(i=>!i.gameObject.activeInHierarchy),Is.True);
                BuildingEditorPreview.SetFloor(1);
                Assert.That(world.Items.All(i=>i.gameObject.activeInHierarchy),Is.True);
                Assert.That((world.gameObject.hideFlags & HideFlags.DontSaveInEditor)!=0,Is.True);
                BuildingEditorPreview.Clear();
                Assert.That(world==null,Is.True);
                Assert.That(asset.Data.floors.Count,Is.EqualTo(2));
            }
            finally
            {
                BuildingEditorPreview.Bind(previous); BuildingEditorPreview.SetFloor(floor);
                Object.DestroyImmediate(root);
            }
        }
        [Test] public void AssetAndJsonPersistAuthoringAndRejectBrokenImports()
        {
            AssetDatabase.CreateFolder("Assets/UnderAvatar/Tests",Path.GetFileName(folder));
            string path=folder+"/Building.asset",jsonPath=folder+"/Building.json";
            AssetDatabase.CreateAsset(asset,path);
            BuildingEditorModel.Commit(asset,"重命名",data=>data.name="保存验证");
            AssetDatabase.SaveAssetIfDirty(asset);
            Assert.That(File.ReadAllText(path),Does.Contain("BuildingData").Or.Contain("building:"));
            BuildingEditorModel.Export(asset,jsonPath);
            string saved=asset.Data.ToJson();
            BuildingEditorModel.Commit(asset,"临时修改",data=>data.name="未导入");
            BuildingEditorModel.Import(asset,jsonPath);
            Assert.That(asset.Data.ToJson(),Is.EqualTo(saved));
            File.WriteAllText(jsonPath,"{broken json");
            Assert.Throws<ArgumentException>(()=>BuildingEditorModel.Import(asset,jsonPath));
            Assert.That(asset.Data.ToJson(),Is.EqualTo(saved));
        }
    }
}
