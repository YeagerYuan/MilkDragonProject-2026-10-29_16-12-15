using UnityEditor;
using UnityEngine;

namespace UnderAvatar.Editor
{
    public sealed partial class BuildingEditorWindow
    {
        bool editReporterSpawn;
        void DrawReporterSpawn()
        {
            var data=Asset.Data;
            EditorGUILayout.Space();EditorGUILayout.LabelField("狗仔出生 / 复活点",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool custom=EditorGUILayout.Toggle("自定义出生点",data.useCustomReporterSpawn);
            int floor=data.reporterSpawnCell.y,x=data.reporterSpawnCell.x,z=data.reporterSpawnCell.z;
            float yaw=data.reporterSpawnYaw;
            if(custom && !data.useCustomReporterSpawn)
            {
                var cell=new BuildingNavigation(data).Cell(BuildingEditorPreview.World.FindFreePosition(BuildingEditorPreview.Floor));
                floor=cell.y;x=cell.x;z=cell.z;
            }
            if(custom)
            {
                floor=EditorGUILayout.IntSlider("出生楼层",floor+1,1,data.floors.Count)-1;
                x=EditorGUILayout.IntField("网格 X",x);z=EditorGUILayout.IntField("网格 Z",z);
                yaw=EditorGUILayout.Slider("初始朝向",yaw,0,360);
            }
            if(EditorGUI.EndChangeCheck()) Try(()=>BuildingEditorModel.Commit(Asset,"修改狗仔出生点",d=>
            {d.useCustomReporterSpawn=custom;d.reporterSpawnCell=new Vector3Int(x,floor,z);d.reporterSpawnYaw=yaw;}));
            if(custom)
            {
                editReporterSpawn=GUILayout.Toggle(editReporterSpawn,"在 Scene 中拖动出生点","Button");
                if(editReporterSpawn) {painting=false;ClearGhost();}
                if(GUILayout.Button("定位出生点"))
                {BuildingEditorPreview.SetFloor(floor);SceneView.lastActiveSceneView?.Frame(new Bounds(data.ReporterSpawnPosition,Vector3.one*5),false);}
                EditorGUILayout.HelpBox("绿色箭头为狗仔出生及复活位置。拖动后吸附当前出生楼层的网格；保存关卡资源后生效。联机使用房主配置。",MessageType.Info);
            }
        }
        void DrawReporterSpawnHandle()
        {
            var data=Asset.Data;if(!data.useCustomReporterSpawn) return;
            var position=data.ReporterSpawnPosition;var rotation=Quaternion.Euler(0,data.reporterSpawnYaw,0);
            var before=Handles.color;Handles.color=Color.green;
            Handles.DrawWireDisc(position,Vector3.up,.3f);Handles.ArrowHandleCap(0,position+Vector3.up*.2f,rotation,1.2f,EventType.Repaint);
            Handles.Label(position+Vector3.up*2,"狗仔出生 / 复活点 · "+(data.reporterSpawnCell.y+1)+"F");
            if(editReporterSpawn)
            {
                EditorGUI.BeginChangeCheck();var next=Handles.PositionHandle(position,Quaternion.identity);
                if(EditorGUI.EndChangeCheck()) Try(()=>BuildingEditorModel.Commit(Asset,"移动狗仔出生点",d=>
                    d.reporterSpawnCell=new Vector3Int(Mathf.RoundToInt(next.x/d.gridSize),data.reporterSpawnCell.y,Mathf.RoundToInt(next.z/d.gridSize))));
            }
            Handles.color=before;
        }
    }
}
