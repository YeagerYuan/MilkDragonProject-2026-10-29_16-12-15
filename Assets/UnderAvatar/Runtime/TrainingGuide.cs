using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    public sealed class TrainingGuide : MonoBehaviour
    {
        MatchSession match;
        Vector3 start;
        bool moved,posture,photo,uploaded,skill,detected,computer,searched;
        public bool Expanded { get; private set; }=true;
        public RenderTexture Mirror { get; private set; }
        Camera mirrorCamera;
        public string[] Steps => match.IsStreamerPlayer?new[]{
            Mark(moved,"WASD 移动，Shift 静步，Ctrl 蹲下"),
            Mark(computer,"找到电脑，靠近按 E 练习开播交互"),
            Mark(skill && detected,"面向伙伴按住 Q，隔门也能看见红色轮廓"),
            Mark(searched,"看向床或衣柜按 E，练习搜查藏身点")
        }:new[]{
            Mark(moved && posture,"WASD 移动；按住 Shift 静步或 Ctrl 蹲下"),
            Mark(photo,"F 打开手机 → 任务图标 → Q 相机 → 左键拍照"),
            Mark(uploaded,"查看照片，再点击练习上传"),
            Mark(skill,"收起手机，按 G 隐身；观察窗可看效果")
        };
        static string Mark(bool complete,string text) => (complete?"✓ ":"○ ")+text;
        public void Initialize(MatchSession session)
        {
            match=session;start=Owner.position;
            if(!match.IsStreamerPlayer)
            {
                Mirror=new RenderTexture(320,240,24){name="Training invisibility mirror"};Mirror.Create();
                var go=new GameObject("大厅隐身观察镜头");go.transform.SetParent(transform,false);
                mirrorCamera=go.AddComponent<Camera>();mirrorCamera.targetTexture=Mirror;mirrorCamera.fieldOfView=48;
                mirrorCamera.nearClipPlane=.1f;mirrorCamera.farClipPlane=8;
                // A clean third-person mirror of the local body; never used in a formal match.
                mirrorCamera.cullingMask=LayerMask.GetMask("UA_Reporter");mirrorCamera.clearFlags=CameraClearFlags.SolidColor;
                mirrorCamera.backgroundColor=new Color(.09f,.13f,.16f);mirrorCamera.depth=-10;
            }
        }
        Transform Owner => match.IsStreamerPlayer?match.Streamer.transform:match.Reporter.transform;
        void Update()
        {
            if(!match || !match.IsTraining) return;
            if(!match.App.GameplayInputBlocked && !match.Reporter.PhoneOpen && Keyboard.current?.hKey.wasPressedThisFrame==true) Expanded=!Expanded;
            moved|=Vector3.Distance(start,Owner.position)>1;
            posture|=match.Reporter.Crouched || match.Reporter.Sneaking;
            photo|=match.PendingPhoto!=null;uploaded|=match.LastSubmittedPhoto>0;
            skill|=match.IsStreamerPlayer?match.Abilities.Laughing:match.Abilities.Invisible;
            detected|=match.Abilities.Revealed;
            computer|=match.Message!=null && match.Message.Contains("电脑交互练习完成");
            searched|=match.World.Items.Any(i=>i.IsHidingPlace && i.IsOpen);
        }
        void LateUpdate()
        {
            if(!mirrorCamera) return;
            mirrorCamera.enabled=Expanded && !match.Reporter.PhoneOpen && !match.Reporter.Hidden && !match.DebugOpen;
            Vector3 center=Owner.position+Vector3.up*.95f;
            mirrorCamera.transform.position=center+Owner.forward*3+Vector3.up*.1f;
            mirrorCamera.transform.LookAt(center);
        }
        void OnDestroy() {if(Mirror) {Mirror.Release();Destroy(Mirror);}}
    }
    public sealed partial class PrototypeHud
    {
        void DrawTrainingGuide(MatchSession match)
        {
            var guide=match.GetComponent<TrainingGuide>();if(!guide || match.DebugOpen || match.Reporter.Hidden) return;
            if(!guide.Expanded) {GUI.Label(new Rect(28,670,480,35),"H：展开角色教学",small);return;}
            Fill(new Rect(20,456,580,270),new Color(.035f,.055f,.075f,.94f));
            GUILayout.BeginArea(new Rect(36,464,548,252));
            GUILayout.Label((match.IsStreamerPlayer?"主播练习":"狗仔练习")+" · H 收起 / 展开",section);
            foreach(string step in guide.Steps) GUILayout.Label(step,small);
            GUILayout.Space(8);
            GUILayout.Label(match.IsStreamerPlayer?
                "正式对局：电脑前玩 Flappy Bird，达到 "+match.Settings.TargetForBroadcast(1)+" 分后搜捕 "+match.Settings.patrolSeconds.ToString("0")+" 秒。搜捕结束回电脑按 E 开播，逾期会掉粉。大厅不启动小游戏。":
                "正式对局：先选任务再拍照上传；仅错误上传扣机会。隐身仍有正常脚步声，能被大笑探测或贴近抓捕；床 / 衣柜内不会显出轮廓。",small);
            GUILayout.EndArea();
            if(guide.Mirror)
            {
                Fill(new Rect(990,240,270,252),new Color(.035f,.055f,.075f,.94f));
                GUI.Label(new Rect(1002,246,246,27),"隐身观察窗 · 大厅专用",section);
                GUI.DrawTexture(new Rect(1002,280,246,184),guide.Mirror,ScaleMode.ScaleToFit);
                GUI.Label(new Rect(1002,466,246,24),match.Abilities.Invisible?"实体已隐藏 · 剩余 "+match.Abilities.InvisibleRemaining.ToString("0.0")+" 秒":"按 G 查看隐身效果 · 不限次数",small);
            }
        }
    }
}
