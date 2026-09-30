# 迷时模拟器（Mishi / MishiSim）

基于 Unity、C# 和 Lua 开发的迷时 TCG 对战模拟器。C# 内核负责规则执行、状态管理和联机同步，外部 Lua 定义卡片数据与效果，卡图、卡组及游戏模式配置也从外部文件加载。

项目仍在开发中。已有卡片效果脚本不代表所有卡片、组合及边界情况都已完成验证。

## 项目概览

- **卡组编辑**：契约选择、国家限制、卡片搜索与筛选、排序、异画共享规则，以及卡组保存和读取。
- **对战内核**：回合阶段、费用与伤害指针、召唤、超频、攻击、未来视、响应窗口及可选诱发效果。
- **Lua 效果**：目标选择、卡片移动、修正器、事件监听、带作用域的变量存储和后续操作。
- **联机房间**：FishNet 直连及 Steam 大厅、房间密码、准备与卡组选择、聊天、观战和断线重连。
- **录像回放**：记录公开场面与有效操作，支持保存、读取、逐帧和倍速回放；观众仅记录自己收到的公开信息。
- **开发工具**：单卡与组合效果测试场景、内核效果编辑工具及 EditMode 测试。

当前联机由房主承载对局，尚不应视为独立 Linux 房间服务器的完整实现。

## 开发环境

- Unity **6000.5.10f1**，以 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) 为准。
- URP、TextMeshPro / UGUI、Input System。
- FishNet、FishySteamworks、Steamworks.NET。
- MoonSharp（Lua 解释器）、PrimeTween。
- 工程还包含 Odin Inspector、Feel / Nice Vibrations 等第三方插件文件；它们不受本项目 MIT 许可证覆盖。

包配置见 `Packages/manifest.json`。当前仓库的忽略规则会排除部分包文件和 DLL，因此全新克隆不保证依赖齐全。打开工程前请核对本地包配置、PrimeTween 包和 MoonSharp 等插件二进制文件，并通过各依赖的合法渠道补齐。MoonSharp 的版本及文件说明见 [插件说明](Assets/Plugins/MoonSharp/README.md)。

## 开始使用

1. 使用对应版本的 Unity，通过 Unity Hub 打开仓库根目录。
2. 等待资源导入并补齐缺失依赖，确认 Console 无编译错误。
3. 打开 `Assets/Scenes/MainMenu.unity`，进入 Play Mode。
4. 在卡组编辑界面创建或加载卡组；进入房间后选择本次使用的卡组并准备。
5. 本地联机可使用直连入口；Steam 大厅需要已启动并登录的 Steam 客户端。

Steam 联机目前使用 Spacewar 测试 App ID `480`。大厅按项目标识、软件版本及协议筛选，连接时还会校验内容兼容性。测试 App ID 的使用不代表本项目获得 Steam 或卡牌官方的认可。

Windows 测试构建入口：`Mishi > Networking > Build Windows Test Player`。构建脚本会将外部 `Content` 复制到可执行文件旁，并为开发构建写入测试用 `steam_appid.txt`。构建前检查 `Content` 内是否含有不打算分发的私人卡组或录像。

## 目录结构

| 路径 | 用途 |
| --- | --- |
| `Assets/Scripts/Battle` | 对战规则内核、效果执行与状态 |
| `Assets/Scripts/Card` | 卡片数据库、卡组与预览 |
| `Assets/Scripts/Networking` | 房间、同步、战斗交互、录像及回放 |
| `Assets/Scripts/Util` | Lua 加载及桥接等工具 |
| `Assets/Prefabs/UI` | UI 预制体 |
| `Assets/Scenes` | 主界面、卡组编辑、对战及效果测试场景 |
| `Assets/Editor/KernelEffects` | 内核效果编辑工具 |
| `Assets/Tests/EditMode` | 自动化测试 |
| `Content/Cards` | 外部 Lua 卡片定义和效果 |
| `Content/Artwork` | 外部卡图、卡背等素材 |
| `Content/Decks` | 卡组存档 |
| `Content/Rules` | 模式、禁限规则与 Lua API 文档 |
| `Content/Tests` | 效果测试配置 |
| `Content/Recordings` | 对局录像 |

编辑器读取工程根目录的 `Content`；Windows / Linux Player 读取可执行文件旁的 `Content`。因此可以修改外部卡片数据和图片，而不必将其打包进 Unity 资源。修改规则或效果后，联机各端应使用兼容的内容版本。

## 编写与测试卡片效果

效果接口和用例见 [Lua 效果 API](Content/Rules/lua-effect-api.md)。异画卡可以使用不同 `id` 与 `artworkPath`，并通过共同的 `rulesId` 表示同一规则身份。

- 编辑外部 Lua 后，使用数据库重新加载功能更新数据。
- 通过 `Tools > Mishi > Effect Test Lab` 配置单卡或组合测试。
- 通过 `Mishi > Kernel Effects > Effect Editor` 维护内核效果。
- 在 Unity Test Runner 的 EditMode 页运行回归测试。

新增效果时应同时检查发动条件、可选分支、目标合法性、持续时间与离场清理，并验证联机中的隐藏信息不会泄露。

## 许可证与素材说明

本项目原创程序代码采用 [MIT License](LICENSE)，包括原创 C# 代码与 Lua 效果实现。第三方代码、插件、字体和其他资源保留各自的许可证，不因存放在本仓库而改为 MIT。

**卡图等卡牌素材为官方素材，本项目未取得其使用或再分发授权。** 官方卡图、卡面设计、标识、角色及卡片原始文本等内容不包含在 MIT 授权范围内，其权利归各自权利人所有。代码的开源许可不授予任何官方素材的使用或再分发权利，也不表示本项目获得官方授权或背书。

若需分发不包含官方素材的版本，请自行移除这些内容并使用已获授权或自制的替代资源；第三方插件同样需要遵守各自的分发条件。
