using System;
using UnityEngine;

namespace UnderAvatar
{
    [Serializable] public sealed class BroadcastTarget
    {
        [Min(1)] public int broadcast=1;
        [Range(1,100)] public int score=15;
    }
    [Serializable] public sealed class FlappyRules
    {
        [Range(.3f,4)] public float gravity=1.8f;
        [Range(.15f,1.2f)] public float flapImpulse=.56f;
        [Range(.08f,.6f)] public float scrollSpeed=.24f;
        [Range(.2f,.8f)] public float pipeSpacing=.43f;
        [Range(.18f,.6f)] public float gapSize=.34f;
        public FlappyRules Copy() => (FlappyRules)MemberwiseClone();
        public bool Valid => In(gravity,.3f,4) && In(flapImpulse,.15f,1.2f) && In(scrollSpeed,.08f,.6f) && In(pipeSpacing,.2f,.8f) && In(gapSize,.18f,.6f);
        static bool In(float v,float min,float max) => !float.IsNaN(v) && v>=min && v<=max;
    }
}
