using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    // Standalone visual preview; does not run or change the actual match rules.
    public sealed class BroadcastPreviewDriver : MonoBehaviour
    {
        MinigameLevel level;
        BroadcastOverlay overlay;
        float restartAt;
        readonly MatchSettings rules=new MatchSettings();
        float ReturnDuration => rules.returnGraceSeconds+Mathf.Ceil(rules.initialFans/(float)rules.lateFanLoss)*rules.lateFanLossInterval;
        int FansAt(float elapsed) => Mathf.Max(0,rules.initialFans-Mathf.Max(0,Mathf.FloorToInt((elapsed-rules.returnGraceSeconds)/rules.lateFanLossInterval))*rules.lateFanLoss);
        [Range(0,4),Tooltip("0: full cycle; 1: hunt announcement; 2: hunt; 3: end announcement; 4: viewer loss. Number keys switch preview screens.")]
        public int previewScreen;
        float selectedAt;
        static readonly Key[] PreviewKeys={Key.Digit0,Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4};
        void Awake()
        {
            level=new MinigameLevel(81);level.Start(1,12);
            overlay=gameObject.AddComponent<BroadcastOverlay>();Application.targetFrameRate=60;
        }
        void Update()
        {
            var keyboard=Keyboard.current;
            if(keyboard!=null)
                for(int i=0;i<=4;i++) if(keyboard[PreviewKeys[i]].wasPressedThisFrame) {previewScreen=i;selectedAt=Time.unscaledTime;}
            if(level.Complete)
            {if(restartAt==0) restartAt=Time.unscaledTime+rules.patrolSeconds+ReturnDuration;if(Time.unscaledTime>=restartAt) {level.Start(level.LevelNumber+1,12);restartAt=0;}return;}
            if(level.WantsAutoFlap) level.Flap();level.Tick(Time.unscaledDeltaTime);
        }
        void OnGUI()
        {
            var previous=GUI.matrix;
            try
            {
                GUI.matrix=Matrix4x4.TRS(PrototypeHud.Offset,Quaternion.identity,Vector3.one*PrototypeHud.Scale);
                if(previewScreen>0)
                {
                    float t=Time.unscaledTime-selectedAt;
                    int fans=previewScreen==4?FansAt(rules.returnGraceSeconds+t):rules.initialFans;
                    overlay.DrawHunt(previewScreen==1?rules.patrolSeconds-t%3:20,previewScreen==3?t%3:rules.returnGraceSeconds+t,previewScreen>=3,fans,rules.patrolSeconds);
                }
                else if(level.Complete && restartAt>0)
                {
                    float elapsed=rules.patrolSeconds+ReturnDuration-(restartAt-Time.unscaledTime);
                    overlay.DrawHunt(Mathf.Max(0,rules.patrolSeconds-elapsed),Mathf.Max(0,elapsed-rules.patrolSeconds),elapsed>=rules.patrolSeconds,FansAt(Mathf.Max(0,elapsed-rules.patrolSeconds)),rules.patrolSeconds);
                }
                else overlay.Draw(level,5000);
            }
            finally {GUI.matrix=previous;}
        }
    }
}
