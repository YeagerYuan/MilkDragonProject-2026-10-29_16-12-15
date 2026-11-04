using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderAvatar.Editor
{
    [InitializeOnLoad]
    public static class BroadcastOverlaySetup
    {
        const string Avatar="Assets/UnderAvatar/Resources/BroadcastAvatar.prefab";
        const string Preview="Assets/UnderAvatar/Scenes/BroadcastOverlayPreview.unity";
        static BroadcastOverlaySetup() {EditorApplication.delayCall+=Prepare;}
        static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnderAvatar/VTuber/WhiteHairVTuber.prefab");if(!source) return;
            if(!File.Exists(Avatar))
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
                try {PrefabUtility.SaveAsPrefabAsset(instance,Avatar);}
                finally {Object.DestroyImmediate(instance);}
            }
            if(!File.Exists(Preview))
            {
                var previous=SceneManager.GetActiveScene();
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var root=new GameObject("Livestream UI Preview");root.AddComponent<BroadcastPreviewDriver>();
                    var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.204f,.325f,.40f);
                    EditorSceneManager.SaveScene(scene,Preview);
                }
                finally {EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()) SceneManager.SetActiveScene(previous);}
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Under Avatar/Preview Livestream UI")]
        public static void Open()
        {Prepare();if(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Preview)) EditorSceneManager.OpenScene(Preview);}
    }
}
