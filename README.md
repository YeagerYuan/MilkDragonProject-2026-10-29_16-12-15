# 皮套之下 · Unity 双人恐怖原型

第一人称主播与狗仔对抗原型，支持局域网双人对战及两种角色的 AI 测试。当前联机协议为 **v8**，两端使用相同版本和资源。

## 打开项目

- Unity：**6000.3.21f1**，项目使用 URP 与 Input System。
- 先安装 Git LFS，再克隆仓库。模型、贴图、录音等资源通过 Git LFS 管理。

```powershell
git lfs install
git clone https://github.com/YeagerYuan/MilkDragonProject-2026-10-29_16-12-15.git
cd MilkDragonProject-2026-10-29_16-12-15
# 如果更新内容仍在合并请求中，切换到对应分支：
git switch codex/integrate-documents-20261102
git lfs pull
```

在 Unity Hub 中添加项目根目录，打开 `Assets/UnderAvatar/Scenes/UnderAvatarPrototype.unity`，等待资源导入与脚本编译。

Tripo Bridge 是可选的模型导入工具，不属于运行游戏的依赖。共享 Packages 配置不包含任何开发者机器的绝对路径；需要使用该工具时，在 Package Manager 中从本机磁盘安装其 `package.json`。

## 编辑与试玩

- 房屋编辑器：`Tools > Under Avatar > Level Editor`。
- 任务编辑器：`Tools > Under Avatar > Task Editor`，也可从房屋编辑器进入。
- 旧关卡首次使用任务编辑器时，点击“启用任务编辑并导入现有任务”。
- 示例：在房屋编辑器的“关卡资源”中选择 `Assets/UnderAvatar/Levels/TaskEditorExample.asset`，体验拍摄苹果和把小球搬到特殊桌子上再拍照的任务。
- Play 后选择任一角色进入 AI 教学大厅，准备后开始正式对局。局域网模式由房主选择角色，另一台输入房主 IP 加入；双方在教学大厅再次准备后，由房主开始正式对局。

`WASD` 行走，`Shift` 静步，按住 `Ctrl` 蹲下，`E` 交互或拿起/放下道具。`F` 打开手机，选任务后进入相机；`Q` 切换手机菜单和取景，左键/空格拍照，预览后手动上传。拍照和取消不扣分，仅错误上传扣一次失误次数。

## 更新与详细说明

- [更新记录](CHANGELOG.md)
- [任务编辑器使用说明](Assets/UnderAvatar/任务编辑器使用说明.txt)
- [完整玩法与联机说明](Assets/UnderAvatar/README.txt)
- [关卡、直播与粉丝规则](Assets/UnderAvatar/关卡与直播调整说明.txt)
- [角色模型与联机动画](Assets/UnderAvatar/角色模型与联机动画说明.txt)
- [模型替换](Assets/UnderAvatar/场景模型替换说明.txt)
- [教学大厅](Assets/UnderAvatar/联机大厅说明.txt)
- [建筑材质](Assets/UnderAvatar/建筑材质说明.txt)
- [音效来源及许可](Assets/UnderAvatar/Audio/SOURCES.txt)

## 验证与构建

本次验证通过 95 项不同测试（61 项运行时回归、1 项额外 AI 搬运测试、33 项编辑器测试）。Unity 房主和 Windows exe 客户端在双向各增加 100 毫秒接收延迟后，通过了拿取/放置同步、手动上传、门与躲藏交互、小游戏、抓捕及重开验证，实测往返约 223–227 毫秒。

Windows 构建场景为 `Assets/UnderAvatar/Scenes/UnderAvatarPrototype.unity`，输出目录 `Builds/UnderAvatarLAN`。本次构建成功，无编译错误；构建期间记录了两条 MCP WebSocket 重连警告。

`Builds`、测试日志、本地备份和 Unity 缓存不进入源码提交。生成的本地测试包为 `Builds/UnderAvatarLAN-v8-tasks.zip`，合作开发者可从源码构建。
