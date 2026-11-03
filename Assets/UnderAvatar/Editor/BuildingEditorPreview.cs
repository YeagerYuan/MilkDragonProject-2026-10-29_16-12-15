using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderAvatar.Editor
{
    [InitializeOnLoad]
    public static class BuildingEditorPreview
    {
        public static PrototypeApp Target { get; private set; }
        public static BuildingWorld World { get; private set; }
        public static GameObject Documents { get; private set; }
        public static TaskWorld Tasks { get; private set; }
        public static int Floor { get; private set; }
        public static bool IsRebuilding { get; private set; }
        public static string Error { get; private set; }
        public static event Action Updated;
        static string snapshot;
        static double nextCheck;
        static BuildingAsset observedAsset;
        static string documentSnapshot;

        static string DocumentSnapshot()
        {
            if(!Target || !Target.DocumentsEnabled) return "disabled";
            var prefab=Target.DocumentPropsPrefab;
            if(!prefab) return "missing";
            string path=AssetDatabase.GetAssetPath(prefab);
            return path+":"+AssetDatabase.GetAssetDependencyHash(path);
        }

        static void SetDocumentFloor()
        {
            if(!Documents || !World) return;
            // Match the house preview: retain this floor and those below it.
            foreach(Transform group in Documents.transform)
            {
                var source=PrefabUtility.GetCorrespondingObjectFromSource(group.gameObject);
                if(source && !source.activeSelf) { group.gameObject.SetActive(false);continue; }
                var renderers=group.GetComponentsInChildren<Renderer>(true);
                var bounds=new Bounds(group.position,Vector3.zero);
                if(renderers.Length>0)
                {
                    bounds=renderers[0].bounds;
                    foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                }
                int floor=Mathf.Max(0,Mathf.FloorToInt((bounds.center.y+.05f)/World.Data.floorHeight));
                group.gameObject.SetActive(floor<=Floor && floor<World.Data.floors.Count);
            }
        }

        public static void FrameDocuments()
        {
            if(!Documents) return;
            var renderers=Documents.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) return;
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var view=SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.sceneLighting=false;view.Frame(bounds,false);view.Show();view.Focus();
        }

        static BuildingEditorPreview()
        {
            EditorApplication.delayCall+=Restore;
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload+=Clear;
            EditorApplication.quitting+=Clear;
            Undo.undoRedoPerformed+=Rebuild;
            BuildingEditorModel.Changed+=OnAssetChanged;
            EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=Restore;
        }
        public static void Restore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!Target)
                Target=UnityEngine.Object.FindObjectsByType<PrototypeApp>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .FirstOrDefault(a=>a.gameObject.scene==SceneManager.GetActiveScene());
            if(Target) Rebuild();
        }
        public static void Bind(PrototypeApp target)
        {
            if(target && Target==target && World && observedAsset==target.Building) return;
            Clear(); Target=target; Floor=0; snapshot=null; Rebuild();
        }
        public static void SetFloor(int floor)
        {
            if(!Target || !Target.Building) return;
            Floor=Mathf.Clamp(floor,0,Target.Building.Data.floors.Count-1);
            if(World) World.SetMode(true,Floor);
            SetDocumentFloor();
            if(Tasks) Tasks.SetEditorFloor(Floor);
            Updated?.Invoke(); SceneView.RepaintAll();
        }
        static void Tick()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<nextCheck) return;
            nextCheck=EditorApplication.timeSinceStartup+.35;
            if(!Target) { if(World) Clear(); Restore(); return; }
            if(!Target.Building) { if(World) Clear(); return; }
            if(!World || observedAsset!=Target.Building || snapshot!=Target.Building.Data.ToJson() || documentSnapshot!=DocumentSnapshot()) Rebuild();
        }
        static void OnAssetChanged(BuildingAsset asset) { if(Target && Target.Building==asset) Rebuild(); }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode) Clear();
            if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall+=Restore;
        }
        public static bool Owns(PlacedItemView view) => view && World && view.transform.IsChildOf(World.transform);
        public static void Rebuild()
        {
            if(IsRebuilding || EditorApplication.isPlayingOrWillChangePlaymode) return;
            IsRebuilding=true;
            var selected=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<PlacedItemView>():null;
            string selectedId=Owns(selected)?selected.Data.id:null;
            try
            {
                Clear(); Error=null;
                if(!Target || !Target.Building) return;
                PrototypeSetup.EnsureLayers();
                var data=Target.Building.CreateRuntimeCopy();
                Floor=Mathf.Clamp(Floor,0,data.floors.Count-1);
                var root=new GameObject("房屋预览（由关卡资源生成）");
                root.tag="EditorOnly";
                root.AddComponent<BuildingPreviewMarker>();
                SceneManager.MoveGameObjectToScene(root,Target.gameObject.scene);
                World=root.AddComponent<BuildingWorld>(); World.Rebuild(data); World.SetMode(true,Floor);
                if(Target.DocumentsEnabled && Target.DocumentPropsPrefab)
                {
                    Documents=(GameObject)PrefabUtility.InstantiatePrefab(Target.DocumentPropsPrefab,root.transform);
                    Documents.name="文档布置（预览；通过编辑文档布置修改）";
                    foreach(var behaviour in Documents.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
                    SetDocumentFloor();
                }
                var taskRoot=new GameObject("任务编辑预览");taskRoot.transform.SetParent(root.transform,false);
                Tasks=taskRoot.AddComponent<TaskWorld>();Tasks.Initialize(data);Tasks.SetEditorFloor(Floor);
                SetFlags(root);
                foreach(var node in taskRoot.GetComponentsInChildren<Transform>(true)) node.gameObject.hideFlags|=HideFlags.NotEditable;
                if(Documents)
                    foreach(var node in Documents.GetComponentsInChildren<Transform>(true)) node.gameObject.hideFlags|=HideFlags.NotEditable;
                observedAsset=Target.Building; snapshot=observedAsset.Data.ToJson();
                documentSnapshot=DocumentSnapshot();
                if(selectedId!=null)
                {
                    var replacement=World.Find(selectedId);
                    if(replacement) Selection.activeGameObject=replacement.gameObject;
                }
            }
            catch(Exception e) { Error=e.Message; Clear(); }
            finally { IsRebuilding=false; Updated?.Invoke(); SceneView.RepaintAll(); }
        }
        public static void SetFlags(GameObject root)
        {
            foreach(var node in root.GetComponentsInChildren<Transform>(true))
                node.gameObject.hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild;
        }
        public static void Clear()
        {
            // Unity's Inspector can retain stale targets if a generated preview
            // object remains selected while the preview is destroyed.
            var selected=Selection.objects;
            if(selected!=null && selected.Any(IsPreviewSelection))
                Selection.objects=Array.Empty<UnityEngine.Object>();
            if(World)
            {
                World.ReleaseMaterials();
                UnityEngine.Object.DestroyImmediate(World.gameObject);
            }
            World=null; Documents=null;Tasks=null;
            foreach(var orphan in Resources.FindObjectsOfTypeAll<BuildingPreviewMarker>())
                if(orphan && orphan.gameObject.scene.IsValid())
                {
                    var world=orphan.GetComponent<BuildingWorld>(); if(world) world.ReleaseMaterials();
                    UnityEngine.Object.DestroyImmediate(orphan.gameObject);
                }
        }
        static bool IsPreviewSelection(UnityEngine.Object value)
        {
            GameObject go=value as GameObject;
            if(!go && value is Component component) go=component?component.gameObject:null;
            if(!go) return false;
            if(World && (go==World.gameObject || go.transform.IsChildOf(World.transform))) return true;
            return go.GetComponentInParent<BuildingPreviewMarker>();
        }
        public static void Frame()
        {
            var view=SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.sceneLighting=false;
            float size=Target && Target.Building ? Mathf.Max(Target.Building.Data.halfWidth,Target.Building.Data.halfDepth)*Target.Building.Data.gridSize*1.25f : 20;
            float height=Target && Target.Building ? Floor*Target.Building.Data.floorHeight : 0;
            view.LookAt(new Vector3(0,height,0),Quaternion.Euler(55,-25,0),size,false);
            view.Show(); view.Focus();
        }
    }
}
