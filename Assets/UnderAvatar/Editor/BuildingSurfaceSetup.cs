using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public static class BuildingSurfaceSetup
    {
        public const string Folder="Assets/UnderAvatar/Environment/AbandonedApartment";
        public static BuildingSurfaceProfile EnsureProfile()
        {
            var profile=BuildingSurfaceProfile.Default;if(profile) return profile;
            Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
            profile=ScriptableObject.CreateInstance<BuildingSurfaceProfile>();
            profile.upperWall=Material("WallPlaster",.12f,0);
            profile.lowerWall=Material("WallGreen",.18f,0);
            profile.floor=Material("FloorTiles",.24f,0);
            profile.ceiling=Material("CeilingPlaster",.08f,0);
            profile.concrete=Material("StairConcrete",.1f,0);
            profile.metal=Material("RustedMetal",.2f,.45f);
            AssetDatabase.CreateAsset(profile,"Assets/UnderAvatar/Resources/BuildingSurfaces.asset");
            AssetDatabase.SaveAssets();BuildingEditorPreview.Rebuild();return profile;
        }
        static Material Material(string name,float smoothness,float metallic)
        {
            string path=Folder+"/Textures/"+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer) throw new FileNotFoundException("缺少建筑贴图",path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;
            importer.mipmapEnabled=true;importer.anisoLevel=8;importer.maxTextureSize=2048;
            importer.isReadable=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",smoothness);material.SetFloat("_Metallic",metallic);
            material.enableInstancing=true;AssetDatabase.CreateAsset(material,Folder+"/Materials/"+name+".mat");return material;
        }
        [MenuItem("Tools/Under Avatar/Building Surface Materials")]
        public static void Open() { Selection.activeObject=EnsureProfile();EditorGUIUtility.PingObject(Selection.activeObject); }
    }

    [CustomEditor(typeof(BuildingSurfaceProfile))]
    public sealed class BuildingSurfaceProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("材质用于正式关卡与教学大厅。贴图按实际米数平铺，墙裙随当前层高调整；运行时修改后重新开始测试。编辑模式隐藏顶层天花板，便于摆放物体。",MessageType.Info);
            Field("useTextures","启用建筑贴图");Field("upperWall","上半部灰白墙");Field("lowerWall","下半部绿墙裙");
            Field("floor","方砖地板");Field("ceiling","受潮天花板");Field("concrete","水泥楼梯 / 楼板侧面");Field("metal","锈铁栏板");
            Field("wainscotFraction","墙裙占层高比例");Field("wallMeters","墙贴图覆盖米数");Field("floorMeters","地砖贴图覆盖米数");
            Field("ceilingMeters","天花板贴图覆盖米数");Field("concreteMeters","水泥贴图覆盖米数");Field("metalMeters","锈铁贴图覆盖米数");
            bool changed=serializedObject.ApplyModifiedProperties();
            if(GUILayout.Button("保存并更新场景预览")) { AssetDatabase.SaveAssets();changed=true; }
            if(changed) BuildingEditorPreview.Rebuild();
        }
        void Field(string name,string label)=>EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(label));
    }
}
