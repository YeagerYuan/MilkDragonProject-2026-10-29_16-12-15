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
        public static int Floor { get; private set; }
        public static bool IsRebuilding { get; private set; }
        public static string Error { get; private set; }
        public static event Action Updated;
        static string snapshot;
        static double nextCheck;
        static BuildingAsset observedAsset;

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
            Updated?.Invoke(); SceneView.RepaintAll();
        }
        static void Tick()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<nextCheck) return;
            nextCheck=EditorApplication.timeSinceStartup+.35;
            if(!Target) { if(World) Clear(); Restore(); return; }
            if(!Target.Building) { if(World) Clear(); return; }
            if(!World || observedAsset!=Target.Building || snapshot!=Target.Building.Data.ToJson()) Rebuild();
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
                SetFlags(root);
                observedAsset=Target.Building; snapshot=observedAsset.Data.ToJson();
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
            if(World)
            {
                World.ReleaseMaterials();
                UnityEngine.Object.DestroyImmediate(World.gameObject);
            }
            World=null;
            foreach(var orphan in Resources.FindObjectsOfTypeAll<BuildingPreviewMarker>())
                if(orphan && orphan.gameObject.scene.IsValid())
                {
                    var world=orphan.GetComponent<BuildingWorld>(); if(world) world.ReleaseMaterials();
                    UnityEngine.Object.DestroyImmediate(orphan.gameObject);
                }
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
