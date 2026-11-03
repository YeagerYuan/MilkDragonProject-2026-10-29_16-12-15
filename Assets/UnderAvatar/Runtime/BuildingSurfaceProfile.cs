using UnityEngine;

namespace UnderAvatar
{
    public enum BuildingSurfaceKind { Wall, Floor, Ceiling, Concrete, Metal }

    [CreateAssetMenu(menuName="Under Avatar/建筑表面材质",fileName="BuildingSurfaces")]
    public sealed class BuildingSurfaceProfile : ScriptableObject
    {
        public bool useTextures=true;
        public Material upperWall,lowerWall,floor,ceiling,concrete,metal;
        [Range(.1f,.8f),Tooltip("绿墙裙占当前层高的比例；所有楼层保持一致。")]
        public float wainscotFraction=.42f;
        [Min(.1f),Tooltip("一张墙面贴图对应多少米；改变墙长或层高不会拉伸纹理。")]
        public float wallMeters=3;
        [Min(.1f),Tooltip("地板图有 4×4 块方砖，2 米对应每块砖约 0.5 米。")]
        public float floorMeters=2;
        [Min(.1f)] public float ceilingMeters=4;
        [Min(.1f)] public float concreteMeters=2;
        [Min(.1f)] public float metalMeters=.6f;
        public static BuildingSurfaceProfile Default=>Resources.Load<BuildingSurfaceProfile>("BuildingSurfaces");
        public bool Ready=>useTextures && upperWall && lowerWall && floor && ceiling && concrete && metal;
        public Material[] Materials(BuildingSurfaceKind kind)
        {
            switch(kind)
            {
                case BuildingSurfaceKind.Wall:return new[]{upperWall,lowerWall};
                case BuildingSurfaceKind.Floor:return new[]{floor,ceiling,concrete};
                case BuildingSurfaceKind.Ceiling:return new[]{ceiling};
                case BuildingSurfaceKind.Metal:return new[]{metal};
                default:return new[]{concrete};
            }
        }
        public float Meters(BuildingSurfaceKind kind,int slot)
        {
            switch(kind)
            {
                case BuildingSurfaceKind.Wall:return Mathf.Max(.1f,wallMeters);
                case BuildingSurfaceKind.Floor:return Mathf.Max(.1f,slot==0?floorMeters:slot==1?ceilingMeters:concreteMeters);
                case BuildingSurfaceKind.Ceiling:return Mathf.Max(.1f,ceilingMeters);
                case BuildingSurfaceKind.Metal:return Mathf.Max(.1f,metalMeters);
                default:return Mathf.Max(.1f,concreteMeters);
            }
        }
    }
}
