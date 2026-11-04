using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // Append types: existing maps serialize the original numeric values 0–5.
    public enum ItemType { Wall, Door, Furniture, QuestItem, StreamerPC, Stairs, Bed, Custom }

    [Serializable]
    public sealed class PlacedItemData
    {
        public string id = Guid.NewGuid().ToString("N");
        public ItemType type;
        public int gridX;
        public int gridZ;
        public int rotationY;
        public string questId = "";
        public string questDescription = "";
        public bool hideable;
        public string definitionId="";
        public string displayName="";
        public Vector3 customSize=new Vector3(1.5f,1.5f,1.5f);
        public bool customSizeOverride;
        public bool IsHidingPlace => type==ItemType.Bed || type==ItemType.Furniture && hideable;
    }

    [Serializable]
    public sealed class FreePlacedObjectData
    {
        public string id=Guid.NewGuid().ToString("N");
        public string name="装饰物",definitionId="";
        // Bottom center in building coordinates; does not occupy a grid cell.
        public Vector3 position,rotation,size=Vector3.one;
        public bool solid=true;
    }

    [Serializable]
    public sealed class FloorData
    {
        public int floorIndex;
        public List<PlacedItemData> items = new List<PlacedItemData>();
    }

    [Serializable]
    public sealed class BuildingData
    {
        public int version = 1;
        public bool useTaskEditor;
        public bool requireLiveEvidence=true;
        public List<InvestigationTaskData> tasks=new List<InvestigationTaskData>();
        public List<TaskPropData> taskProps=new List<TaskPropData>();
        public List<TaskClueData> taskClues=new List<TaskClueData>();
        public List<FreePlacedObjectData> decorations=new List<FreePlacedObjectData>();
        public string name = "主播的宅邸";
        public float floorHeight = 4f;
        public float gridSize = 2f;
        public int halfWidth = 8;
        public int halfDepth = 6;
        public List<FloorData> floors = new List<FloorData>();

        public int WidthCells => halfWidth*2+1;
        public int DepthCells => halfDepth*2+1;
        public float WallHeight => floorHeight;
        public bool ContainsCell(int x, int z) => Mathf.Abs(x) <= halfWidth && Mathf.Abs(z) <= halfDepth;
        public Vector3 Position(int floor, PlacedItemData item) =>
            new Vector3(item.gridX * gridSize, floor * floorHeight, item.gridZ * gridSize);
        public PlacedItemData At(int floor, int x, int z) =>
            floors[floor].items.Find(i => i.gridX == x && i.gridZ == z);
        public string ToJson() => JsonUtility.ToJson(this, true);
        public BuildingData Copy() => FromJson(ToJson());

        public static BuildingData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                throw new ArgumentException("地图文件必须是 JSON 对象。");
            BuildingData data;
            try { data = JsonUtility.FromJson<BuildingData>(json); }
            catch (Exception e) { throw new ArgumentException("JSON 无法解析：" + e.Message, e); }
            data.Validate();
            return data;
        }

        public void Validate()
        {
            if (version != 1) throw new ArgumentException("不支持此地图版本。");
            if (float.IsNaN(floorHeight) || float.IsInfinity(floorHeight) || floorHeight < 3.5f || floorHeight > 12f ||
                float.IsNaN(gridSize) || float.IsInfinity(gridSize) || gridSize < 2f || gridSize > 5f)
                throw new ArgumentException("层高须为 3.5–12 米，网格须为 2–5 米。");
            if (halfWidth < 3 || halfDepth < 3 || halfWidth > 30 || halfDepth > 30)
                throw new ArgumentException("地图半宽/半深须为 3–30 格。");
            if (floors == null || floors.Count < 1 || floors.Count > 12)
                throw new ArgumentException("地图须包含 1–12 层楼。");
            if (floors.Any(f => f == null)) throw new ArgumentException("楼层数据不能为空。");
            floors.Sort((a, b) => a.floorIndex.CompareTo(b.floorIndex));
            var ids = new HashSet<string>();
            var quests = new HashSet<string>();
            for (int f = 0; f < floors.Count; f++)
            {
                var floor = floors[f];
                if (floor.floorIndex != f || floor.items == null || floor.items.Count > 4000)
                    throw new ArgumentException("楼层必须从 0 连续编号，且每层物品不超过 4000 个。");
                var cells = new HashSet<Vector2Int>();
                foreach (var item in floor.items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id))
                        throw new ArgumentException("物品 id 不能为空或重复。");
                    if (!Enum.IsDefined(typeof(ItemType), item.type) || !ContainsCell(item.gridX, item.gridZ) ||
                        item.rotationY < 0 || item.rotationY > 270 || item.rotationY % 90 != 0)
                        throw new ArgumentException("物品类型、网格坐标或旋转角度无效。");
                    if (!cells.Add(new Vector2Int(item.gridX, item.gridZ)))
                        throw new ArgumentException("同一网格不能重复放置物品。");
                    if(item.type==ItemType.Custom && (!ValidSize(item.customSize.x) || !ValidSize(item.customSize.y) || !ValidSize(item.customSize.z) ||
                        item.customSize.y>floorHeight))
                        throw new ArgumentException("自定义物体尺寸必须至少为 0.1 米，且高度不能超出层高。");
                    if (item.type == ItemType.QuestItem &&
                        (string.IsNullOrWhiteSpace(item.questId) || string.IsNullOrWhiteSpace(item.questDescription) || !quests.Add(item.questId)))
                        throw new ArgumentException("任务 ID 必须唯一，且任务说明不能为空。");
                }
            }
            if(decorations==null) decorations=new List<FreePlacedObjectData>();
            if(decorations.Count>2000) throw new ArgumentException("自由摆放物体最多 2000 个。");
            foreach(var item in decorations)
            {
                if(item==null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id) || string.IsNullOrWhiteSpace(item.definitionId) ||
                    !InvestigationData.Finite(item.position.x) || !InvestigationData.Finite(item.position.y) || !InvestigationData.Finite(item.position.z) ||
                    Mathf.Abs(item.position.x)>(halfWidth+.5f)*gridSize || Mathf.Abs(item.position.z)>(halfDepth+.5f)*gridSize || item.position.y<0 || item.position.y>=floors.Count*floorHeight ||
                    !InvestigationData.Finite(item.rotation.x) || !InvestigationData.Finite(item.rotation.y) || !InvestigationData.Finite(item.rotation.z) ||
                    !ValidSize(item.size.x) || !ValidSize(item.size.y) || !ValidSize(item.size.z) || item.size.x>100 || item.size.y>100 || item.size.z>100)
                    throw new ArgumentException("自由摆放物体的 ID、模型、位置、旋转或尺寸无效；底部位置须在建筑范围内，尺寸为 0.1–100 米。");
            }
            if(taskProps!=null && taskProps.Any(p=>p!=null && ids.Contains(p.id))) throw new ArgumentException("任务道具与建筑物体的 ID 不能重复。");
            InvestigationData.Validate(this);
        }

        static bool ValidSize(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value>=.1f;

        public void RemoveFloor(int index)
        {
            if (floors.Count == 1) throw new InvalidOperationException("至少保留一层楼。");
            if (index < 0 || index >= floors.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Func<Vector3,bool> onFloor=p=>Mathf.FloorToInt(Mathf.Max(0,p.y)/floorHeight)==index;
            if(decorations.Any(p=>onFloor(p.position)) || taskProps.Any(p=>onFloor(p.position)) || taskClues.Any(c=>onFloor(c.position)) || tasks.Any(t=>t.kind==InvestigationKind.PlaceAndPhotograph && onFloor(t.regionPosition)))
                throw new InvalidOperationException("该楼层有自由摆放物体、任务道具、线索或区域，请先移走或删除它们。");
            Func<Vector3,Vector3> shift=p=>p.y>=(index+1)*floorHeight?p-Vector3.up*floorHeight:p;
            foreach(var p in taskProps) p.position=shift(p.position);
            foreach(var c in taskClues) c.position=shift(c.position);
            foreach(var task in tasks) task.regionPosition=shift(task.regionPosition);
            foreach(var item in decorations) item.position=shift(item.position);
            floors.RemoveAt(index);
            for (int i = 0; i < floors.Count; i++) floors[i].floorIndex = i;
        }

        public static BuildingData CreateDefault()
        {
            var data = new BuildingData();
            for (int f = 0; f < 2; f++)
            {
                var floor = new FloorData { floorIndex = f };
                data.floors.Add(floor);
                for (int x = -7; x <= 7; x++)
                {
                    Add(floor, ItemType.Wall, x, -5);
                    Add(floor, ItemType.Wall, x, 5);
                }
                for (int z = -4; z <= 4; z++)
                {
                    Add(floor, ItemType.Wall, -7, z, 90);
                    Add(floor, ItemType.Wall, 7, z, 90);
                    Add(floor, z == -2 || z == 2 ? ItemType.Door : ItemType.Wall, 0, z, 90);
                }

                Add(floor, ItemType.Furniture, -3, 3).hideable = true;
                Add(floor, ItemType.Furniture, 2, 3).hideable = true;
                Add(floor, ItemType.Furniture, -3, 0);
                Add(floor, ItemType.Furniture, 4, 0, 90);
            }
            Add(data.floors[0], ItemType.Stairs, -5, -1);
            Add(data.floors[0], ItemType.Stairs, 5, -3);
            Add(data.floors[1], ItemType.StreamerPC, 4, 4);
            Quest(data.floors[0], -5, -3, "item_medicine", "拍摄一楼厨房的可疑药剂");
            Quest(data.floors[1], -4, -3, "item_diary", "拍摄二楼卧室的怪物日记");
            Quest(data.floors[1], 5, 1, "item_contract", "拍摄二楼书房的秘密合同");
            data.Validate();
            return data;
        }

        static PlacedItemData Add(FloorData floor, ItemType type, int x, int z, int rotation = 0)
        {
            var item = new PlacedItemData { type = type, gridX = x, gridZ = z, rotationY = rotation };
            floor.items.Add(item);
            return item;
        }

        static void Quest(FloorData floor, int x, int z, string id, string description)
        {
            var item = Add(floor, ItemType.QuestItem, x, z);
            item.questId = id;
            item.questDescription = description;
        }
    }
}
