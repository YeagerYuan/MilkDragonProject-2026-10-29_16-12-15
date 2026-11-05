using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnderAvatar.Editor
{
    [InitializeOnLoad]
    public static class VTuberAssetSetup
    {
        const string Root="Assets/UnderAvatar/VTuber/";
        static VTuberAssetSetup() {EditorApplication.delayCall+=BuildIfReady;}
        static void BuildIfReady()
        {
            if(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Root+"ExpressionAtlas.png") && !File.Exists(Root+"WhiteHairVTuber.prefab")) Build();
        }
        [MenuItem("Tools/Under Avatar/Create VTuber Prefab")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            var importer=AssetImporter.GetAtPath(Root+"ExpressionAtlas.png") as TextureImporter;
            if(!importer) {Debug.LogWarning("VTuber artwork is not imported yet.");return;}
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"ExpressionAtlas.png");
            var shader=Shader.Find("UnderAvatar/VTuberLoop");if(!shader) {Debug.LogError("VTuber shader is missing.");return;}
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"VTuberLoop.mat");
            if(!material) {material=new Material(shader);AssetDatabase.CreateAsset(material,Root+"VTuberLoop.mat");}
            material.mainTexture=atlas;EditorUtility.SetDirty(material);
            var root=new GameObject("WhiteHairVTuber",typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(640,640);
                var graphic=root.AddComponent<VtuberLoopGraphic>();graphic.material=material;graphic.SetArtwork(atlas);
                PrefabUtility.SaveAsPrefabAsset(root,Root+"WhiteHairVTuber.prefab");AssetDatabase.SaveAssets();
            }
            finally {Object.DestroyImmediate(root);}
            if(!File.Exists(Root+"VTuberPreview.unity")) CreatePreview();
            Debug.Log("Created transparent VTuber prefab: "+Root+"WhiteHairVTuber.prefab");
        }
        static void CreatePreview()
        {
            var previous=SceneManager.GetActiveScene();
            var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(preview);
                var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.075f,.10f);
                var canvas=new GameObject("VTuberPreviewCanvas",typeof(Canvas),typeof(CanvasScaler));
                canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,800);scaler.matchWidthOrHeight=1;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"WhiteHairVTuber.prefab");
                var avatar=(GameObject)PrefabUtility.InstantiatePrefab(prefab,canvas.transform);
                var rect=avatar.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(780,780);
                EditorSceneManager.SaveScene(preview,Root+"VTuberPreview.unity");
            }
            finally {EditorSceneManager.CloseScene(preview,true);if(previous.IsValid()) SceneManager.SetActiveScene(previous);}
        }
    }
}
