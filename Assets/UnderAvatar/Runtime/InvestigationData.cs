using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    public enum InvestigationKind { Photograph, PlaceAndPhotograph }
    [Serializable] public sealed class InvestigationTaskData
    {
        public string id=Guid.NewGuid().ToString("N"),title="新任务",description="阅读线索，找到正确的物体并拍照上传。",targetId="";
        public InvestigationKind kind;
        public List<string> clueIds=new List<string>();
        public Vector3 regionPosition=new Vector3(0,.4f,0),regionSize=new Vector3(2,.8f,2);
        public float regionYaw;
        public bool Contains(Vector3 bottom) => new Bounds(Vector3.zero,regionSize).Contains(Quaternion.Euler(0,-regionYaw,0)*(bottom-regionPosition));
    }
    [Serializable] public sealed class TaskPropData
    {
        public string id=Guid.NewGuid().ToString("N"),name="任务道具",definitionId="";
        public Vector3 position,rotation,size=new Vector3(.35f,.3f,.35f);
        public bool portable=true;
    }
    [Serializable] public sealed class TaskClueData
    {
        public string id=Guid.NewGuid().ToString("N"),title="纸条",text="在这里填写线索。";
        public Vector3 position,rotation=new Vector3(90,0,0);
    }
    [Serializable] public sealed class PhotoEvidence
    {
        public int id,broadcast;
        public string taskId,targetId;
        public bool objectCorrect,regionCorrect,live;
    }
    [Serializable] public sealed class TaskPropSnapshot
    {
        public string id;
        public Vector3 position,rotation;
    }
    public static class InvestigationData
    {
        public const string LiveTask="__live";
        public static List<InvestigationTaskData> Tasks(BuildingData map) => map.useTaskEditor?map.tasks:
            map.floors.SelectMany(f=>f.items).Where(i=>i.type==ItemType.QuestItem).Select(i=>new InvestigationTaskData
            {id=i.questId,title=string.IsNullOrWhiteSpace(i.displayName)?i.questDescription:i.displayName,description=i.questDescription,targetId=i.id}).ToList();
        public static void Validate(BuildingData map)
        {
            if(map.tasks==null) map.tasks=new List<InvestigationTaskData>();
            if(map.taskProps==null) map.taskProps=new List<TaskPropData>();
            if(map.taskClues==null) map.taskClues=new List<TaskClueData>();
            if(map.tasks.Count>100 || map.taskProps.Count>200 || map.taskClues.Count>200) throw new ArgumentException("任务最多 100 项，道具和线索各最多 200 个。");
            var ids=new HashSet<string>(map.floors.SelectMany(f=>f.items).Select(i=>i.id));
            foreach(var p in map.taskProps)
                if(p==null || string.IsNullOrWhiteSpace(p.id) || !ids.Add(p.id) || !Vector(p.position,1000) || !Vector(p.rotation,36000) || !Size(p.size,3)) throw new ArgumentException("任务道具 ID、位置或尺寸无效（尺寸须为 0.02–3 米）。");
            var clues=new HashSet<string>();
            foreach(var c in map.taskClues)
                if(c==null || string.IsNullOrWhiteSpace(c.id) || !clues.Add(c.id) || !Vector(c.position,1000) || !Vector(c.rotation,36000) || (c.text?.Length??0)>16000) throw new ArgumentException("线索 ID、位置或文本无效。");
            var tasks=new HashSet<string>();
            foreach(var t in map.tasks)
            {
                if(t==null || string.IsNullOrWhiteSpace(t.id) || t.id==LiveTask || !tasks.Add(t.id) || string.IsNullOrWhiteSpace(t.title) || string.IsNullOrWhiteSpace(t.description) || !Enum.IsDefined(typeof(InvestigationKind),t.kind)) throw new ArgumentException("任务 ID、名称、描述或类型无效。");
                // An empty target is a valid draft; starting a match rejects drafts.
                if(!string.IsNullOrEmpty(t.targetId) && !ids.Contains(t.targetId)) throw new ArgumentException("任务引用的目标物体不存在，请先解除任务绑定。");
                if(!Vector(t.regionPosition,1000) || !Size(t.regionSize,100) || !Finite(t.regionYaw) || (t.clueIds!=null && t.clueIds.Any(c=>!clues.Contains(c)))) throw new ArgumentException("任务区域或线索关联无效。");
                if(t.kind==InvestigationKind.PlaceAndPhotograph && !string.IsNullOrEmpty(t.targetId) && !map.taskProps.Any(p=>p.id==t.targetId && p.portable)) throw new ArgumentException("搬运任务须绑定可拿取的任务道具。");
            }
        }
        public static bool Finite(float v) => !float.IsNaN(v)&&!float.IsInfinity(v);
        static bool Vector(Vector3 v,float max) => Finite(v.x)&&Finite(v.y)&&Finite(v.z)&&Mathf.Abs(v.x)<=max&&Mathf.Abs(v.y)<=max&&Mathf.Abs(v.z)<=max;
        static bool Size(Vector3 v,float max) => Vector(v,max)&&v.x>=.02f&&v.y>=.02f&&v.z>=.02f;
    }
}
