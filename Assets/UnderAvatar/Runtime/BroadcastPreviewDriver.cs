using UnityEngine;

namespace UnderAvatar
{
    // Standalone visual preview; does not run or change the actual match rules.
    public sealed class BroadcastPreviewDriver : MonoBehaviour
    {
        MinigameLevel level;
        BroadcastOverlay overlay;
        float restartAt;
        void Awake()
        {
            level=new MinigameLevel(81);level.Start(1,12);
            overlay=gameObject.AddComponent<BroadcastOverlay>();Application.targetFrameRate=60;
        }
        void Update()
        {
            if(level.Complete)
            {if(restartAt==0) restartAt=Time.unscaledTime+3;if(Time.unscaledTime>=restartAt) {level.Start(level.LevelNumber+1,12);restartAt=0;}return;}
            if(level.WantsAutoFlap) level.Flap();level.Tick(Time.unscaledDeltaTime);
        }
        void OnGUI()
        {
            var previous=GUI.matrix;
            try {GUI.matrix=Matrix4x4.TRS(PrototypeHud.Offset,Quaternion.identity,Vector3.one*PrototypeHud.Scale);overlay.Draw(level,5000);}
            finally {GUI.matrix=previous;}
        }
    }
}
