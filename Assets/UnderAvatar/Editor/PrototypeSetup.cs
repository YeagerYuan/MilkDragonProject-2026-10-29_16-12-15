using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderAvatar.Editor
{
    public static class PrototypeSetup
    {
        public const string ScenePath="Assets/UnderAvatar/Scenes/UnderAvatarPrototype.unity";
        public const string BuildingPath="Assets/UnderAvatar/Levels/DefaultBuilding.asset";

        [MenuItem("Tools/Under Avatar/Prepare Prototype")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play。");
            EnsureLayers();
            Directory.CreateDirectory("Assets/UnderAvatar/Scenes");
            Directory.CreateDirectory("Assets/UnderAvatar/Resources");
            Directory.CreateDirectory("Assets/UnderAvatar/Levels");
            AssetDatabase.Refresh();
            const string materialPath="Assets/UnderAvatar/Resources/PrototypeLit.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(materialPath))
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")),materialPath);
            const string jsonPath="Assets/UnderAvatar/Resources/DefaultBuilding.json";
            if(!File.Exists(jsonPath)) File.WriteAllText(jsonPath,BuildingData.CreateDefault().ToJson());
            var building=AssetDatabase.LoadAssetAtPath<BuildingAsset>(BuildingPath);
            if(!building)
            {
                building=ScriptableObject.CreateInstance<BuildingAsset>();
                building.SetData(BuildingData.FromJson(File.ReadAllText(jsonPath)));
                AssetDatabase.CreateAsset(building,BuildingPath);
            }
            var scene=SceneManager.GetSceneByPath(ScenePath);
            bool alreadyLoaded=scene.IsValid() && scene.isLoaded;
            bool wasDirty=alreadyLoaded && scene.isDirty;
            if(!alreadyLoaded)
                scene=File.Exists(ScenePath)?EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive):
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            PrototypeApp app=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                app=root.GetComponentInChildren<PrototypeApp>(true);
                if(app) break;
            }
            if(!app)
            {
                var root=new GameObject("UnderAvatarPrototype"); SceneManager.MoveGameObjectToScene(root,scene);
                app=root.AddComponent<PrototypeApp>();
            }
            if(!app.Building) { app.Building=building; EditorSceneManager.MarkSceneDirty(scene); }
            // Don't implicitly save unrelated pending scene edits.
            if(!wasDirty) EditorSceneManager.SaveScene(scene,ScenePath);
            if(!alreadyLoaded) EditorSceneManager.CloseScene(scene,true);
            AssetDatabase.SaveAssets();
            BuildingEditorPreview.Restore();
            Debug.Log("[UnderAvatar] Unity 关卡编辑器已准备："+BuildingPath);
        }

        [MenuItem("Tools/Under Avatar/Open Prototype Scene")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Prepare();
            if(SceneManager.GetActiveScene().path!=ScenePath) EditorSceneManager.OpenScene(ScenePath);
            BuildingEditorPreview.Restore();
            BuildingEditorWindow.Open();
            BuildingEditorPreview.Frame();
        }

        public static void EnsureLayers()
        {
            var asset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var manager=new SerializedObject(asset);
            var layers=manager.FindProperty("layers");
            foreach(string name in PrototypeLayers.Names)
            {
                bool present=false;
                for(int i=0;i<layers.arraySize;i++) if(layers.GetArrayElementAtIndex(i).stringValue==name) { present=true; break; }
                if(present) continue;
                int free=-1;
                for(int i=8;i<32;i++) if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { free=i; break; }
                if(free<0) throw new InvalidOperationException("没有空闲 Layer 可用于 "+name);
                layers.GetArrayElementAtIndex(free).stringValue=name;
            }
            manager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
