using UnityEngine;

namespace UnderAvatar
{
    [CreateAssetMenu(menuName="Under Avatar/游戏音效配置",fileName="GameAudio")]
    public sealed class GameAudioProfile : ScriptableObject
    {
        [Header("室内脚步（每个文件只放一个脚步，随机轮换）")]
        public AudioClip[] walkSteps;
        public AudioClip[] chaseSteps;
        [Range(0,1)] public float walkVolume=.65f;
        [Range(0,1)] public float chaseVolume=.85f;
        [Min(.1f)] public float walkInterval=.48f;
        [Min(.1f)] public float chaseInterval=.3f;
        [Range(0,.15f)] public float stepPitchVariation=.035f;
        [Header("门与快门")]
        public AudioClip[] doorOpen;
        public AudioClip[] doorClose;
        public AudioClip[] shutter;
        [Tooltip("留空使用现有关门实录，降低音高并增大音量。")]
        public AudioClip[] bedHide;
        [Range(0,1)] public float bedHideVolume=1;
        [Min(1)] public float bedHideDistanceMultiplier=1.5f;
        [Range(0,1)] public float doorVolume=.75f;
        [Range(0,1)] public float shutterVolume=.8f;
        [Header("狗仔本机喘气（不广播、不触发声波和 AI 听觉）")]
        public AudioClip breathingLoop;
        [Range(0,1)] public float breathingVolume=.3f;
        [Range(0,1)] public float quietBreathingMultiplier=.35f;
        [Min(.01f)] public float breathingFadeSeconds=.15f;
        [Header("空间声音衰减（喘气不使用此设置）")]
        [Min(.1f)] public float minDistance=1.5f;
        [Min(1)] public float maxDistance=28;
        [Header("可选：椅子转动 / 取证成功；留空沿用原型提示音")]
        public AudioClip chair;
        public AudioClip success;
    }
}
