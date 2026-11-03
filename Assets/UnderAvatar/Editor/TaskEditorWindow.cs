using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public sealed class TaskEditorWindow : EditorWindow
    {
        static TaskEditorWindow instance;
        public static bool EditingScene => instance && instance.Asset && instance.Asset.Data.useTaskEditor && instance.SelectedPosition().HasValue;
        int tab,index=-1;
        Vector2 scroll;
        string error;
        readonly BoxBoundsHandle boundsHandle=new BoxBoundsHandle();
        BuildingAsset Asset => BuildingEditorPreview.Target?BuildingEditorPreview.Target.Building:null;
        [MenuItem("Tools/Under Avatar/Task Editor")]
        public static void Open() {BuildingEditorPreview.Restore();var window=GetWindow<TaskEditorWindow>("任务编辑器");window.minSize=new Vector2(370,450);window.Show();}
        void OnEnable() {instance=this;SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=Repaint;}
        void OnDisable() {SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=Repaint;}
        void Edit(string label,Action<BuildingData> action)
        {
            try {BuildingEditorModel.Commit(Asset,label,action);error=null;}
            catch(Exception e) {error=e.Message;}
            Repaint();
        }
        Vector3 Spawn()
        {
            var view=SceneView.lastActiveSceneView;var p=view?view.pivot:Vector3.zero;
            p.y=BuildingEditorPreview.Floor*Asset.Data.floorHeight+.04f;return p;
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("任务、目标物体与线索",EditorStyles.boldLabel);
            if(!Asset) {EditorGUILayout.HelpBox("请先打开房屋编辑器并选择关卡资源。",MessageType.Info);if(GUILayout.Button("打开房屋编辑器")) BuildingEditorWindow.Open();return;}
            EditorGUILayout.ObjectField("关卡资源",Asset,typeof(BuildingAsset),false);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if(!Asset.Data.useTaskEditor)
                {
                    EditorGUILayout.HelpBox("当前使用旧任务。点击下方按钮将其导入任务编辑器；已有物体、位置和任务 ID 保留。",MessageType.Info);
                    if(GUILayout.Button("启用任务编辑并导入现有任务")) Edit("导入现有任务",d=>{d.tasks=InvestigationData.Tasks(d);d.useTaskEditor=true;});
                    return;
                }
                int next=GUILayout.Toolbar(tab,new[]{"任务","道具","线索"});if(next!=tab) {tab=next;index=-1;}
                scroll=EditorGUILayout.BeginScrollView(scroll);
                var names=tab==0?Asset.Data.tasks.Select(t=>t.title).ToArray():tab==1?Asset.Data.taskProps.Select(p=>p.name).ToArray():Asset.Data.taskClues.Select(c=>c.title).ToArray();
                for(int i=0;i<names.Length;i++) if(GUILayout.Toggle(index==i,names[i],"Button")) index=i;
                if(GUILayout.Button("＋ 新增"+(tab==0?"任务":tab==1?"道具":"线索")))
                {
                    Vector3 position=Spawn();
                    Edit("新增任务内容",d=>
                    {
                        if(tab==0) {d.tasks.Add(new InvestigationTaskData{regionPosition=position+Vector3.up*.4f});index=d.tasks.Count-1;}
                        if(tab==1) {d.taskProps.Add(new TaskPropData{position=position});index=d.taskProps.Count-1;}
                        if(tab==2) {d.taskClues.Add(new TaskClueData{position=position});index=d.taskClues.Count-1;}
                    });
                }
                EditorGUILayout.Space(10);
                if(index>=0 && index<names.Length)
                {
                    if(tab==0) DrawTask();else if(tab==1) DrawProp();else DrawClue();
                    if(GUILayout.Button("聚焦所选内容")) FrameSelected();
                    if(GUILayout.Button("结束 Scene 编辑")) index=-1;
                    if(GUILayout.Button("删除所选"))
                    {
                        Edit("删除任务内容",d=>
                        {
                            if(tab==0) d.tasks.RemoveAt(index);
                            if(tab==1)
                            {
                                string id=d.taskProps[index].id;
                                if(d.tasks.Any(t=>t.targetId==id)) throw new ArgumentException("请先解除任务对这个道具的目标绑定。");
                                d.taskProps.RemoveAt(index);
                            }
                            if(tab==2) {string id=d.taskClues[index].id;d.taskClues.RemoveAt(index);foreach(var t in d.tasks) t.clueIds?.Remove(id);}
                        });
                        index=-1;
                    }
                }
                EditorGUILayout.EndScrollView();
                if(GUILayout.Button("保存任务与关卡",GUILayout.Height(32))) {AssetDatabase.SaveAssetIfDirty(Asset);error="已保存。";}
                EditorGUILayout.HelpBox(error??"Scene 中使用 W/E 移动、旋转所选道具或线索；搬运任务区域显示半透明框，可拖动框边调整大小。运行时区域隐藏。",MessageType.Info);
                EditorGUILayout.HelpBox("未绑定目标的任务是草稿，开始对局前需要完成绑定。预制体可在“道具”页指定；线索为可编辑文本，不强制阅读。联机双方需使用同一套模型资源。",MessageType.None);
            }
        }
        void DrawTask()
        {
            var t=Asset.Data.tasks[index];
            EditorGUI.BeginChangeCheck();
            string title=EditorGUILayout.DelayedTextField("任务名称",t.title);
            var kind=(InvestigationKind)EditorGUILayout.Popup("任务类型",(int)t.kind,new[]{"拍摄类","搬运后拍摄"});
            EditorGUILayout.LabelField("手机任务描述");string description=EditorGUILayout.TextArea(t.description,GUILayout.MinHeight(70));
            var ids=new[]{""}.Concat(Asset.Data.taskProps.Select(p=>p.id)).Concat(Asset.Data.floors.SelectMany(f=>f.items).Where(p=>p.type==ItemType.QuestItem).Select(p=>p.id)).ToArray();
            var labels=new[]{"尚未绑定"}.Concat(Asset.Data.taskProps.Select(p=>p.name)).Concat(Asset.Data.floors.SelectMany(f=>f.items).Where(p=>p.type==ItemType.QuestItem).Select(p=>string.IsNullOrEmpty(p.displayName)?p.questDescription:p.displayName)).ToArray();
            int target=EditorGUILayout.Popup("正确目标物体",Mathf.Max(0,Array.IndexOf(ids,t.targetId)),labels);
            Vector3 position=t.regionPosition,size=t.regionSize;float yaw=t.regionYaw;
            if(kind==InvestigationKind.PlaceAndPhotograph)
            {
                position=EditorGUILayout.Vector3Field("目标区域中心",position);size=EditorGUILayout.Vector3Field("目标区域尺寸",size);yaw=EditorGUILayout.FloatField("区域旋转 Y",yaw);
            }
            if(EditorGUI.EndChangeCheck()) Edit("修改任务",d=>{var v=d.tasks[index];v.title=title;v.description=description;v.kind=kind;v.targetId=ids[target];v.regionPosition=position;v.regionSize=size;v.regionYaw=yaw;});
            EditorGUILayout.LabelField("关联线索（仅编辑辅助，不强制阅读）",EditorStyles.boldLabel);
            foreach(var c in Asset.Data.taskClues)
            {
                bool included=t.clueIds!=null && t.clueIds.Contains(c.id);bool value=EditorGUILayout.ToggleLeft(c.title,included);
                if(value!=included) Edit("关联任务线索",d=>{var v=d.tasks[index];if(v.clueIds==null) v.clueIds=new System.Collections.Generic.List<string>();if(value) v.clueIds.Add(c.id);else v.clueIds.Remove(c.id);});
            }
        }
        void DrawProp()
        {
            var p=Asset.Data.taskProps[index];var catalog=LevelObjectCatalogEditor.EnsureCatalog();
            var definition=catalog.objects.Find(d=>d.id==p.definitionId);
            EditorGUI.BeginChangeCheck();
            string name=EditorGUILayout.DelayedTextField("道具名称",p.name);
            bool portable=EditorGUILayout.Toggle("允许狗仔拿取",p.portable);
            var position=EditorGUILayout.Vector3Field("底部位置",p.position);var rotation=EditorGUILayout.Vector3Field("旋转",p.rotation);var size=EditorGUILayout.Vector3Field("碰撞与模型尺寸",p.size);
            if(EditorGUI.EndChangeCheck()) Edit("修改任务道具",d=>{var v=d.taskProps[index];v.name=name;v.portable=portable;v.position=position;v.rotation=rotation;v.size=size;});
            var prefab=(GameObject)EditorGUILayout.ObjectField("外观预制体",definition?.prefab,typeof(GameObject),false);
            if(prefab!=(definition?.prefab))
            {
                string key=string.IsNullOrEmpty(p.definitionId)?"task_"+p.id:p.definitionId;
                Undo.RecordObject(catalog,"指定任务道具模型");
                if(definition==null) {definition=new LevelObjectDefinition{id=key,displayName=p.name};catalog.objects.Add(definition);}
                definition.prefab=prefab;EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
                Edit("指定任务道具模型",d=>d.taskProps[index].definitionId=key);
            }
            EditorGUILayout.HelpBox("可拖入模型或 Prefab，自动缩放到上述尺寸。仅小道具设为可拿取；桌子等位置线索设为不可拿取。留空使用占位模型。",MessageType.None);
        }
        void DrawClue()
        {
            var c=Asset.Data.taskClues[index];EditorGUI.BeginChangeCheck();
            string title=EditorGUILayout.DelayedTextField("线索名称",c.title);
            EditorGUILayout.LabelField("阅读内容");string text=EditorGUILayout.TextArea(c.text,GUILayout.MinHeight(150));
            var position=EditorGUILayout.Vector3Field("位置",c.position);var rotation=EditorGUILayout.Vector3Field("旋转",c.rotation);
            if(EditorGUI.EndChangeCheck()) Edit("修改任务线索",d=>{var v=d.taskClues[index];v.title=title;v.text=text;v.position=position;v.rotation=rotation;});
        }
        Vector3? SelectedPosition()
        {
            if(!Asset || index<0) return null;
            if(tab==0 && index<Asset.Data.tasks.Count)
            {
                var t=Asset.Data.tasks[index];if(t.kind==InvestigationKind.PlaceAndPhotograph) return t.regionPosition;
                var p=Asset.Data.taskProps.Find(v=>v.id==t.targetId);if(p!=null) return p.position;
                foreach(var f in Asset.Data.floors) {var i=f.items.Find(v=>v.id==t.targetId);if(i!=null) return Asset.Data.Position(f.floorIndex,i);}
            }
            if(tab==1 && index<Asset.Data.taskProps.Count) return Asset.Data.taskProps[index].position;
            if(tab==2 && index<Asset.Data.taskClues.Count) return Asset.Data.taskClues[index].position;
            return null;
        }
        void FrameSelected()
        {
            var p=SelectedPosition();if(!p.HasValue) return;
            BuildingEditorPreview.SetFloor(Mathf.Clamp(Mathf.FloorToInt(p.Value.y/Asset.Data.floorHeight),0,Asset.Data.floors.Count-1));
            var view=SceneView.lastActiveSceneView??GetWindow<SceneView>();view.sceneLighting=false;view.LookAt(p.Value,Quaternion.Euler(35,-25,0),4);view.Show();view.Focus();
        }
        void SceneGUI(SceneView view)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()!=null || !Asset || !Asset.Data.useTaskEditor) return;
            foreach(var task in Asset.Data.tasks.Where(t=>t.kind==InvestigationKind.PlaceAndPhotograph))
            {
                if(task.regionPosition.y>(BuildingEditorPreview.Floor+1)*Asset.Data.floorHeight) continue;
                using(new Handles.DrawingScope(new Color(.2f,1,.6f,.45f),Matrix4x4.TRS(task.regionPosition,Quaternion.Euler(0,task.regionYaw,0),Vector3.one)))
                {
                    Handles.DrawWireCube(Vector3.zero,task.regionSize);
                    var h=task.regionSize*.5f;
                    Handles.DrawSolidRectangleWithOutline(new[]{new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z)},new Color(.1f,.9f,.4f,.1f),new Color(.2f,1,.6f,.6f));
                }
                Handles.Label(task.regionPosition,task.title+" · 正确放置区域");
            }
            var position=SelectedPosition();if(!position.HasValue) return;
            if(tab==0)
            {
                var task=Asset.Data.tasks[index];
                var target=Asset.Data.taskProps.Find(v=>v.id==task.targetId);
                var existing=BuildingEditorPreview.World?BuildingEditorPreview.World.Find(task.targetId):null;
                Vector3 targetPosition=target!=null?target.position:existing?existing.transform.position:position.Value;
                Vector3 targetSize=target!=null?target.size:Vector3.one;
                using(new Handles.DrawingScope(Color.yellow))
                {
                    Handles.DrawWireCube(targetPosition+Vector3.up*targetSize.y*.5f,targetSize*1.1f);
                    Handles.Label(targetPosition+Vector3.up*(targetSize.y+.2f),"正确目标");
                    foreach(var clue in Asset.Data.taskClues.Where(c=>task.clueIds!=null && task.clueIds.Contains(c.id)))
                    {Handles.DrawWireDisc(clue.position,Vector3.up,.4f);Handles.Label(clue.position+Vector3.up*.2f,"线索："+clue.title);}
                }
                if(task.kind!=InvestigationKind.PlaceAndPhotograph) return;
                EditorGUI.BeginChangeCheck();var p=Handles.PositionHandle(task.regionPosition,Quaternion.identity);
                var rotation=Handles.RotationHandle(Quaternion.Euler(0,task.regionYaw,0),p);
                if(EditorGUI.EndChangeCheck()) Edit("移动任务区域",d=>{d.tasks[index].regionPosition=p;d.tasks[index].regionYaw=rotation.eulerAngles.y;});
                using(new Handles.DrawingScope(Matrix4x4.TRS(task.regionPosition,Quaternion.Euler(0,task.regionYaw,0),Vector3.one)))
                {
                    boundsHandle.center=Vector3.zero;boundsHandle.size=task.regionSize;EditorGUI.BeginChangeCheck();boundsHandle.DrawHandle();
                    if(EditorGUI.EndChangeCheck()) {var center=boundsHandle.center;var size=boundsHandle.size;Edit("调整任务区域",d=>{d.tasks[index].regionPosition+=Quaternion.Euler(0,task.regionYaw,0)*center;d.tasks[index].regionSize=size;});}
                }
                return;
            }
            Vector3 euler=tab==1?Asset.Data.taskProps[index].rotation:Asset.Data.taskClues[index].rotation;
            EditorGUI.BeginChangeCheck();var point=position.Value;var orientation=Quaternion.Euler(euler);
            if(Tools.current==Tool.Rotate) orientation=Handles.RotationHandle(orientation,point);else point=Handles.PositionHandle(point,Quaternion.identity);
            if(EditorGUI.EndChangeCheck()) Edit("移动任务内容",d=>{if(tab==1) {d.taskProps[index].position=point;d.taskProps[index].rotation=orientation.eulerAngles;}else {d.taskClues[index].position=point;d.taskClues[index].rotation=orientation.eulerAngles;}});
            Handles.Label(point,tab==1?Asset.Data.taskProps[index].name:Asset.Data.taskClues[index].title);
        }
    }
}
