现在执行【阶段一：多楼层房屋编辑器（Multi-Floor Level Editor）】。
请编写完整的编辑器代码与 UI 界面，实现以下具体功能：

1. **数据结构定义**：
   - `BuildingData` 包含多个 `FloorData`（按 `floorIndex` 0, 1, 2... 排序，每层高度间隔固定，如 4 米）。
   - `PlacedItemData` 包含：`id`, `type` (`Wall`, `Door`, `Furniture`, `QuestItem`, `StreamerPC`, `Stairs`), `gridX`, `gridZ`, `rotationY` (0/90/180/270), 以及当类型为 `QuestItem` 时的扩展字段 `questId` 和 `questDescription`。

2. **楼层操作 UI 与逻辑**：
   - 提供按钮：【新增楼层】、【删除当前楼层】、【上一层 / 下一层】。
   - 视图过滤：当处于第 N 层编辑时，自动隐藏 `> N` 层的物体，并将 `< N` 层的物体设为暗色或半透明作为对齐参考。

3. **网格摆放与属性配置**：
   - 鼠标在当前楼层网格上悬停显示半透明预览，左键放置、R键旋转90度、右键删除。
   - 当选中或放置 `QuestItem` 时，弹出一个小型输入框，允许我输入该物品的 `questId`（如 "item_diary"）和任务提示文本（如 "拍摄卧室里的怪物日记"）。

4. **模式切换与序列化**：
   - 提供【导出/保存地图 JSON】、【加载默认测试地图（请内置一个包含2层楼、几扇门、1个主播电脑桌和3个任务道具的预设地图）】以及【进入游玩模式 (Play Mode)】按钮。