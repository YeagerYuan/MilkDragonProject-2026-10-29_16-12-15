using NUnit.Framework;
using UnderAvatar.Editor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace UnderAvatar.Tests
{
    public sealed class DocumentPreviewTests
    {
        [Test] public void DefaultDocumentsAppearWithoutPlayAndDoNotModifyTheirPrefab()
        {
            var previous=BuildingEditorPreview.Target;int floor=BuildingEditorPreview.Floor;
            var root=new GameObject("DocumentPreviewTest");var building=ScriptableObject.CreateInstance<BuildingAsset>();
            try
            {
                var app=root.AddComponent<PrototypeApp>();app.Building=building;
                string path=AssetDatabase.GetAssetPath(app.DocumentPropsPrefab);
                var before=AssetDatabase.GetAssetDependencyHash(path);
                BuildingEditorPreview.Bind(app);
                Assert.That(BuildingEditorPreview.Error,Is.Null);
                var docs=BuildingEditorPreview.Documents;
                Assert.That(docs,Is.Not.Null);
                Assert.That(docs.GetComponentsInChildren<DocumentInteractable>().Length,Is.EqualTo(app.DocumentPropsPrefab.GetComponentsInChildren<DocumentInteractable>().Length));
                Assert.That(docs.GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0));
                Assert.That(docs.GetComponentInChildren<DocumentManager>(true),Is.Null);
                Assert.That((docs.hideFlags & HideFlags.DontSaveInEditor)!=0,Is.True);
                BuildingEditorPreview.Rebuild();
                Assert.That(docs==null,Is.True,"Rebuild must clean up the old preview.");
                Assert.That(AssetDatabase.GetAssetDependencyHash(path),Is.EqualTo(before));
                var serialized=new SerializedObject(app);serialized.FindProperty("enableDocuments").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
                BuildingEditorPreview.Rebuild();Assert.That(BuildingEditorPreview.Documents,Is.Null);
            }
            finally
            {
                BuildingEditorPreview.Clear();Object.DestroyImmediate(root);Object.DestroyImmediate(building);
                BuildingEditorPreview.Bind(previous);BuildingEditorPreview.SetFloor(floor);
            }
        }

        [Test] public void CustomDocumentLayoutUsesMatchingFloorsAndReloadsSavedPositions()
        {
            var previous=BuildingEditorPreview.Target;int floor=BuildingEditorPreview.Floor;
            var root=new GameObject("CustomDocumentPreviewTest");var building=ScriptableObject.CreateInstance<BuildingAsset>();
            var source=new GameObject("CustomDocuments");
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/UnderAvatar/Tests/TemporaryDocumentLayout.prefab");
            try
            {
                var lower=GameObject.CreatePrimitive(PrimitiveType.Cube);lower.name="Lower";lower.transform.SetParent(source.transform);lower.transform.position=Vector3.up;
                var upper=GameObject.CreatePrimitive(PrimitiveType.Cube);upper.name="Upper";upper.transform.SetParent(source.transform);upper.transform.position=Vector3.up*5;
                var disabled=new GameObject("Disabled");disabled.transform.SetParent(source.transform);disabled.SetActive(false);
                var prefab=PrefabUtility.SaveAsPrefabAsset(source,path);
                var app=root.AddComponent<PrototypeApp>();app.Building=building;
                var serialized=new SerializedObject(app);serialized.FindProperty("documentPropsPrefab").objectReferenceValue=prefab;serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(app.DocumentPropsPrefab,Is.SameAs(prefab));
                BuildingEditorPreview.Bind(app);
                Assert.That(BuildingEditorPreview.Documents.transform.Find("Lower").gameObject.activeSelf,Is.True);
                Assert.That(BuildingEditorPreview.Documents.transform.Find("Upper").gameObject.activeSelf,Is.False);
                BuildingEditorPreview.SetFloor(1);
                Assert.That(BuildingEditorPreview.Documents.transform.Find("Upper").gameObject.activeSelf,Is.True);
                Assert.That(BuildingEditorPreview.Documents.transform.Find("Disabled").gameObject.activeSelf,Is.False);
                var editable=PrefabUtility.LoadPrefabContents(path);
                try { editable.transform.Find("Upper").position=new Vector3(3,5,2);PrefabUtility.SaveAsPrefabAsset(editable,path); }
                finally { PrefabUtility.UnloadPrefabContents(editable); }
                BuildingEditorPreview.Rebuild();
                Assert.That(BuildingEditorPreview.Documents.transform.Find("Upper").position,Is.EqualTo(new Vector3(3,5,2)));
                BuildingEditorPreview.Clear();Assert.That(BuildingEditorPreview.Documents,Is.Null);
            }
            finally
            {
                BuildingEditorPreview.Clear();Object.DestroyImmediate(root);Object.DestroyImmediate(building);Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(path);BuildingEditorPreview.Bind(previous);BuildingEditorPreview.SetFloor(floor);
            }
        }
    }
}
