# 效果登场、发动费用与延迟处理

## 已接入的实际卡片

- 卡文为“支付时间3，将契约区等级0契约时魔登场”的玩家卡：统一接入 `Player` 区启动技能。契约 Lua 的玩家数组第一个为初始正面，第二个为初始背面；只有当前正面可发动。契约离场和从契约区登场均按规则翻面，登场不重复收取印刷时间。
- HZ01-024-R / HZ01-024-SP：白时钟下支付 X≥1，公开顶牌；合格时魔登场，回合结束放牌底；不合格则放牌底、抽一张。未来视的独立诱发仍保持可选。
- 其他玩家卡的转移攻击、替代破坏等技能不因玩家卡进入内核而自动完成。

## 通用登场

```lua
return {
    { op = "Choose", zone = "Contract", type = "契约时魔", maxTime = 0 },
    { op = "Summon", target = "selected" }
}
```

`Summon` 支持 `selected`、`scry`、命名集合等现有目标接口；接受己方手牌、契约区、弃牌区、牌库中已由 Scry 确定的目标、公开暂存区和除外区中的时魔。逐张选择空圆阵，排除对方玩家／防御圆阵，登场竖置，不另付印刷费用。没有合法位置则不移动卡片。

内核发布真实来源区域的 `Summoned` 和移动事件，原因是 `effectSummon`。不会将其误记为 `paidHand` 或 `overclock`。登场诱发仍由控制玩家选择是否发动。

## 发动费用事务

费用声明支持起动效果的 `effects.activationCosts`、决策卡的 `effects.playCosts`、每个 `effects.triggers` 条目内的 `costs`。三者使用相同的费用组成格式。决策卡印刷时间与 playCosts 相加；作为费用舍弃的手牌不能选择正在使用的决策卡。诱发效果先由玩家选择和排序，到该效果处理时支付，取消或超时只跳过该效果，继续后续队列。

```lua
effects = {
    activationCosts = {
        { kind = "Time", min = 1, max = 4, storeAs = "paidTime" },
        { kind = "Discard", amount = 2 },
        { kind = "Destroy", amount = 1 }
    },
    onActivate = function(ctx)
        return {
            { op = "Scry", amount = 1, reveal = true, after = "afterLook" }
        }
    end,
    afterLook = function(ctx)
        local paid = ctx.vars.effect.paidTime
        -- 根据支付的 X、ctx.scry 等信息返回后续指令。
        return {}
    end
}
```

`Time` 可为固定 amount 或 min/max；Discard 与 Destroy 当前为固定张数／圆阵数，只选己方资源，不能重复选择同一卡。Destroy 破坏整个超频堆叠。最多8个费用组成项，每项数值0–32。

内核先检查最小完整费用，再收集选择，最后要求统一确认。整个费用窗口共用一次响应倒计时；选择不刷新时间。取消、超时、不足或非法选择均不支付费用、不记录使用次数；非法输入不替玩家取消选择。确认后一次性提交费用，再结算效果。费用导致的事件在整个支付提交后统一收集。牺牲发动源不会取消已经支付的效果，但后续 `self` 不会跨规则实例追踪源卡。

旧 `activateCost` 保持兼容，与 `activationCosts` 中的费用相加。`storeAs` 将实际支付值保存到本次效果作用域，后续 Scry 回调可读取；初次 `onActivate` 构建计划时尚未支付，不能在这里读取最终 X。

## 延迟处理

```lua
{ op = "Schedule", target = "scry", timing = "turnEnd", after = {
    { op = "ReturnToDeck", target = "set", set = "scheduled", position = "bottom" }
} }
```

| timing | 执行时点 |
| --- | --- |
| turnEnd | 当前回合结束阶段；回合计时耗尽直接交接时也会处理 |
| nextOwnMain | 登记后下个己方回合的主要阶段开始 |
| nextBattle | 指定目标下一次作为攻击者或被攻击者，双方完成响应之后、未来视之前 |
| nextAttack | 指定目标下一次作为攻击者，同上时点 |
| nextDefend | 指定目标下一次作为被攻击时魔，同上时点；直接攻击玩家不算 |

登记时复制本次效果变量、目标集合和目标规则实例身份。`scheduled` 集合是这条 Schedule 捕获的目标。来源离场不会取消登记；目标离场再回来视为新实例，不会被旧任务追踪。执行后移除；nextBattle 的全部目标实例失效时自动清理。最多128个待处理任务，不支持 Schedule 直接递归登记自身。

延迟步骤是已发动效果的后续处理，不会再次弹出“是否发动诱发效果”。它产生的新诱发仍是可选的。旧脚本如果只希望作为攻击者时处理，请将 nextBattle 改为 nextAttack。

场上超频卡回手／回牌库时，仅最上方卡进入手牌／指定卡顶或卡底，其余普通卡进入弃牌区。附带玩家卡不进入这些区域，回自己的玩家区并放在已有时魔下方。契约离场目前沿用回契约区、翻玩家卡并抽一的独立规则。衍生物前往场外以外的区域时消失。发布移动／离场事件，不发布破坏事件。先完成顶部卡的卡顶／卡底排列，再处理契约离场抽卡。移动到场外区时整叠使用玩家选择的同一个场外位置。

## C# 接口与联机

- `ActivationCost`：费用声明；`MatchActivationPayment`：选择、验证、取消、统一提交。
- `EffectExecutionContext.Summon`、独立 `SummonEffect`、`MatchEffectSummon`：效果登场。
- `EffectExecutionContext.Schedule`、独立 `ScheduleEffect`、`MatchScheduledEffects`：延迟任务。
- `AddPlayerCards`：开局将两张玩家卡加入内核，`PlayerCards` 返回当前牌面状态。
- `ctx.emptyBoardCount`、`ctx.contractZoneCount`：主机和本地菜单均提供的发动条件上下文。
- `ChooseNumber`：目标参数为费用数值，-1取消。`ChooseEffect` 选择费用卡，空 GUID 取消。
- `ChooseEffectZone` 复用现有区域选择；`IsBoardPlacement` 区分圆阵与共享场外区，避免编号碰撞。

网络协议21；双方需要相同程序与 Content。仍由客户端本地生成操作菜单，主机验证实际行动。费用数值界面复用 BattleInteractionUI 的已有按钮和 TMP，不在运行时创建窗口结构。

## 测试入口

测试模式下从主菜单卡组下拉选择：

1. **流程测试-登场回牌底**：HZ01-024 + 49张时间2普通时魔。主要阶段发动，支付2，确认 Scry，选择空圆阵；结束阶段应回牌底。支付1则测试未命中分支。
2. **流程测试-复活与未命中**：HZ01-024 + 49张 PD02-014。使用决策卡破坏己方契约，玩家卡翻到复活面；下个自己时间重构恢复费用后，发动玩家技能，支付3并选择空圆阵。也可用契约启动验证公开决策卡后放牌底、抽一。

这两套是重复卡测试组，不作为标准模式合法卡组。

自动回归包括付款取消／超时／非法输入／对方代操作拒绝、整笔支付与 X 保存、牺牲发动源、契约复活与牌面、真实 Lua 的命中与未命中分支、延迟来源离场／目标重入，以及双方过滤视图。纯 C# 流程测试不等于 FishNet 双实例或 Unity UI 实测；还需在 Multiplayer Play Mode 检查按钮、区域点击、显示和实际网络同步。
