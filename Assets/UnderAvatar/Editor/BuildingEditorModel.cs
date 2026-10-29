using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace UnderAvatar.Editor
{
    public static class BuildingEditorModel
    {
        public static event Action<BuildingAsset> Changed;
        public static void Commit(BuildingAsset asset,string label,Action<BuildingData> edit)
        {
            if(!asset) throw new ArgumentNullException(nameof(asset));
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play，再编辑关卡。");
            var data=asset.CreateRuntimeCopy(); edit(data); data.Validate(); StairLayout.Validate(data);
            Undo.RegisterCompleteObjectUndo(asset,label);
            asset.SetData(data); EditorUtility.SetDirty(asset); Changed?.Invoke(asset);
        }
        public static string Place(BuildingAsset asset,int floor,int x,int z,ItemType type,int rotation,bool hideable)
        {
            string id=Guid.NewGuid().ToString("N");
            Commit(asset,"放置 "+type,data=>
            {
                if(floor<0 || floor>=data.floors.Count || !data.ContainsCell(x,z)) throw new ArgumentException("超出当前楼层范围。");
                if(data.At(floor,x,z)!=null) throw new ArgumentException("这个网格已有物品。");
                var item=new PlacedItemData{id=id,type=type,gridX=x,gridZ=z,rotationY=rotation,hideable=type==ItemType.Furniture && hideable};
                if(type==ItemType.QuestItem) { item.questId="item_"+id.Substring(0,8); item.questDescription="拍摄第 "+(floor+1)+" 层的线索"; }
                data.floors[floor].items.Add(item);
            });
            return id;
        }
        public static void SetDimensions(BuildingAsset asset,float wallHeight,int widthCells,int depthCells)
        {
            if(widthCells%2!=1 || depthCells%2!=1) throw new ArgumentException("宽度和深度使用奇数格（以中心网格对称延伸）。");
            Commit(asset,"修改全部楼层尺寸",data=>
            {
                data.floorHeight=wallHeight; data.halfWidth=(widthCells-1)/2; data.halfDepth=(depthCells-1)/2;
            });
        }

        public static void Delete(BuildingAsset asset,string id) => Commit(asset,"删除关卡物品",data=>
        {
            foreach(var floor in data.floors) floor.items.RemoveAll(i=>i.id==id);
        });
        public static void AddFloor(BuildingAsset asset) => Commit(asset,"新增楼层",data=>
        {
            if(data.floors.Count>=12) throw new ArgumentException("最多支持 12 层。");
            data.floors.Add(new FloorData{floorIndex=data.floors.Count});
        });
        public static void DeleteFloor(BuildingAsset asset,int floor) => Commit(asset,"删除楼层",data=>data.RemoveFloor(floor));
        public static void EditItem(BuildingAsset asset,string id,Action<PlacedItemData> edit) => Commit(asset,"修改关卡物品",data=>
        {
            var item=data.floors.SelectMany(f=>f.items).FirstOrDefault(i=>i.id==id);
            if(item==null) throw new ArgumentException("物品已不存在。");
            edit(item);
        });
        public static void Import(BuildingAsset asset,string path)
        {
            if(new FileInfo(path).Length>4*1024*1024) throw new ArgumentException("地图文件不能超过 4 MB。");
            var imported=BuildingData.FromJson(File.ReadAllText(path));
            Commit(asset,"导入地图 JSON",data=>
            {
                data.name=imported.name; data.floorHeight=imported.floorHeight; data.gridSize=imported.gridSize;
                data.halfWidth=imported.halfWidth; data.halfDepth=imported.halfDepth; data.floors=imported.floors;
            });
        }
        public static void Export(BuildingAsset asset,string path)
        {
            asset.Data.Validate();
            File.WriteAllText(path,asset.Data.ToJson(),new System.Text.UTF8Encoding(false));
        }
    }
}
