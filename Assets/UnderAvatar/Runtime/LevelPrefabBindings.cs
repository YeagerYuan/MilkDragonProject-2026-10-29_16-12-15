using UnityEngine;

namespace UnderAvatar
{
    // Optional visual hinge on a wardrobe/bed prefab. Gameplay remains on PlacedItemView.
    public sealed class LevelPrefabBindings : MonoBehaviour
    {
        [Tooltip("衣柜门或床垫的转轴；不填则仍可搜查，但模型没有打开动画。")]
        public Transform searchPivot;
        public Vector3 searchOpenEuler=new Vector3(0,100,0);
    }
}
