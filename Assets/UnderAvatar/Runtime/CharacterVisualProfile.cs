using System;
using UnityEngine;

namespace UnderAvatar
{
    [Serializable] public sealed class CharacterVisualSettings
    {
        public GameObject prefab;
        public RuntimeAnimatorController animatorController;
        public Vector3 offset;
        public Vector3 rotation;
        public Vector3 scale=Vector3.one;
        public string speedParameter="Speed";
        public string moveXParameter="MoveX";
        public string moveZParameter="MoveZ";
        public string crouchedParameter="Crouched";
        public string sneakingParameter="Sneaking";
        [Min(.01f)] public float animationDamping=.1f;
    }
    [CreateAssetMenu(menuName="Under Avatar/角色模型配置",fileName="CharacterVisuals")]
    public sealed class CharacterVisualProfile : ScriptableObject
    {
        public CharacterVisualSettings reporter=new CharacterVisualSettings();
        public CharacterVisualSettings streamer=new CharacterVisualSettings();
        [Range(.03f,.25f)] public float interpolationDelay=.1f;
        [Range(0,.15f)] public float maxExtrapolation=.08f;
        [Range(.01f,.15f)] public float positionSmoothing=.04f;
        [Range(.15f,1)] public float staleAfter=.35f;
    }
}
