using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    [CustomEditor(typeof(GameAudioProfile))]
    public sealed class GameAudioProfileEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Under Avatar/Audio Settings")]
        public static void Open()
        {
            var app=Object.FindFirstObjectByType<PrototypeApp>();
            var profile=app?app.AudioProfile:Resources.Load<GameAudioProfile>("GameAudio");
            Selection.activeObject=profile;EditorGUIUtility.PingObject(profile);
        }
        void Field(string property,string label,string tip="") =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(property),new GUIContent(label,tip),true);
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("将 Project 中的音频拖入下列槽位。脚步数组每项放一个脚步；喘气放完整循环。修改配置资源会保存，Play 中调整也会保留。音量设为 0 可静音，不改变取证与 AI 听觉规则。",MessageType.Info);
            Field("walkSteps","行走脚步变体");Field("chaseSteps","主播搜捕脚步变体");
            Field("walkVolume","行走音量");Field("chaseVolume","搜捕音量");
            Field("walkInterval","行走步频间隔（秒）");Field("chaseInterval","搜捕步频间隔（秒）");
            Field("stepPitchVariation","脚步音高随机幅度");
            Field("doorOpen","开门声音");Field("doorClose","关门声音");Field("shutter","拍照快门");
            Field("doorVolume","门声音量");Field("shutterVolume","快门音量");
            Field("bedHide","躲床声音（可选）");Field("bedHideVolume","躲床音量");Field("bedHideDistanceMultiplier","躲床传播距离倍率");
            Field("breathingLoop","喘气循环");Field("breathingVolume","普通移动喘气音量");
            Field("quietBreathingMultiplier","静步 / 蹲行喘气倍率");Field("breathingFadeSeconds","喘气淡入淡出（秒）");
            Field("minDistance","开始衰减距离（米）");Field("maxDistance","最远可听距离（米）");
            Field("chair","椅子转动（可选）");Field("success","取证成功（可选）");
            serializedObject.ApplyModifiedProperties();
            if(GUILayout.Button("保存音效配置")) AssetDatabase.SaveAssets();
        }
    }
}
