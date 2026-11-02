using System;
using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    [CustomEditor(typeof(LevelObjectCatalog))]
    public sealed class LevelObjectCatalogEditor : UnityEditor.Editor
    {
        public static LevelObjectCatalog EnsureCatalog()
        {
            var catalog=LevelObjectCatalog.Default;
            if(catalog) return catalog;
            catalog=CreateInstance<LevelObjectCatalog>();
            string[] names={"墙体","交互门","家具","任务物品","主播电脑桌","折返楼梯","床"};
            for(int i=0;i<names.Length;i++) catalog.objects.Add(new LevelObjectDefinition{id="builtin_"+i,builtIn=true,type=(ItemType)i,displayName=names[i]});
            catalog.objects.Add(new LevelObjectDefinition{id="builtin_wardrobe",builtIn=true,type=ItemType.Furniture,wardrobe=true,displayName="衣柜"});
            AssetDatabase.CreateAsset(catalog,"Assets/UnderAvatar/Resources/LevelObjects.asset");AssetDatabase.SaveAssets();return catalog;
        }
        [MenuItem("Tools/Under Avatar/Level Object Catalog")]
        public static void Open() { Selection.activeObject=EnsureCatalog();EditorGUIUtility.PingObject(Selection.activeObject); }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("自定义名称和物体类型，拖入 Prefab 替换外观。旧关卡自动使用内置类型的替换配置。碰撞、交互和楼梯通行仍使用原关卡尺寸；Prefab 内的额外碰撞和脚本不启用。门 Prefab 只替换门扇；旋转轴沿用原门。",MessageType.Info);
            var list=serializedObject.FindProperty("objects");
            for(int i=0;i<list.arraySize;i++)
            {
                var entry=list.GetArrayElementAtIndex(i);bool builtIn=entry.FindPropertyRelative("builtIn").boolValue;
                entry.isExpanded=EditorGUILayout.Foldout(entry.isExpanded,(builtIn?"内置 · ":"自定义 · ")+entry.FindPropertyRelative("displayName").stringValue,true);
                if(!entry.isExpanded) continue;
                EditorGUI.indentLevel++;
                Field(entry,"displayName","名称");
                using(new EditorGUI.DisabledScope(builtIn)) { Field(entry,"type","基础行为");if(entry.FindPropertyRelative("type").enumValueIndex==(int)ItemType.Furniture) Field(entry,"wardrobe","可躲藏衣柜"); }
                Field(entry,"prefab","外观预制体");Field(entry,"fitToPrototype","自动匹配原物体尺寸");
                Field(entry,"offset","模型位置修正");Field(entry,"rotation","模型旋转修正");Field(entry,"scale","模型缩放倍率");
                if(entry.FindPropertyRelative("type").enumValueIndex==(int)ItemType.Custom) Field(entry,"customSize","默认碰撞尺寸（米）");
                EditorGUILayout.HelpBox("行为类型仅影响之后新放置的物体；已放置物体保留原来的行为。自定义物体暂占一格。",MessageType.None);
                EditorGUI.indentLevel--;
            }
            bool changed=serializedObject.ApplyModifiedProperties();
            if(GUILayout.Button("新增自定义物体"))
            {
                Undo.RecordObject(target,"新增自定义物体");((LevelObjectCatalog)target).objects.Add(new LevelObjectDefinition());EditorUtility.SetDirty(target);changed=true;
            }
            if(GUILayout.Button("保存物体库并更新预览")) { AssetDatabase.SaveAssets();changed=true; }
            if(changed) BuildingEditorPreview.Rebuild();
        }
        static void Field(SerializedProperty entry,string name,string label) => EditorGUILayout.PropertyField(entry.FindPropertyRelative(name),new GUIContent(label),true);
    }
}
