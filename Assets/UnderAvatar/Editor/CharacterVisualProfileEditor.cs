using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    [CustomEditor(typeof(CharacterVisualProfile))]
    public sealed class CharacterVisualProfileEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Under Avatar/Character Models")]
        public static void Open()
        {
            var profile=Resources.Load<CharacterVisualProfile>("CharacterVisuals");
            if(!profile) { profile=CreateInstance<CharacterVisualProfile>();AssetDatabase.CreateAsset(profile,"Assets/UnderAvatar/Resources/CharacterVisuals.asset");AssetDatabase.SaveAssets(); }
            Selection.activeObject=profile;EditorGUIUtility.PingObject(profile);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("两个角色各自配置外观，下一次开始测试时生效。模型脚底应在原点、正面朝 +Z。外观不会改变角色碰撞和第一人称镜头。",MessageType.Info);
            Role("reporter","狗仔 · 你豆姐");Role("streamer","主播 · 恐怖奶龙");
            EditorGUILayout.Space();EditorGUILayout.LabelField("联机远端外观",EditorStyles.boldLabel);
            Field(serializedObject.FindProperty("interpolationDelay"),"插值缓冲（秒）");
            Field(serializedObject.FindProperty("maxExtrapolation"),"最大外推时间（秒）");
            Field(serializedObject.FindProperty("positionSmoothing"),"位置平滑时间（秒）");
            Field(serializedObject.FindProperty("staleAfter"),"断更停止动画（秒）");
            EditorGUILayout.HelpBox("只平滑远端外观。本机输入即时响应；只同步位置、速度和姿态，不逐帧发送骨骼。缓冲增大会更平滑，也会增加看到对方的延迟。两端应使用同一版本和模型资源。",MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            if(GUILayout.Button("保存角色配置")) AssetDatabase.SaveAssets();
        }
        void Role(string name,string label)
        {
            var entry=serializedObject.FindProperty(name);
            EditorGUILayout.Space();EditorGUILayout.LabelField(label,EditorStyles.boldLabel);
            Field(entry.FindPropertyRelative("prefab"),"模型预制体");
            Field(entry.FindPropertyRelative("animatorController"),"动画控制器覆盖（可留空）");
            Field(entry.FindPropertyRelative("offset"),"位置修正");Field(entry.FindPropertyRelative("rotation"),"旋转修正");Field(entry.FindPropertyRelative("scale"),"缩放倍率");
            entry.isExpanded=EditorGUILayout.Foldout(entry.isExpanded,"动画参数映射",true);
            if(entry.isExpanded)
            {
                Field(entry.FindPropertyRelative("speedParameter"),"实际速度 Float（米/秒）");
                Field(entry.FindPropertyRelative("moveXParameter"),"左右速度 Float");Field(entry.FindPropertyRelative("moveZParameter"),"前后速度 Float");
                Field(entry.FindPropertyRelative("crouchedParameter"),"蹲下 Bool");Field(entry.FindPropertyRelative("sneakingParameter"),"静步 Bool");
                Field(entry.FindPropertyRelative("animationDamping"),"动画平滑时间（秒）");
            }
            var prefab=entry.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
            if(prefab && !prefab.GetComponentInChildren<Animator>(true)) EditorGUILayout.HelpBox("该预制体没有 Animator，将作为静态外观。需要骨骼动画时，请给预制体配置 Animator 和匹配的控制器。",MessageType.Info);
        }
        static void Field(SerializedProperty p,string text) => EditorGUILayout.PropertyField(p,new GUIContent(text),true);
    }
}
