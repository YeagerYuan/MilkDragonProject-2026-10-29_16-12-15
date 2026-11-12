using System;
using System.Collections.Generic;
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
        public PhonePage ActivePhonePage {get;private set;}=PhonePage.Tasks;
        bool cameraOnNextPhoneOpen;
        public bool CameraMode => Reporter && Reporter.PhoneOpen && !PhoneMenuOpen && PendingPhoto==null;
        public void OpenPhonePage(PhonePage page)
        {
            if(IsStreamerPlayer) {SetStreamerMap(true);return;}
            if(Finished || ReporterDying || IsStreamerPlayer || Reporter.Hidden || TaskWorld?.Held) return;
            ActivePhonePage=page;
            if(Reporter.PhoneOpen) SetPhoneMenu(true);else QueuePhoneAction(close:true);
        }
        public void EnterPhotoMode()
        {
            if(Finished || ReporterDying || IsStreamerPlayer || Reporter.Hidden || PendingPhoto!=null || TaskWorld?.Held) return;
            if(Reporter.PhoneOpen) SetPhoneMenu(false);
            else {cameraOnNextPhoneOpen=true;QueuePhoneAction(close:true);}
        }
        public Texture2D PhotoPreview { get; private set; }
        public int LastSubmittedPhoto { get; private set; }
        int photoSequence;
        PlayerCommand queuedPhone=new PlayerCommand();
        readonly Dictionary<string,DocumentInteractable> documentTargets=new Dictionary<string,DocumentInteractable>();
        public void RegisterDocumentTargets(GameObject documents)
        {
            documentTargets.Clear();
            if(documents) foreach(var doc in documents.GetComponentsInChildren<DocumentInteractable>())
            {
                if(!doc.enabled || doc.PhotoTargetId==null) continue;
                if(documentTargets.ContainsKey(doc.PhotoTargetId)) throw new InvalidOperationException("文档拍照目标标识重复："+doc.PhotoTargetId);
                documentTargets.Add(doc.PhotoTargetId,doc);
            }
            foreach(var id in InvestigationData.Tasks(World.Data).SelectMany(t=>t.PhotoTargets).Where(InvestigationData.IsDocumentTarget).Distinct())
                if(!documentTargets.ContainsKey(id)) throw new InvalidOperationException("任务绑定的可阅读文档未启用或不存在："+id+"。请检查文档布置预制体及 Photo Target Id。");
        }
        DocumentInteractable FindDocumentTarget(string id) => id!=null && documentTargets.TryGetValue(id,out var doc) && doc && doc.isActiveAndEnabled?doc:null;
        public InvestigationTaskData SelectedTask => IsTraining?new InvestigationTaskData{id="__practice",title="自由拍照练习",description="进入相机，对准物品或伙伴拍照，再确认上传。大厅只演示操作，不计分、不产生正式任务。"}:InvestigationData.Tasks(World.Data).Find(t=>t.id==SelectedTaskId);
        void InitializeInvestigation()
        {
            if(IsTraining) {SelectedTaskId="__practice";return;}
            var node=new GameObject("任务道具与线索");node.transform.SetParent(transform,false);
            TaskWorld=node.AddComponent<TaskWorld>();TaskWorld.Initialize(World.Data,this);
            foreach(var task in InvestigationData.Tasks(World.Data))
                Quests.Add(new QuestProgress{Id=task.id,Description=task.description,Title=task.title});
            SelectedTaskId=Quests.FirstOrDefault()?.Id??(RequiresLiveEvidence?InvestigationData.LiveTask:null);
        }
        public void SetPhoneMenu(bool open)
        {
            if(open && cameraOnNextPhoneOpen) {open=false;cameraOnNextPhoneOpen=false;}
            if(!IsStreamerPlayer) {PhoneMenuOpen=open && Reporter.PhoneOpen;Reporter.SetCursor(PhoneMenuOpen || Finished);}
        }
        public void QueuePhoneAction(string task=null,int upload=0,bool cancel=false,bool close=false)
        {
            if(Finished || ReporterDying || IsStreamerPlayer) return;
            if(task!=null) {SelectTask(task);queuedPhone.taskId=SelectedTaskId;}
            if((upload>0 || cancel) && !Reporter.PhoneOpen) queuedPhone.phone=true;
            if(upload>0) queuedPhone.uploadPhoto=upload;
            if(cancel) queuedPhone.cancelPhoto=true;
            if(close) queuedPhone.phone=true;
        }
        public PlayerCommand ConsumePhoneCommands()
        {var c=queuedPhone;queuedPhone=new PlayerCommand();return c;}
        public void ApplyPhoneCommand(PlayerCommand c)
        {
            if(!IsAuthority || !Reporter.PhoneOpen || Reporter.Hidden || Finished) return;
            if(c.taskId!=null && (RequiresLiveEvidence && c.taskId==InvestigationData.LiveTask || Quests.Any(q=>q.Id==c.taskId))) SelectedTaskId=c.taskId;
            if(c.cancelPhoto) {PendingPhoto=null;Reporter.SetPhone(false);}
            if(c.uploadPhoto>0) {UploadPhoto(c.uploadPhoto);Reporter.SetPhone(false);}
        }
        public void SelectTask(string id)
        {
            if(RequiresLiveEvidence && id==InvestigationData.LiveTask || Quests.Any(q=>q.Id==id)) SelectedTaskId=id;
        }
        public Vector3? TargetPoint(string id)
        {
            if(InvestigationData.IsDocumentTarget(id)) {var doc=FindDocumentTarget(id);return doc?(Vector3?)doc.PhotoPoint:null;}
            if(id==InvestigationData.StreamerTarget) return Streamer?(Vector3?)Streamer.PhotoPoint:null;
            if(id==InvestigationData.ComputerTarget) return Streamer && Streamer.Computer?(Vector3?)Streamer.Computer.PhotoPoint:null;
            var prop=TaskWorld?.Props.Find(p=>p.Data.id==id);
            if(prop) return prop.PhotoPoint;
            return World.Find(id)?.PhotoPoint;
        }
        public float TaskPhotoDistance => Mathf.Min(Mathf.Max(.5f,Settings.taskPhotoDistance),Settings.maxPhotoDistance,Settings.reporterVision?.range??12);
        public bool TargetInPhoto(string id)
        {
            var point=TargetPoint(id);
            if(!point.HasValue || Vector3.Distance(Reporter.View.transform.position,point.Value)>TaskPhotoDistance) return false;
            if(InvestigationData.IsDocumentTarget(id))
            {
                var doc=FindDocumentTarget(id);
                if(!doc || DocumentManager.BlocksGameplayInput) return false;
                if(!IsPhotographable(Reporter.View,doc.PhotoPoint,null,doc.transform)) return false;
                Vector3 direction=doc.PhotoPoint-Reporter.View.transform.position;
                foreach(var hit in Physics.RaycastAll(Reporter.View.transform.position,direction.normalized,direction.magnitude-.02f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
                    if(!hit.collider.transform.IsChildOf(doc.transform)) return false;
                return true;
            }
            var prop=TaskWorld?.Props.Find(p=>p.Data.id==id);var placed=World.Find(id);
            if(id==InvestigationData.ComputerTarget) placed=Streamer.Computer;
            // Computer geometry may surround its own photo point; only other objects obstruct it.
            if(placed && placed.Data.type==ItemType.StreamerPC)
            {
                if(!IsPhotographable(Reporter.View,point.Value,null,placed.transform)) return false;
            }
            else if(!IsPhotographable(Reporter.View,point.Value,prop?prop.Body:null)) return false;
            Vector3 ray=point.Value-Reporter.View.transform.position;
            foreach(var hit in Physics.RaycastAll(Reporter.View.transform.position,ray.normalized,ray.magnitude-.02f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
                if(prop?hit.collider.GetComponentInParent<TaskProp>()!=prop:!placed || hit.collider.GetComponentInParent<PlacedItemView>()!=placed) return false;
            return true;
        }
        public bool TaskInPhoto(InvestigationTaskData task) => task!=null && task.PhotoTargets.All(TargetInPhoto);
        public Vector3? TaskPhotoPoint(InvestigationTaskData task)
        {
            var points=task.PhotoTargets.Select(TargetPoint).ToArray();
            if(points.Any(p=>!p.HasValue)) return null;
            return points.Aggregate(Vector3.zero,(sum,p)=>sum+p.Value)/points.Length;
        }
        public void CapturePreview()
        {
            if(DocumentManager.BlocksGameplayInput || ReporterDying || ReporterProtected || IsStreamerPlayer || !Reporter.PhoneOpen || Reporter.Hidden || TaskWorld?.Held) return;
            if(PhotoPreview) Destroy(PhotoPreview);
            var camera=Reporter.View;var before=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(640,Mathf.Max(128,Mathf.RoundToInt(640/camera.aspect)),24);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var crop=new Rect(Mathf.RoundToInt(rt.width*.025f),Mathf.RoundToInt(rt.height*.165f),Mathf.FloorToInt(rt.width*.95f),Mathf.FloorToInt(rt.height*.785f));
                PhotoPreview=new Texture2D((int)crop.width,(int)crop.height,TextureFormat.RGB24,false);PhotoPreview.ReadPixels(crop,0,0);PhotoPreview.Apply();
            }
            finally {camera.targetTexture=before;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }
        void CaptureEvidence()
        {
            if(IsTraining)
            {
                PendingPhoto=new PhotoEvidence{id=++photoSequence,taskId="__practice",objectCorrect=true,regionCorrect=true};
                if(!IsNetwork || Role==TestRole.Reporter) {CapturePreview();SetPhoneMenu(true);}
                Notice("练习照片已暂存，请在手机中查看或上传。大厅不扣分。");return;
            }
            if(TaskWorld?.Held) {Notice("请先放下手中的道具，再拍照。");return;}
            if(SelectedTaskId==InvestigationData.LiveTask && !RequiresLiveEvidence) {Notice("此关卡没有额外直播证据任务。");return;}
            if(SelectedTaskId==InvestigationData.LiveTask && !StreamerPhotoAvailable)
            {Notice("本场主播照片已拍摄，等待下一场直播。");return;}
            var task=SelectedTask;
            if(SelectedTaskId!=InvestigationData.LiveTask && (task==null || Quests.Any(q=>q.Id==SelectedTaskId && q.Complete)))
            {Notice("请先在手机中选择尚未完成的任务。");return;}
            if(task!=null && task.IncludesStreamer && (Streamer.State!=StreamerState.Streaming || photographedRound==Streamer.Controller.BroadcastNumber))
            {Notice("所有主播拍摄任务每场直播合计只能拍摄上传一次，请等待下一场直播。");return;}
            var photo=new PhotoEvidence{id=++photoSequence,taskId=SelectedTaskId,regionCorrect=true};
            if(SelectedTaskId==InvestigationData.LiveTask)
            {
                photo.live=photo.objectCorrect=StreamerPhotoAvailable && StreamerInFrame(Reporter.View);
                photo.broadcast=Streamer.Controller.BroadcastNumber;
                if(photo.live) {photographedRound=photo.broadcast;Streamer.OnPhotographed();}
            }
            else
            {
                photo.objectCorrect=TaskInPhoto(task);photo.targetId=photo.objectCorrect?task.targetId:"";
                if(task.kind==InvestigationKind.PlaceAndPhotograph)
                {
                    var prop=TaskWorld.Props.Find(p=>p.Data.id==task.targetId);
                    photo.regionCorrect=prop && TaskWorld.HeldId!=prop.Data.id && task.Contains(prop.transform.position);
                }
            }
            if(task!=null && task.IncludesStreamer && photo.objectCorrect)
            {photo.broadcast=Streamer.Controller.BroadcastNumber;photographedRound=photo.broadcast;Streamer.OnPhotographed();}
            PendingPhoto=photo;
            if(!IsNetwork || Role==TestRole.Reporter) {CapturePreview();SetPhoneMenu(true);}
            Notice("照片已暂存。确认后上传到拍摄时选定的任务；取消不会扣分。");
        }
        public void UploadPhoto(int id)
        {
            if(!IsAuthority || Finished || ReporterDying || ReporterProtected || Reporter.Hidden || !Reporter.PhoneOpen || PendingPhoto==null || id!=PendingPhoto.id || id<=LastSubmittedPhoto) return;
            var photo=PendingPhoto;PendingPhoto=null;LastSubmittedPhoto=id;
            if(IsTraining) {SetUploadFeedback(true,"练习上传成功","操作已完成。大厅不计分，正式对局需要选对目标与区域。");return;}
            if(!photo.objectCorrect || !photo.regionCorrect)
            {
                Score=Mathf.Max(0,Score-1);
                string reason=!photo.objectCorrect?"物体错误：目标不符、超出距离或被遮挡":"区域错误：道具没有放进指定区域";
                SetUploadFeedback(false,"上传失败 · 扣除 1 次机会",reason+"。剩余失误机会 "+Score+" / "+Settings.maxScore+"。");
                Notice(UploadFeedbackDetail);
                if(Score==0) Finish("主播获胜：狗仔任务失败，失误次数已用尽");
                return;
            }
            if(photo.live) {StreamerPhotos++;}
            else {var quest=Quests.Find(q=>q.Id==photo.taskId);if(quest==null || quest.Complete) return;quest.Complete=true;Abilities.RewardTask(InvestigationData.Tasks(World.Data).Find(t=>t.id==quest.Id)?.reward??TaskReward.None);}
            SelectNextTaskAfterUpload(photo.taskId);
            var reward=InvestigationData.Tasks(World.Data).Find(t=>t.id==photo.taskId)?.reward??TaskReward.None;
            string rewardText=reward==TaskReward.Invisibility?" · 隐身次数 +1":reward==TaskReward.Decoy?" · 假人次数 +1":"";
            SetUploadFeedback(true,"上传成功",photo.live?"直播证据 "+StreamerPhotos+" / 3":"任务完成："+Quests.Find(q=>q.Id==photo.taskId)?.Title+rewardText);
            Notice(UploadFeedbackDetail);
            if(Quests.All(q=>q.Complete) && (!RequiresLiveEvidence || StreamerPhotos>=3)) Finish("狗仔获胜：全部证据已上传");
        }
        void SelectNextTaskAfterUpload(string completedId)
        {
            int index=Quests.FindIndex(q=>q.Id==completedId);
            var next=Quests.Skip(index+1).Concat(Quests.Take(index+1)).FirstOrDefault(q=>!q.Complete);
            SelectedTaskId=next?.Id??(RequiresLiveEvidence && StreamerPhotos<3?InvestigationData.LiveTask:null);
        }
        void UpdatePhone()
        {
            if(!IsNetwork)
            {
                var command=ConsumePhoneCommands();if(command.phone) Reporter.SetPhone(!Reporter.PhoneOpen);ApplyPhoneCommand(command);
            }
        }
        void OnDestroy() {if(PhotoPreview) Destroy(PhotoPreview);}
    }
}
