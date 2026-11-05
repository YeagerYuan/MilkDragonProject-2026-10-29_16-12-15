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
        public Texture2D PhotoPreview { get; private set; }
        public int LastSubmittedPhoto { get; private set; }
        int photoSequence;
        bool cameraOnNextPhoneOpen;
        public bool CameraMode => Reporter && Reporter.PhoneOpen && !PhoneMenuOpen && PendingPhoto==null;
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
        public InvestigationTaskData SelectedTask => InvestigationData.Tasks(World.Data).Find(t=>t.id==SelectedTaskId);
        void InitializeInvestigation()
        {
            if(IsTraining) return;
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
        public void EnterPhotoMode()
        {
            if(Finished || IsStreamerPlayer || Reporter.Hidden || PendingPhoto!=null || TaskWorld?.Held) return;
            if(Reporter.PhoneOpen) SetPhoneMenu(false);
            else {cameraOnNextPhoneOpen=true;QueuePhoneAction(close:true);}
        }
        public void QueuePhoneAction(string task=null,int upload=0,bool cancel=false,bool close=false)
        {
            if(Finished || IsStreamerPlayer) return;
            if(task!=null) {SelectTask(task);queuedPhone.taskId=SelectedTaskId;}
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
        public bool TargetInPhoto(string id)
        {
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
            var point=TargetPoint(id);if(!point.HasValue) return false;
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
            if(DocumentManager.BlocksGameplayInput || IsStreamerPlayer || !Reporter.PhoneOpen || Reporter.Hidden || TaskWorld?.Held) return;
            if(PhotoPreview) Destroy(PhotoPreview);
            var camera=Reporter.View;var before=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(640,Mathf.Max(128,Mathf.RoundToInt(640/camera.aspect)),24);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var crop=IsTraining?new Rect(0,0,rt.width,rt.height):new Rect(Mathf.RoundToInt(rt.width*.025f),Mathf.RoundToInt(rt.height*.165f),Mathf.FloorToInt(rt.width*.95f),Mathf.FloorToInt(rt.height*.785f));
                PhotoPreview=new Texture2D((int)crop.width,(int)crop.height,TextureFormat.RGB24,false);PhotoPreview.ReadPixels(crop,0,0);PhotoPreview.Apply();
            }
            finally {camera.targetTexture=before;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }
        void CaptureEvidence()
        {
            if(TaskWorld?.Held) {Notice("请先放下手中的道具，再拍照。");return;}
            if(SelectedTaskId==InvestigationData.LiveTask && !RequiresLiveEvidence) {Notice("此关卡没有额外直播证据任务。");return;}
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
                photo.objectCorrect=TaskInPhoto(task);photo.targetId=photo.objectCorrect?task.targetId:"";
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
            if(!photo.live || StreamerPhotos>=3)
            {
                int index=Quests.FindIndex(q=>q.Id==photo.taskId);
                var next=Quests.Skip(index+1).Concat(Quests.Take(index+1)).FirstOrDefault(q=>!q.Complete);
                SelectedTaskId=next?.Id??(RequiresLiveEvidence && StreamerPhotos<3?InvestigationData.LiveTask:SelectedTaskId);
            }
            if(Quests.All(q=>q.Complete) && (!RequiresLiveEvidence || StreamerPhotos>=3)) Finish("狗仔获胜：全部证据已上传");
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
