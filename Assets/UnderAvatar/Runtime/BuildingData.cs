using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    public enum ItemType { Wall, Door, Furniture, QuestItem, StreamerPC, Stairs }

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
                    if (item.type == ItemType.QuestItem &&
                        (string.IsNullOrWhiteSpace(item.questId) || string.IsNullOrWhiteSpace(item.questDescription) || !quests.Add(item.questId)))
                        throw new ArgumentException("任务 ID 必须唯一，且任务说明不能为空。");
                }
            }
        }

        public void RemoveFloor(int index)
        {
            if (floors.Count == 1) throw new InvalidOperationException("至少保留一层楼。");
            if (index < 0 || index >= floors.Count) throw new ArgumentOutOfRangeException(nameof(index));
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
