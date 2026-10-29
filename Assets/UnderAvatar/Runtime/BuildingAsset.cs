using UnityEngine;

namespace UnderAvatar
{
    [CreateAssetMenu(fileName="NewBuilding",menuName="Under Avatar/Building")]
    public sealed class BuildingAsset : ScriptableObject
    {
        [SerializeField] BuildingData building=BuildingData.CreateDefault();
        public BuildingData Data => building;
        public BuildingData CreateRuntimeCopy() => building.Copy();
        public void SetData(BuildingData value) { value.Validate(); building=value.Copy(); }
    }
}
