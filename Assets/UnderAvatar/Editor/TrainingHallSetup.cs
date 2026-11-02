using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public static class TrainingHallSetup
    {
        public static BuildingAsset EnsureAsset()
        {
            var asset=Resources.Load<BuildingAsset>("TrainingHall");
            if(asset) return asset;
            asset=ScriptableObject.CreateInstance<BuildingAsset>();asset.SetData(TrainingHallLayout.Create());
            AssetDatabase.CreateAsset(asset,"Assets/UnderAvatar/Resources/TrainingHall.asset");AssetDatabase.SaveAssets();return asset;
        }
        [MenuItem("Tools/Under Avatar/Training Hall Map")]
        public static void Open() { Selection.activeObject=EnsureAsset();EditorGUIUtility.PingObject(Selection.activeObject); }
    }
}
