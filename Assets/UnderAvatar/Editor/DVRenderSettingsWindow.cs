using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public sealed class DVRenderSettingsWindow : EditorWindow
    {
        public const string ReporterPath="Assets/Settings/Resources/HorrorDVSignal_Reporter.mat";
        public const string StreamerPath="Assets/Settings/Resources/HorrorDVSignal_Streamer.mat";
        Vector2 scroll;

        [MenuItem("Tools/Under Avatar/DV Rendering")]
        public static void Open()
        {
            EnsurePresets();
            var window=GetWindow<DVRenderSettingsWindow>("双角色画质");
            window.minSize=new Vector2(390,420);window.Show();
        }

        public static void EnsurePresets()
        {
            var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Resources/HorrorDVSignal.mat");
            var shader=Shader.Find("UnderAvatar/DV Camcorder Blur");
            if(!source && !shader) return;
            if(!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets","Settings");
            if(!AssetDatabase.IsValidFolder("Assets/Settings/Resources")) AssetDatabase.CreateFolder("Assets/Settings","Resources");
            foreach(string path in new[]{ReporterPath,StreamerPath})
            {
                if(AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
                var material=source?new Material(source):new Material(shader);
                material.name=System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(material,path);
            }
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("分别调整两种角色的正式对局画面。Play 时修改会实时生效，退出 Play 后仍然保留；AI 主播观察窗使用主播设置。教学大厅沿用原画面。",MessageType.Info);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            DrawRole("狗仔画面",ReporterPath);
            EditorGUILayout.Space(12);
            DrawRole("主播画面（含 AI 观察窗）",StreamerPath);
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox("模糊半径越大越模糊，0 关闭额外柔化。画面分辨率越低越糊，即使半径为 0 也会保留低清效果。建议 520×292 配 1.25，较清晰可试 960×540 配 0.3。",MessageType.None);
            EditorGUILayout.EndScrollView();
            if(GUILayout.Button("保存两种角色的画质设置",GUILayout.Height(30)))
                foreach(string path in new[]{ReporterPath,StreamerPath})
                    AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Material>(path));
        }

        static void DrawRole(string label,string path)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            EditorGUILayout.LabelField(label,EditorStyles.boldLabel);
            if(!material) {if(GUILayout.Button("创建默认设置")) EnsurePresets();return;}
            EditorGUI.BeginChangeCheck();
            float blur=EditorGUILayout.Slider("模糊半径",material.GetFloat("_BlurRadius"),0,3);
            var size=material.GetVector("_TargetResolution");
            var resolution=EditorGUILayout.Vector2IntField("画面分辨率",new Vector2Int(Mathf.RoundToInt(size.x),Mathf.RoundToInt(size.y)));
            float chroma=EditorGUILayout.Slider("彩色拖影 / 色散",material.GetFloat("_ChromaOffset"),0,.02f);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(material,"调整角色 DV 画质");
                material.SetFloat("_BlurRadius",blur);
                material.SetVector("_TargetResolution",new Vector4(Mathf.Clamp(resolution.x,16,4096),Mathf.Clamp(resolution.y,16,4096),0,0));
                material.SetFloat("_ChromaOffset",chroma);
                EditorUtility.SetDirty(material);
            }
            if(GUILayout.Button("选中材质 / 调整噪点、暗角等参数")) Selection.activeObject=material;
        }
    }
}
