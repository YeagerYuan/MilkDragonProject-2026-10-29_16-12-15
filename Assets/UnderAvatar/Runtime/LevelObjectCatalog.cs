using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderAvatar
{
    [Serializable]
    public sealed class LevelObjectDefinition
    {
        public string id=Guid.NewGuid().ToString("N");
        public string displayName="新物体";
        public bool builtIn;
        public ItemType type=ItemType.Custom;
        public bool wardrobe;
        public GameObject prefab;
        public Vector3 customSize=new Vector3(1.5f,1.5f,1.5f);
        public bool fitToPrototype=true;
        public Vector3 offset;
        public Vector3 rotation;
        public Vector3 scale=Vector3.one;
    }

    [CreateAssetMenu(menuName="Under Avatar/关卡物体库",fileName="LevelObjects")]
    public sealed class LevelObjectCatalog : ScriptableObject
    {
        public List<LevelObjectDefinition> objects=new List<LevelObjectDefinition>();
        public static LevelObjectCatalog Default => Resources.Load<LevelObjectCatalog>("LevelObjects");
        public LevelObjectDefinition Resolve(PlacedItemData item)
        {
            if(!string.IsNullOrEmpty(item.definitionId))
            {
                var value=objects.Find(d=>d.id==item.definitionId);
                if(value!=null) return value;
            }
            return objects.Find(d=>d.builtIn && d.type==item.type && (d.type!=ItemType.Furniture || d.wardrobe==item.hideable));
        }
    }
}
