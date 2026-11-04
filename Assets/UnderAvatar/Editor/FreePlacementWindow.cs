using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public sealed class FreePlacementWindow : EditorWindow
    {
        static FreePlacementWindow instance;
        public static bool EditingScene => instance && instance.Asset && (instance.placeOnSurface || instance.Selected!=null);
        [SerializeField] GameObject newModel;
        [SerializeField] string selectedId;
        [SerializeField] bool placeOnSurface;
        Vector2 scroll;
        string message="装饰物自由摆放，不占网格，也不会自动成为任务目标。";
        BuildingAsset Asset => BuildingEditorPreview.Target?BuildingEditorPreview.Target.Building:null;
        FreePlacedObjectData Selected => Asset?Asset.Data.decorations.Find(d=>d.id==selectedId):null;

        public static void EndSceneEditing() {if(instance) {instance.selectedId=null;instance.placeOnSurface=false;}}

        [MenuItem("Tools/Under Avatar/Free Placement",true)]
        static bool CanOpen() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/Under Avatar/Free Placement")]
        public static void Open()
        {
            TaskEditorWindow.EndSceneEditing();BuildingEditorPreview.Restore();var window=GetWindow<FreePlacementWindow>("自由摆放");window.minSize=new Vector2(360,440);window.Show();
        }
        void OnEnable() {instance=this;SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=Repaint;}
        void OnDisable() {if(instance==this) instance=null;SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=Repaint;SceneView.RepaintAll();}
        void Edit(string label,Action<BuildingData> action)
        {
            try
            {
                BuildingEditorModel.Commit(Asset,label,action);
                if(Selected!=null) BuildingEditorPreview.SetFloor(Mathf.FloorToInt(Selected.position.y/Asset.Data.floorHeight));
                message="已更新。Ctrl+Z 撤销；点击保存关卡资源保存。";
            }
            catch(Exception e) {message=e.Message;}
            Repaint();SceneView.RepaintAll();
        }
        string ModelKey(GameObject model)
        {
            if(!model || !EditorUtility.IsPersistent(model) || !AssetDatabase.Contains(model)) throw new ArgumentException("请从 Project 拖入模型或 Prefab，不能使用场景实例。");
            var catalog=LevelObjectCatalogEditor.EnsureCatalog();
            var existing=catalog.objects.Find(d=>d.prefab==model);
            if(existing!=null) return existing.id;
            Undo.RecordObject(catalog,"添加自由摆放模型");
            var definition=new LevelObjectDefinition{displayName=model.name,prefab=model};catalog.objects.Add(definition);
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);return definition.id;
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("自由摆放装饰物",EditorStyles.boldLabel);
            if(!Asset) {EditorGUILayout.HelpBox("请先在 Level Editor 选择场景入口和关卡资源。",MessageType.Info);if(GUILayout.Button("打开 Level Editor")) BuildingEditorWindow.Open();return;}
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.ObjectField("关卡资源",Asset,typeof(BuildingAsset),false);
                EditorGUILayout.HelpBox("用于柜顶、桌面等位置的普通装饰；不生成拍照任务，不可拿取。使用底部位置放在表面上。",MessageType.Info);
                newModel=(GameObject)EditorGUILayout.ObjectField("新增物体模型",newModel,typeof(GameObject),false);
                using(new EditorGUI.DisabledScope(!newModel)) if(GUILayout.Button("＋ 新增装饰物"))
                {
                    try
                    {
                        var key=ModelKey(newModel);
                        var position=SceneView.lastActiveSceneView?SceneView.lastActiveSceneView.pivot:Vector3.zero;
                        position.x=Mathf.Clamp(position.x,-Asset.Data.halfWidth*Asset.Data.gridSize,Asset.Data.halfWidth*Asset.Data.gridSize);
                        position.z=Mathf.Clamp(position.z,-Asset.Data.halfDepth*Asset.Data.gridSize,Asset.Data.halfDepth*Asset.Data.gridSize);
                        position.y=BuildingEditorPreview.Floor*Asset.Data.floorHeight;
                        string id=Guid.NewGuid().ToString("N");
                        Edit("新增自由摆放物体",d=>d.decorations.Add(new FreePlacedObjectData{id=id,name=newModel.name,definitionId=key,position=position}));
                        TaskEditorWindow.EndSceneEditing();selectedId=id;placeOnSurface=true;Selection.activeObject=null;SceneView.RepaintAll();
                    }
                    catch(Exception e) {message=e.Message;}
                }
                scroll=EditorGUILayout.BeginScrollView(scroll);
                foreach(var item in Asset.Data.decorations)
                {
                    int floor=Mathf.FloorToInt(item.position.y/Asset.Data.floorHeight);
                    if(GUILayout.Toggle(item.id==selectedId,item.name+"  · "+(floor+1)+" 楼","Button") && selectedId!=item.id)
                    {TaskEditorWindow.EndSceneEditing();selectedId=item.id;placeOnSurface=false;Selection.activeObject=null;BuildingEditorPreview.SetFloor(floor);SceneView.RepaintAll();}
                }
                var selected=Selected;
                if(selected!=null)
                {
                    EditorGUILayout.Space();EditorGUI.BeginChangeCheck();
                    string name=EditorGUILayout.DelayedTextField("名称",selected.name);
                    var position=EditorGUILayout.Vector3Field("底部位置（米）",selected.position);
                    var rotation=EditorGUILayout.Vector3Field("旋转（度）",selected.rotation);
                    var size=EditorGUILayout.Vector3Field("包围尺寸（米）",selected.size);
                    bool solid=EditorGUILayout.Toggle("启用碰撞",selected.solid);
                    if(EditorGUI.EndChangeCheck()) Edit("修改自由摆放物体",d=>{var item=d.decorations.Find(x=>x.id==selectedId);item.name=name;item.position=position;item.rotation=rotation;item.size=size;item.solid=solid;});
                    EditorGUILayout.HelpBox("模型按比例缩放到包围尺寸内，底部居中。碰撞开启时会阻挡玩家和摄影视线。模型中的交互脚本及动画不运行。",MessageType.None);
                    var catalog=LevelObjectCatalog.Default;
                    var definition=catalog?catalog.objects.Find(d=>d.id==selected.definitionId):null;
                    var model=(GameObject)EditorGUILayout.ObjectField("模型 / Prefab",definition?.prefab,typeof(GameObject),false);
                    if(model && model!=definition?.prefab)
                    {
                        try {var key=ModelKey(model);Edit("替换装饰模型",d=>d.decorations.Find(x=>x.id==selectedId).definitionId=key);}
                        catch(Exception e) {message=e.Message;}
                    }
                    if(GUILayout.Button(placeOnSurface?"取消点击放置（Esc）":"点击柜顶 / 桌面 / 地板放置")) {placeOnSurface=!placeOnSurface;SceneView.RepaintAll();}
                    if(GUILayout.Button("聚焦所选物体"))
                    {var view=SceneView.lastActiveSceneView??GetWindow<SceneView>();view.LookAt(selected.position,Quaternion.Euler(30,-30,0),Mathf.Max(2,selected.size.magnitude*1.5f));view.Show();}
                    if(GUILayout.Button("结束编辑")) {selectedId=null;placeOnSurface=false;SceneView.RepaintAll();}
                    if(GUILayout.Button("删除所选装饰物")) {var id=selectedId;Edit("删除自由摆放物体",d=>d.decorations.RemoveAll(x=>x.id==id));selectedId=null;placeOnSurface=false;}
                }
                EditorGUILayout.EndScrollView();
                if(GUILayout.Button("保存关卡资源",GUILayout.Height(30))) {AssetDatabase.SaveAssetIfDirty(Asset);message="关卡已保存。";}
                EditorGUILayout.HelpBox(message,MessageType.None);
                EditorGUILayout.LabelField("Scene：W 移动 · E 旋转 · R 调整尺寸；支持任意位置，不吸附网格。",EditorStyles.wordWrappedLabel);
            }
        }
        void SceneGUI(SceneView view)
        {
            if(!Asset || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null) return;
            var selected=Selected;if(selected==null) return;
            var e=Event.current;if(e.alt) return;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape) {placeOnSurface=false;selectedId=null;e.Use();Repaint();return;}
            if(placeOnSurface)
            {
                if(e.type==EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                Handles.BeginGUI();GUI.Label(new Rect(12,12,390,25),"点击柜顶、桌面或地板放置；Esc 退出");Handles.EndGUI();
                if(e.type==EventType.MouseDown && e.button==0)
                {
                    Physics.SyncTransforms();var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    var world=BuildingEditorPreview.World;
                    var hits=Physics.RaycastAll(ray,1000,~LayerMask.GetMask("UA_Sound"),QueryTriggerInteraction.Ignore)
                        .Where(h=>world && h.collider.transform.IsChildOf(world.transform) && h.collider.GetComponentInParent<FreePlacedObjectView>()?.Data.id!=selectedId)
                        .OrderBy(h=>h.distance).ToArray();
                    Vector3 point;
                    if(hits.Length>0)
                    {
                        if(hits[0].normal.y<.5f) {message="请点击朝上的表面。";e.Use();Repaint();return;}
                        point=hits[0].point+Vector3.up*.005f;
                    }
                    else
                    {
                        var plane=new Plane(Vector3.up,Vector3.up*BuildingEditorPreview.Floor*Asset.Data.floorHeight);
                        if(!plane.Raycast(ray,out var distance)) return;point=ray.GetPoint(distance);
                    }
                    Edit("放置到表面",d=>d.decorations.Find(x=>x.id==selectedId).position=point);
                    placeOnSurface=false;e.Use();
                }
                return;
            }
            EditorGUI.BeginChangeCheck();
            var position=selected.position;var rotation=Quaternion.Euler(selected.rotation);var size=selected.size;
            if(Tools.current==Tool.Rotate) rotation=Handles.RotationHandle(rotation,position);
            else if(Tools.current==Tool.Scale) size=Handles.ScaleHandle(size,position,rotation,HandleUtility.GetHandleSize(position));
            else position=Handles.PositionHandle(position,Quaternion.identity);
            if(EditorGUI.EndChangeCheck()) Edit("调整自由摆放物体",d=>{var item=d.decorations.Find(x=>x.id==selectedId);item.position=position;item.rotation=rotation.eulerAngles;item.size=Vector3.Max(Vector3.one*.1f,size);});
            Handles.Label(position+Vector3.up*selected.size.y,selected.name);
        }
    }
}
