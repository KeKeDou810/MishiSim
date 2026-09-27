# 联机行动测试

## 启动

打开 `Assets/Scenes/MainMenu.unity` 进入 Play Mode。主编辑器选 Host，Multiplayer Play Mode 虚拟玩家选 Join，地址 `127.0.0.1`、端口 `7770`。局域网使用 Host 的局域网地址。两端必须使用相同代码、Lua 和地图。

虚拟玩家自动读取原项目的 Content。NetworkManager 使用已绑定 SpawnablePrefabs 的 `Assets/Prefabs/Networking/MvpNetworkManager.prefab`。

Deck Build 进入现有编辑器，卡组会保留；目前行动测试仍用固定卡，不读取所选 50 张卡组。构建 Windows 测试客户端可用 `Mishi > Networking > Build Windows Test Player`，Content 会复制到 exe 旁。

## 本轮测试内容

- 每方开局一张 PD01-001 契约时魔、四张 PD01-004 普通时魔手牌；力量读取 Host 的 CardDefinition。
- 自己的手牌显示在画面下方，hover 可预览；对方只收到张数，收不到实例 ID 或定义 ID。
- 双方都从自己的方向看棋盘，卡面按所属玩家朝向。横置显示旋转 90 度。
- 回合阶段依次为 TurnStart（回合开始）、Draw（抽卡）、Rebuild（重构）、TimeReset（时间重构）、Main（主要）、Combat（战斗）、End（结束）。点击 Next Phase 前进；只有 End 阶段可 End Turn。
- Rebuild 阶段自动重置当前玩家场上时魔，不重置另一方。Draw 和 TimeReset 目前仅验证阶段限制，不实际抽卡或操作时钟。
- Main：从手牌拖到任意空圆阵召唤，不要求与契约相邻；排除对方玩家／防御圆阵，允许中央和己方玩家／防御圆阵。召唤不扣费用，登场竖置，同回合可攻击。
- Main：契约可以沿连接移动一次。普通时魔不开放这类移动。
- Combat：拖己方竖置时魔到相邻敌方时魔；也可先点击己方时魔，再点击敌方时魔。攻击宣言后横置，本回合不能再次攻击。
- 普通时魔按力量比较，平手攻击方获胜；契约进攻直接破坏目标。较弱攻击方横置但留场。此次仅验证无卡片效果的时魔对战。
- 被破坏的普通时魔进入弃牌区，契约进入契约区。可侵占时显示 Occupy / Stay，选择后才能继续行动。对方玩家／防御圆阵不能侵占。
- 右侧 Show test controls 提供手牌/场上卡选择、目标圆阵按钮和公开区域预览，可用于验证非法操作会被 Host 拒绝。

## 超频预留接口

网络命令 `TestCommandKind.Overclock` 已预留。把手牌拖到己方已有时魔的圆阵时发送此命令，不走普通召唤覆盖原卡。

Host 的 `NetworkTestMatch.ValidateOverclock` 检查当前行动方、主要阶段、手牌来源与己方场上目标，输出只读 `OverclockRequest`（MatchId、Revision、Player、SourceCardId、TargetCardId、TargetNode）。这是共同前置检查，尚不等于完整超频合法性验证。

当前命令明确返回“超频尚未实现”，不消耗手牌、不改变场面、不增加 Revision。后续在此入口接入时间差、堆叠关系、登场效果与网络结果；不要把 CardPlacementZoneView.Cards 当成权威堆叠数据。

## 当前边界

暂未接入：正式牌库和抽卡、召唤/效果费用、Lua 战斗效果、决策响应窗口、未来视、对玩家攻击、契约离场后的抽卡及玩家卡翻面、超频结算。战斗使用现有 `BattleState.ResolveWithoutCardEffects` 测试入口自动跳过响应，不能用于正式含效果对局。

所有行动仍经过客户端请求 → Host 校验 → 各玩家可见快照 → 本地视图。重复/过期/非当前玩家命令被拒绝。被拒绝的召唤保留手牌，移动/攻击不会绕过阶段规则。

任一玩家掉线结束本局，返回菜单重建房间；无断线续局、Host 迁移、匹配、NAT 打洞或中继。

## 验证

已执行 C# 编译及 59 项纯 C# NUnit 断言测试。覆盖阶段、召唤位置/身份/不重复召唤、移动、攻击、横置/重置、侵占、手牌隐私及超频占位请求不修改状态。

`BattleInteractionViewTests` 为 Unity EditMode 几何测试，包含视角、射线/拖动、场景区域 Collider 检查。此次尚未在 Unity 中执行这些测试，也尚未实际运行本轮双窗口召唤/攻击流程；普通 dotnet 编译不等于运行验证。
