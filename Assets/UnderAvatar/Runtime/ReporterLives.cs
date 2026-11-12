using System;
using UnityEngine;

namespace UnderAvatar
{
    [Serializable] public sealed class ReporterLifeRules
    {
        [Range(1,20)] public int initialLives=3;
        [Range(.5f,10)] public float deathSeconds=2.5f;
        [Range(0,15)] public float protectionSeconds=3;
        public bool Valid => initialLives>=1 && initialLives<=20 && deathSeconds>=.5f && deathSeconds<=10 && protectionSeconds>=0 && protectionSeconds<=15;
    }
    public sealed partial class MatchSession
    {
        public int ReporterLives {get;private set;}
        public int ReporterLifeEpoch {get;private set;}
        public bool ReporterDying {get;private set;}
        public float ReporterDeathRemaining {get;private set;}
        public float ReporterProtectionRemaining {get;private set;}
        public bool ReporterProtected => ReporterProtectionRemaining>0;
        public Vector3 ReporterSpawn {get;private set;}
        float reporterSpawnYaw;
        public void InitializeReporterLives()
        {
            ReporterLives=Settings.reporterLives.initialLives;
            ReporterSpawn=Reporter.transform.position;reporterSpawnYaw=Reporter.transform.eulerAngles.y;
            gameObject.AddComponent<ReporterDeathPresentation>().Initialize(this);
        }
        public bool CaptureReporter()
        {
            if(!IsAuthority || IsTraining || Finished || ReporterDying || ReporterProtected) return false;
            ReporterLives=Mathf.Max(0,ReporterLives-1);ReporterDying=true;
            ReporterDeathRemaining=Settings.reporterLives.deathSeconds;ReporterLifeEpoch++;
            PendingPhoto=null;queuedPhone=new PlayerCommand();cameraOnNextPhoneOpen=false;
            if(PhotoPreview) Destroy(PhotoPreview);PhotoPreview=null;
            TaskWorld?.DropOnDeath();Abilities?.ClearReporterEffects();
            if(DocumentManager.Instance && DocumentManager.Instance.Owner==this) DocumentManager.Instance.CloseForSessionEnd();
            Reporter.BeginDeath();SetPhoneMenu(false);
            Notice(ReporterLives>0?"狗仔被抓，剩余 "+ReporterLives+" 条命，即将返回出生点。":"狗仔的生命已耗尽。",Settings.reporterLives.deathSeconds);
            return true;
        }
        public void TickReporterLife(float dt)
        {
            if(Finished || IsTraining || SimulationPaused || dt<=0) return;
            ReporterProtectionRemaining=Mathf.Max(0,ReporterProtectionRemaining-dt);
            if(!ReporterDying) return;
            ReporterDeathRemaining=Mathf.Max(0,ReporterDeathRemaining-dt);
            if(!IsAuthority || ReporterDeathRemaining>0) return;
            if(ReporterLives<=0) {Finish("主播获胜：狗仔的生命已耗尽");return;}
            ReporterDying=false;ReporterLifeEpoch++;
            Reporter.RespawnAt(ReporterSpawn,reporterSpawnYaw);
            ReporterProtectionRemaining=Settings.reporterLives.protectionSeconds;
            ReporterAI?.ResetAfterRespawn();
            Notice("已复活，剩余 "+ReporterLives+" 条命。保护期间不能拍照或上传。",Mathf.Max(2,ReporterProtectionRemaining));
        }
        void ApplyReporterLife(MatchSnapshot snapshot)
        {
            bool changed=ReporterLifeEpoch!=snapshot.reporterLifeEpoch;
            ReporterLives=snapshot.reporterLives;ReporterLifeEpoch=snapshot.reporterLifeEpoch;
            ReporterDying=snapshot.reporterDying;ReporterDeathRemaining=Mathf.Max(0,snapshot.reporterDeathRemaining);
            ReporterProtectionRemaining=Mathf.Max(0,snapshot.reporterProtectionRemaining);
            if(changed)
            {
                queuedPhone=new PlayerCommand();cameraOnNextPhoneOpen=false;TaskWorld?.ClearPrediction();
                if(PhotoPreview) Destroy(PhotoPreview);PhotoPreview=null;
                if(ReporterDying && DocumentManager.Instance && DocumentManager.Instance.Owner==this) DocumentManager.Instance.CloseForSessionEnd();
            }
        }
    }
    [DefaultExecutionOrder(600)]
    public sealed class ReporterDeathPresentation : MonoBehaviour
    {
        MatchSession session;
        float fade;
        bool wasDying;
        public void Initialize(MatchSession match) {session=match;}
        void LateUpdate()
        {
            if(!session || !session.Reporter) return;
            if(session.ReporterDying)
            {
                float elapsed=session.Settings.reporterLives.deathSeconds-session.ReporterDeathRemaining;
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.85f));
                var camera=session.Reporter.View.transform;
                camera.localPosition=Vector3.up*Mathf.Lerp(1.65f,.25f,t);
                camera.localRotation=Quaternion.Euler(18*t,0,76*t);
                fade=Mathf.Clamp01((elapsed-.65f)/.8f);
            }
            else
            {
                if(wasDying) session.Reporter.RestoreView();
                fade=Mathf.MoveTowards(fade,0,Time.unscaledDeltaTime*2.5f);
            }
            wasDying=session.ReporterDying;
        }
        void OnGUI()
        {
            if(!session || session.IsTraining || session.Finished) return;
            var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            GUI.matrix=Matrix4x4.identity;GUI.depth=-50;
            var counter=new GUIStyle(GUI.skin.box){fontSize=Mathf.Clamp(Screen.height/45,14,24),alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
            counter.font=Resources.Load<Font>("MainMenu/Fonts/Hangeuljaemin");
            GUI.Box(new Rect(16,Screen.height*.23f,210,30),"狗仔生命："+session.ReporterLives+" / "+session.Settings.reporterLives.initialLives,counter);
            if(session.IsStreamerPlayer) {GUI.depth=depth;GUI.color=color;GUI.matrix=matrix;return;}
            if(fade>0) {GUI.color=new Color(0,0,0,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=color;}
            var style=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Screen.height/36,15,28),alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
            style.font=counter.font;
            string status=session.ReporterDying?(session.ReporterLives>0?"你被抓住了 · 即将复活":"生命耗尽"):
                session.ReporterProtected?"复活保护 "+Mathf.CeilToInt(session.ReporterProtectionRemaining)+" 秒 · 无法拍照 / 上传":null;
            if(status!=null) GUI.Label(new Rect(0,Screen.height*.4f,Screen.width,100),status,style);
            GUI.depth=depth;GUI.color=color;GUI.matrix=matrix;
        }
    }
}
