# Role & Task
你是一个资深 Unity URP 渲染工程师。我正在开发一款中国 80-90 年代背景的写实室内恐怖游戏（Unity URP 管线）。请帮我编写一套完整的 C# 脚本（包含 Editor 自动化配置工具和 Runtime 脚本），用于将当前的白盒室内场景一键转化为阴暗、潮湿、手电筒高对比度的写实恐怖渲染环境。

# Deliverables Required
请生成以下 3 个完整的 C# 脚本，并确保代码可直接编译运行（使用 UnityEngine.Rendering 和 UnityEngine.Rendering.Universal 命名空间）：

## 1. `Editor/HorrorSceneSetupWindow.cs`（一键环境与后处理配置工具）
创建一个菜单项 `Tools/Horror Setup/1. Initialize Atmosphere & Post-Processing`，点击后自动执行：
- **环境光与天空盒重置**：
  - 将 `RenderSettings.skybox` 设为 `null`，消除默认的蓝天白云环境光。
  - 将 `RenderSettings.ambientMode` 设为 `AmbientMode.Flat`。
  - 将 `RenderSettings.ambientLight` 设为极暗的冷墨绿色 `new Color(0.015f, 0.022f, 0.02f, 1f)`。
  - 查找场景中的 `Directional Light` 并将其禁用或强度设为 `0`。
- **指数雾效（Fog）配置**：
  - 开启 `RenderSettings.fog = true`。
  - 模式设为 `FogMode.ExponentialSquared`。
  - 颜色设为 `new Color(0.01f, 0.015f, 0.015f, 1f)`，浓度 `fogDensity = 0.055f`，让远处走廊与楼梯自然融入黑暗。
- **全局后处理（Global Volume）自动生成**：
  - 在场景中创建名为 `Global Volume_Horror` 的 GameObject，挂载 `Volume` 组件（`isGlobal = true`）。
  - 在 `Assets/Settings/` 下自动创建并持久化保存一个 `VolumeProfile` 资源，自动添加并覆写以下组件参数：
    1. `Tonemapping`：Mode = `TonemappingMode.ACES`。
    2. `ColorAdjustments`：Post Exposure = `-0.3f`，Contrast = `22f`，Saturation = `-28f`（荒废褪色感）。
    3. `LiftGammaGain`：微调 Lift（暗部）向青绿色偏移 `new Vector4(0.95f, 1.0f, 0.98f, -0.02f)`。
    4. `Vignette`：Intensity = `0.42f`，Smoothness = `0.35f`（模拟幽闭视野）。
    5. `FilmGrain`：Type = `FilmGrainLookup.Medium1`，Intensity = `0.25f`，Response = `0.8f`（90年代粗糙胶片感并消除暗部色阶断层）。
    6. `Bloom`：Threshold = `1.1f`，Intensity = `0.4f`，Scatter = `0.65f`。

## 2. `Runtime/HorrorFlashlightController.cs`（双层拟真手电筒脚本）
创建一个挂载在玩家摄像机下的手电筒控制脚本，在 `Reset()` 或初始化时自动构建以下层级与逻辑：
- **双光源结构（还原真实手电筒内外光圈）**：
  - `MainBeam`（主聚光灯）：Spot Light，Range = `18f`，Spot Angle = `48f`，Inner Spot Angle = `25f`，Intensity = `12f`，Color = 偏暖白微黄 `new Color(1f, 0.95f, 0.85f)`，开启软阴影 `LightShadows.Soft`，`shadowNearPlane = 0.05f`，预留 `cookie` 贴图槽位。
  - `BounceHalo`（外围微弱散射光）：Spot Light，Range = `8f`，Spot Angle = `95f`，Intensity = `0.8f`，无阴影，模拟手电筒照亮墙壁时的近处微弱漫反射，避免光斑外完全死黑。
- **程序化呼吸与延迟晃动（Procedural Sway）**：
  - 使用 `Quaternion.Slerp` 让手电筒跟随摄像机旋转时产生微小的滞后阻尼感（参数可调 `swaySpeed = 8f`）。
  - 加入基于 `Mathf.PerlinNoise` 的微弱呼吸晃动与低电量随机闪烁接口 `TriggerFlicker()`。

## 3. `Editor/HorrorPBRMaterialAutoLinker.cs`（PBR 材质球批量组装工具）
创建一个菜单项 `Tools/Horror Setup/2. Auto-Build PBR Materials from Folder`：
- 根据选中文件夹内的贴图命名后缀（`_BaseMap`/`_Albedo`, `_Normal`, `_Roughness`/`_Smoothness`/`_Metallic`, `_AO`），自动创建 `Universal Render Pipeline/Lit` 材质球。
- 自动将法线贴图的 TextureImporter 设置为 `TextureImporterType.NormalMap`，并将法线强度 `_BumpScale` 默认设为 `1.2f`（强化破损木纹与墙皮凹凸），Smoothness 默认控制在 `0.2f - 0.35f` 之间以消除塑料反光感。