using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    public sealed partial class MatchSession
    {
        public TaskWorld TaskWorld { get; private set; }
        public string SelectedTaskId { get; private set; }
        public PhotoEvidence PendingPhoto { get; private set; }
        public bool PhoneMenuOpen { get; private set; }
        public Texture2D PhotoPreview { get; private set; }
        public int LastSubmittedPhoto { get; private set; }
        int photoSequence;
        PlayerCommand queuedPhone=new PlayerCommand();
        public InvestigationTaskData SelectedTask => InvestigationData.Tasks(World.Data).Find(t=>t.id==SelectedTaskId);
        void InitializeInvestigation()
        {
            if(IsTraining) return;
            var node=new GameObject("任务道具与线索");node.transform.SetParent(transform,false);
            TaskWorld=node.AddComponent<TaskWorld>();TaskWorld.Initialize(World.Data,this);
            foreach(var task in InvestigationData.Tasks(World.Data))
                Quests.Add(new QuestProgress{Id=task.id,Description=task.description,Title=task.title});
            SelectedTaskId=Quests.FirstOrDefault()?.Id??InvestigationData.LiveTask;
        }
        public void SetPhoneMenu(bool open)
        {
            if(!IsStreamerPlayer) {PhoneMenuOpen=open && Reporter.PhoneOpen;Reporter.SetCursor(PhoneMenuOpen || Finished);}
        }
        public void QueuePhoneAction(string task=null,int upload=0,bool cancel=false,bool close=false)
        {
            if(Finished || IsStreamerPlayer) return;
            if(task!=null) {SelectedTaskId=task;queuedPhone.taskId=task;}
            if(upload>0) queuedPhone.uploadPhoto=upload;
            if(cancel) queuedPhone.cancelPhoto=true;
            if(close) queuedPhone.phone=true;
        }
        public PlayerCommand ConsumePhoneCommands()
        {var c=queuedPhone;queuedPhone=new PlayerCommand();return c;}
        public void ApplyPhoneCommand(PlayerCommand c)
        {
            if(!IsAuthority || !Reporter.PhoneOpen || Reporter.Hidden || Finished) return;
            if(c.taskId!=null && (c.taskId==InvestigationData.LiveTask || Quests.Any(q=>q.Id==c.taskId))) SelectedTaskId=c.taskId;
            if(c.cancelPhoto) PendingPhoto=null;
            if(c.uploadPhoto>0) UploadPhoto(c.uploadPhoto);
        }
        public void SelectTask(string id)
        {
            if(id==InvestigationData.LiveTask || Quests.Any(q=>q.Id==id)) SelectedTaskId=id;
        }
        public Vector3? TargetPoint(string id)
        {
            var prop=TaskWorld?.Props.Find(p=>p.Data.id==id);
            if(prop) return prop.PhotoPoint;
            return World.Find(id)?.PhotoPoint;
        }
        public bool TargetInPhoto(string id)
        {
            var point=TargetPoint(id);if(!point.HasValue) return false;
            var prop=TaskWorld?.Props.Find(p=>p.Data.id==id);var placed=World.Find(id);
            if(!IsPhotographable(Reporter.View,point.Value,prop?prop.Body:null)) return false;
            Vector3 ray=point.Value-Reporter.View.transform.position;
            foreach(var hit in Physics.RaycastAll(Reporter.View.transform.position,ray.normalized,ray.magnitude-.02f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
                if(prop?hit.collider.GetComponentInParent<TaskProp>()!=prop:hit.collider.GetComponentInParent<PlacedItemView>()!=placed) return false;
            return true;
        }
        public void CapturePreview()
        {
            if(IsStreamerPlayer || !Reporter.PhoneOpen || Reporter.Hidden || TaskWorld?.Held) return;
            if(PhotoPreview) Destroy(PhotoPreview);
            var camera=Reporter.View;var before=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(640,360,24);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                PhotoPreview=new Texture2D(640,360,TextureFormat.RGB24,false);PhotoPreview.ReadPixels(new Rect(0,0,640,360),0,0);PhotoPreview.Apply();
            }
            finally {camera.targetTexture=before;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }
        void CaptureEvidence()
        {
            if(TaskWorld?.Held) {Notice("请先放下手中的道具，再拍照。");return;}
            if(SelectedTaskId==InvestigationData.LiveTask && !StreamerPhotoAvailable)
            {Notice("本场主播照片已拍摄，等待下一场直播。");return;}
            var task=SelectedTask;
            if(SelectedTaskId!=InvestigationData.LiveTask && (task==null || Quests.Any(q=>q.Id==SelectedTaskId && q.Complete)))
            {Notice("请先在手机中选择尚未完成的任务。");return;}
            var photo=new PhotoEvidence{id=++photoSequence,taskId=SelectedTaskId,regionCorrect=true};
            if(SelectedTaskId==InvestigationData.LiveTask)
            {
                photo.live=photo.objectCorrect=StreamerPhotoAvailable && StreamerInFrame(Reporter.View);
                photo.broadcast=Streamer.Controller.BroadcastNumber;
                if(photo.live) {photographedRound=photo.broadcast;Streamer.OnPhotographed();}
            }
            else
            {
                photo.objectCorrect=TargetInPhoto(task.targetId);photo.targetId=photo.objectCorrect?task.targetId:"";
                if(task.kind==InvestigationKind.PlaceAndPhotograph)
                {
                    var prop=TaskWorld.Props.Find(p=>p.Data.id==task.targetId);
                    photo.regionCorrect=prop && TaskWorld.HeldId!=prop.Data.id && task.Contains(prop.transform.position);
                }
            }
            PendingPhoto=photo;
            if(!IsNetwork || Role==TestRole.Reporter) {CapturePreview();SetPhoneMenu(true);}
            Notice("照片已暂存。确认后上传到拍摄时选定的任务；取消不会扣分。");
        }
        public void UploadPhoto(int id)
        {
            if(!IsAuthority || Finished || IsTraining || Reporter.Hidden || !Reporter.PhoneOpen || PendingPhoto==null || id!=PendingPhoto.id || id<=LastSubmittedPhoto) return;
            var photo=PendingPhoto;PendingPhoto=null;LastSubmittedPhoto=id;
            if(!photo.objectCorrect || !photo.regionCorrect)
            {
                Score=Mathf.Max(0,Score-1);Notice((!photo.objectCorrect?"物体错误":"区域错误")+"：上传失败，剩余失误次数 "+Score+"。");
                if(Score==0) Finish("主播获胜：狗仔任务失败，失误次数已用尽");
                return;
            }
            if(photo.live) StreamerPhotos++;
            else {var quest=Quests.Find(q=>q.Id==photo.taskId);if(quest==null || quest.Complete) return;quest.Complete=true;}
            audioBank.Success();Notice("上传成功："+(photo.live?"主播直播证据 "+StreamerPhotos+" / 3":Quests.Find(q=>q.Id==photo.taskId)?.Title));
            if(Quests.All(q=>q.Complete) && StreamerPhotos>=3) Finish("狗仔获胜：全部证据已上传");
        }
        void UpdatePhone()
        {
            if(!IsNetwork)
            {
                var command=ConsumePhoneCommands();if(command.phone) Reporter.SetPhone(false);ApplyPhoneCommand(command);
            }
        }
        void OnDestroy() {if(PhotoPreview) Destroy(PhotoPreview);}
    }
}
