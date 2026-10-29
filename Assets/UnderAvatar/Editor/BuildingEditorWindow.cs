using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public sealed class BuildingEditorWindow : EditorWindow
    {
        static readonly string[] Names={"墙体","交互门","家具 / 衣柜","任务物品","主播电脑桌","折返楼梯"};
        [SerializeField] bool painting;
        [SerializeField] ItemType brush;
        [SerializeField] int rotation;
        [SerializeField] bool wardrobe=true;
        [SerializeField] string selectedId;
        Vector2 scroll;
        string message="在 Scene 视图编辑关卡，点击 Unity Play 直接测试。";
        GameObject ghost;
        PrototypeVisuals palette;
        bool oldToolsHidden;
        bool questFocus;

        [MenuItem("Tools/Under Avatar/Level Editor")]
        static void OpenFromMenu() => Open();

        public static BuildingEditorWindow Open()
        {
            var window=GetWindow<BuildingEditorWindow>("房屋编辑器");
            window.minSize=new Vector2(320,420);
            BuildingEditorPreview.Restore();
            window.Show();
            return window;
        }
        void OnEnable()
        {
            oldToolsHidden=Tools.hidden;
            SceneView.duringSceneGui+=OnSceneGUI;
            Selection.selectionChanged+=OnSelection;
            BuildingEditorPreview.Updated+=OnPreviewUpdated;
            EditorApplication.playModeStateChanged+=OnPlayMode;
        }
        void OnDisable()
        {
            SceneView.duringSceneGui-=OnSceneGUI;
            Selection.selectionChanged-=OnSelection;
            BuildingEditorPreview.Updated-=OnPreviewUpdated;
            EditorApplication.playModeStateChanged-=OnPlayMode;
            ClearGhost(); Tools.hidden=oldToolsHidden;
        }
        void OnPlayMode(PlayModeStateChange state)
        {
            ClearGhost(); Tools.hidden=oldToolsHidden; Repaint();
        }
        void OnPreviewUpdated() { ClearGhost(); Repaint(); }
        void OnSelection()
        {
            if(BuildingEditorPreview.IsRebuilding) return;
            var item=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<PlacedItemView>():null;
            if(BuildingEditorPreview.Owns(item)) selectedId=item.Data.id;
            else Tools.hidden=oldToolsHidden;
            Repaint();
        }
        BuildingAsset Asset => BuildingEditorPreview.Target?BuildingEditorPreview.Target.Building:null;
        PlacedItemData Selected => Asset?Asset.Data.floors.SelectMany(f=>f.items).FirstOrDefault(i=>i.id==selectedId):null;

        void Try(Action action)
        {
            try { action(); message="已更新关卡。Ctrl+Z 可撤销；点击保存关卡写入资源。"; }
            catch(Exception e) { message=e.Message; }
            Repaint();
        }
        void SelectItem(string id)
        {
            selectedId=id;
            var view=BuildingEditorPreview.World?BuildingEditorPreview.World.Find(id):null;
            if(view) Selection.activeGameObject=view.gameObject;
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("皮套之下 · 关卡制作",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("在此制作关卡。Play 后先选择测试角色，或创建 / 加入局域网房间。",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var target=(PrototypeApp)EditorGUILayout.ObjectField("场景入口",BuildingEditorPreview.Target,typeof(PrototypeApp),true);
                if(target!=BuildingEditorPreview.Target) BuildingEditorPreview.Bind(target);
                if(!target)
                {
                    if(GUILayout.Button("打开原型场景")) { PrototypeSetup.Open(); BuildingEditorPreview.Restore(); }
                    return;
                }
                var asset=(BuildingAsset)EditorGUILayout.ObjectField("关卡资源",target.Building,typeof(BuildingAsset),false);
                if(asset!=target.Building)
                {
                    Undo.RecordObject(target,"指定关卡资源"); target.Building=asset;
                    EditorUtility.SetDirty(target); EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                    BuildingEditorPreview.Rebuild();
                }
                if(!Asset)
                {
                    if(GUILayout.Button("创建关卡资源")) CreateAsset();
                    return;
                }
                scroll=EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.LabelField("测试设置（Play 后选择角色）",EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var role=(TestRole)EditorGUILayout.Popup("操控角色",(int)target.Role,new[]{"狗仔（AI 主播）","主播（AI 狗仔）"});
                int targetScore=EditorGUILayout.IntField("Flappy Bird 通关分数",target.Settings.flappyTargetScore);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target,"调整单人测试规则");
                    target.Role=role; target.Settings.flappyTargetScore=Mathf.Clamp(targetScore,1,100);
                    EditorUtility.SetDirty(target); EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                }
                EditorGUILayout.HelpBox("Play 后可选狗仔 / 主播单人测试，或两台电脑局域网对战。联机使用房主关卡和参数。",MessageType.None);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("楼层",EditorStyles.boldLabel);
                var data=Asset.Data;
                int current=EditorGUILayout.IntSlider("当前楼层",BuildingEditorPreview.Floor+1,1,data.floors.Count)-1;
                if(current!=BuildingEditorPreview.Floor) { selectedId=null; BuildingEditorPreview.SetFloor(current); }
                EditorGUILayout.BeginHorizontal();
                using(new EditorGUI.DisabledScope(current<=0))
                    if(GUILayout.Button("↓ 下一层")) BuildingEditorPreview.SetFloor(current-1);
                using(new EditorGUI.DisabledScope(current>=data.floors.Count-1))
                    if(GUILayout.Button("↑ 上一层")) BuildingEditorPreview.SetFloor(current+1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("统一建筑尺寸（作用于全部楼层）",EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                float wallHeight=EditorGUILayout.DelayedFloatField("墙高 / 层高（米）",data.floorHeight);
                int width=EditorGUILayout.DelayedIntField("楼层宽度（格）",data.WidthCells);
                int depth=EditorGUILayout.DelayedIntField("楼层深度（格）",data.DepthCells);
                if(EditorGUI.EndChangeCheck()) Try(()=>BuildingEditorModel.SetDimensions(Asset,wallHeight,width,depth));
                EditorGUILayout.LabelField("实际尺寸",data.WidthCells*data.gridSize+" × "+data.DepthCells*data.gridSize+" 米");
                EditorGUILayout.HelpBox("墙、楼层间距和楼梯高度同步变化；宽深使用奇数格。缩小时保留全部物品，存在冲突则提示原因。",MessageType.None);
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("新增楼层")) Try(()=>{ BuildingEditorModel.AddFloor(Asset); BuildingEditorPreview.SetFloor(Asset.Data.floors.Count-1); });
                using(new EditorGUI.DisabledScope(data.floors.Count<=1))
                    if(GUILayout.Button("删除本层") && EditorUtility.DisplayDialog("删除楼层","删除本层全部物品？操作可撤销。","删除","取消"))
                        Try(()=>BuildingEditorModel.DeleteFloor(Asset,BuildingEditorPreview.Floor));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Scene 视图工具",EditorStyles.boldLabel);
                bool newPainting=GUILayout.Toggle(painting,"启用网格摆放", "Button");
                if(newPainting!=painting) { painting=newPainting; ClearGhost(); SceneView.RepaintAll(); }
                var newBrush=(ItemType)GUILayout.SelectionGrid((int)brush,Names,2,GUILayout.Height(78));
                if(newBrush!=brush) { brush=newBrush; painting=true; ClearGhost(); }
                int newRotation=EditorGUILayout.IntPopup("摆放旋转",rotation,new[]{"0°","90°","180°","270°"},new[]{0,90,180,270});
                if(newRotation!=rotation) { rotation=newRotation; ClearGhost(); }
                if(brush==ItemType.Stairs)
                    EditorGUILayout.HelpBox("放在下层即可连接上一层，并自动开楼板洞口。占地 2 × "+new StairLayout(data,0,new PlacedItemData()).DepthCells+" 格，两层范围都需空出；上楼直接走台阶。",MessageType.Info);
                if(brush==ItemType.Furniture)
                {
                    bool value=EditorGUILayout.Toggle("作为可躲藏衣柜",wardrobe);
                    if(value!=wardrobe) { wardrobe=value; ClearGhost(); }
                }
                EditorGUILayout.HelpBox(painting
                    ? "左键放置 / 选中，R 旋转，右键删除。Alt + 鼠标保留 Unity 视角操作，Esc 退出摆放。"
                    : "点击场景物品选择；W / E 使用移动或旋转手柄，位置吸附网格。",MessageType.None);
                if(GUILayout.Button("聚焦当前楼层")) BuildingEditorPreview.Frame();
                DrawSelected();

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("保存与测试",EditorStyles.boldLabel);
                if(GUILayout.Button("保存关卡资源",GUILayout.Height(30))) Save();
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("导出 JSON"))
                {
                    string path=EditorUtility.SaveFilePanel("导出地图",Application.dataPath,Asset.name,"json");
                    if(!string.IsNullOrEmpty(path)) Try(()=>BuildingEditorModel.Export(Asset,path));
                }
                if(GUILayout.Button("导入 JSON"))
                {
                    string path=EditorUtility.OpenFilePanel("导入地图",Application.dataPath,"json");
                    if(!string.IsNullOrEmpty(path)) Try(()=>BuildingEditorModel.Import(Asset,path));
                }
                EditorGUILayout.EndHorizontal();
                if(GUILayout.Button("加载默认两层地图") && EditorUtility.DisplayDialog("默认地图","用默认地图替换当前资源内容？操作可撤销。","替换","取消"))
                    Try(()=>BuildingEditorModel.Commit(Asset,"载入默认地图",d=>
                    {
                        var value=BuildingData.CreateDefault(); d.name=value.name; d.floors=value.floors; d.floorHeight=value.floorHeight;
                        d.gridSize=value.gridSize; d.halfWidth=value.halfWidth; d.halfDepth=value.halfDepth;
                    }));
                if(GUILayout.Button("另存为新关卡资源")) CreateAsset();
                bool expand=EditorGUILayout.Toggle("测试时展开 Game 视图",PrototypePlayView.ExpandOnPlay);
                if(expand!=PrototypePlayView.ExpandOnPlay) PrototypePlayView.ExpandOnPlay=expand;
                if(GUILayout.Button("Play 测试当前关卡",GUILayout.Height(32))) StartPlay();
                EditorGUILayout.HelpBox(BuildingEditorPreview.Error??message,BuildingEditorPreview.Error==null?MessageType.None:MessageType.Error);
                EditorGUILayout.EndScrollView();
            }
            if(EditorApplication.isPlaying) EditorGUILayout.HelpBox("正在测试。停止 Unity Play 后继续编辑。",MessageType.Info);
        }
        void Save()
        {
            Try(()=>{ Asset.Data.Validate(); AssetDatabase.SaveAssetIfDirty(Asset); });

        }
        void CreateAsset()
        {
            string path=EditorUtility.SaveFilePanelInProject("保存关卡资源","NewBuilding","asset","选择关卡资源位置");
            if(string.IsNullOrEmpty(path)) return;
            Try(()=>
            {
                var value=CreateInstance<BuildingAsset>(); if(Asset) value.SetData(Asset.Data);
                AssetDatabase.CreateAsset(value,path);
                var target=BuildingEditorPreview.Target; Undo.RecordObject(target,"指定新关卡");
                target.Building=value; EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                AssetDatabase.SaveAssets(); BuildingEditorPreview.Rebuild();
            });
        }
        void StartPlay()
        {
            Try(()=>
            {
                var data=Asset.CreateRuntimeCopy();
                StairLayout.Validate(data);
                if(data.floors.SelectMany(f=>f.items).Count(i=>i.type==ItemType.StreamerPC)!=1)
                    throw new InvalidOperationException("关卡需要一个主播电脑桌。");
                AssetDatabase.SaveAssetIfDirty(Asset); painting=false; ClearGhost(); EditorApplication.isPlaying=true;
            });
        }
        void DrawSelected()
        {
            var item=Selected;
            if(item==null) return;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("已选中："+Names[(int)item.type],EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int x=EditorGUILayout.DelayedIntField("网格 X",item.gridX);
            int z=EditorGUILayout.DelayedIntField("网格 Z",item.gridZ);
            int angle=EditorGUILayout.IntPopup("物品旋转",item.rotationY,new[]{"0°","90°","180°","270°"},new[]{0,90,180,270});
            string id=item.questId,description=item.questDescription;
            bool hideable=item.hideable;
            if(item.type==ItemType.QuestItem)
            {
                GUI.SetNextControlName("QuestId");
                id=EditorGUILayout.DelayedTextField("Quest ID",id);
                description=EditorGUILayout.DelayedTextField("任务说明",description);
                if(questFocus) { EditorGUI.FocusTextInControl("QuestId"); questFocus=false; }
            }
            if(item.type==ItemType.Furniture) hideable=EditorGUILayout.Toggle("可躲藏衣柜",hideable);
            if(EditorGUI.EndChangeCheck())
                Try(()=>BuildingEditorModel.EditItem(Asset,item.id,i=>{i.gridX=x;i.gridZ=z;i.rotationY=angle;i.questId=id;i.questDescription=description;i.hideable=hideable;}));
            if(GUILayout.Button("删除选中物品")) Try(()=>{ BuildingEditorModel.Delete(Asset,item.id); selectedId=null; });
        }

        void OnSceneGUI(SceneView view)
        {
            if(!Asset || !BuildingEditorPreview.World || EditorApplication.isPlayingOrWillChangePlaymode) { ClearGhost(); Tools.hidden=oldToolsHidden; return; }
            var e=Event.current;
            var selectedView=Selection.activeGameObject?Selection.activeGameObject.GetComponentInParent<PlacedItemView>():null;
            bool ownsSelection=BuildingEditorPreview.Owns(selectedView);
            Tools.hidden=painting || ownsSelection ? true : oldToolsHidden;
            if(!painting)
            {
                if(ghost) ghost.SetActive(false);
                if(ownsSelection) DrawHandle(selectedView);
                return;
            }
            if(e.alt) { if(ghost) ghost.SetActive(false); return; }
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout) HandleUtility.AddDefaultControl(control);
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape) { painting=false; ClearGhost(); e.Use(); Repaint(); return; }
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.R) { rotation=(rotation+90)%360; ClearGhost(); e.Use(); Repaint(); }
            int floor=BuildingEditorPreview.Floor;
            var data=Asset.Data;
            Ray ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane=new Plane(Vector3.up,Vector3.up*(floor*data.floorHeight));
            if(!plane.Raycast(ray,out float distance)) return;
            Vector3 position=ray.GetPoint(distance);
            int x=Mathf.RoundToInt(position.x/data.gridSize),z=Mathf.RoundToInt(position.z/data.gridSize);
            if(!data.ContainsCell(x,z)) { if(ghost) ghost.SetActive(false); return; }
            var existing=BuildingEditorPreview.World.ItemAt(floor,x,z)?.Data;
            if(existing==null)
            {
                EnsureGhost();
                ghost.transform.position=new Vector3(x*data.gridSize,floor*data.floorHeight,z*data.gridSize);
                ghost.SetActive(true);
            }
            else if(ghost) ghost.SetActive(false);
            if(e.type==EventType.MouseDown && e.button==0)
            {
                if(existing!=null) SelectItem(existing.id);
                else Try(()=>
                {
                    string id=BuildingEditorModel.Place(Asset,floor,x,z,brush,rotation,wardrobe);
                    SelectItem(id);
                    if(brush==ItemType.QuestItem) { questFocus=true; Focus(); }
                });
                e.Use();
            }
            else if(e.type==EventType.MouseDown && e.button==1)
            {
                if(existing!=null) Try(()=>BuildingEditorModel.Delete(Asset,existing.id));
                e.Use();
            }
            if(e.type==EventType.MouseMove) { view.Repaint(); Repaint(); }
            Handles.Label(new Vector3(x*data.gridSize,floor*data.floorHeight+.2f,z*data.gridSize),"("+x+", "+z+") · "+(floor+1)+"F");
        }
        void DrawHandle(PlacedItemView item)
        {
            if(Tools.current!=Tool.Rotate && Tools.current!=Tool.Move) return;
            EditorGUI.BeginChangeCheck();
            Vector3 point=item.transform.position;
            Quaternion orientation=item.transform.rotation;
            if(Tools.current==Tool.Rotate) orientation=Handles.RotationHandle(orientation,point);
            else if(Tools.current==Tool.Move) point=Handles.PositionHandle(point,Quaternion.identity);
            if(!EditorGUI.EndChangeCheck()) return;
            int x=Mathf.RoundToInt(point.x/Asset.Data.gridSize),z=Mathf.RoundToInt(point.z/Asset.Data.gridSize);
            int angle=((Mathf.RoundToInt(orientation.eulerAngles.y/90)*90)%360+360)%360;
            if(x==item.Data.gridX && z==item.Data.gridZ && angle==item.Data.rotationY) return;
            Try(()=>BuildingEditorModel.EditItem(Asset,item.Data.id,i=>{i.gridX=x;i.gridZ=z;i.rotationY=angle;}));
        }
        void EnsureGhost()
        {
            if(ghost) return;
            palette=new PrototypeVisuals(); ghost=new GameObject("摆放预览");
            ghost.AddComponent<PlacedItemView>().Initialize(new PlacedItemData{type=brush,rotationY=rotation,hideable=wardrobe},
                BuildingEditorPreview.Floor,Asset.Data.gridSize,Asset.Data.floorHeight,palette,true);
            BuildingEditorPreview.SetFlags(ghost);
            ghost.hideFlags|=HideFlags.HideInHierarchy;
        }
        void ClearGhost()
        {
            if(ghost) DestroyImmediate(ghost);
            ghost=null; palette?.Dispose(); palette=null;
        }
    }

    [CustomEditor(typeof(PlacedItemView))]
    public sealed class PlacedItemViewInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var item=(PlacedItemView)target;
            if(!BuildingEditorPreview.Owns(item)) { DrawDefaultInspector(); return; }
            EditorGUILayout.LabelField("关卡物品",item.Data.type.ToString());
            EditorGUILayout.LabelField("楼层 / 网格",(item.Floor+1)+"F / "+item.Data.gridX+", "+item.Data.gridZ);
            if(item.Data.type==ItemType.QuestItem)
            { EditorGUILayout.LabelField("Quest ID",item.Data.questId); EditorGUILayout.LabelField("任务说明",item.Data.questDescription); }
            EditorGUILayout.HelpBox("在房屋编辑器中修改属性。关闭摆放模式后，W/E 手柄支持网格移动和 90° 旋转。",MessageType.Info);
            if(GUILayout.Button("打开房屋编辑器")) BuildingEditorWindow.Open();
        }
    }
}
