# 持续修正与自动效果排序

通用监听与卡片筛选已扩展，见 [自动事件监听 API](event-listeners-api.md)。以下没有 listen 的 Summoned/Destroyed 写法继续保持本卡语义。

## 根据 counter 持续计算力量

将下面的 `effects` 合入卡片 Lua。计数器使用已有的带类型存储，`continuous` 只返回当前应当存在的加成。

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "SetValue",
                scope = "card",
                key = "counter",
                value = (ctx.vars.card.counter or 0) + 1
            }
        }
    end,

    continuous = function(ctx)
        local counter = ctx.vars.card.counter or 0

        return {
            {
                id = "counter_power",
                stat = "power",
                target = "self",
                amount = counter * 1000
            }
        }
    end
}
```

内核在状态刷新时重新求值；计数器改变后不需要先减去旧加成再加新加成。每个贡献归属于具体来源实例及 `id`。重复刷新不会重复累加；同一张定义的两份场上实例分别贡献自己的加成。最终数值是基础值、固定修正与全部有效持续贡献的总和，最后统一限制为不小于 0。

来源离开生效区域或被超频覆盖后，它的贡献停止，其他来源的贡献保留。来源再次登场时按新实例重新计算，旧的 card 作用域计数器已经清空。条件不再满足时返回 `{}`，即可撤掉该回调之前返回的贡献。

`continuous` 是纯计算回调：不要修改 Lua 全局变量、计数或产生随机结果；不要返回 `Draw` 等执行指令。它会被重复调用，不能用调用次数表达游戏规则。`ctx.vars` 是只读语义的副本；修改副本不会写回内核。此处没有正在结算的 effect 作用域变量，应读取 card/player/turn/match。

### 持续修正字段

| 字段 | 默认值与用途 |
| --- | --- |
| `id` | 必填，1–32 位 ASCII 标识符；同一回调返回的 ID 不得重复 |
| `stat` | `"power"` 或 `"time"`，默认 power |
| `amount` | 整数 -100000 至 100000；每次回调可以重新计算 |
| `target` | `"self"` 或 `"all"`，默认 self |
| `sourceZone` | 来源生效区域，`"Board"` / `"OffField"`，默认 Board |
| `zone` | 受影响目标区域，`"Board"` / `"OffField"`，默认 Board |
| `side` | `"own"` / `"opponent"` / `"any"`，默认 own |
| `type` / `race` / `name` | 可选的目标精确匹配条件 |

例如，仅在自己回合生效的场外天使加成：

```lua
continuous = function(ctx)
    if ctx.owner ~= ctx.activePlayer then
        return {}
    end

    return {
        {
            id = "angel_aura",
            sourceZone = "OffField",
            target = "all",
            zone = "Board",
            race = "天使",
            amount = 300
        }
    }
end
```

每个来源最多返回 32 个贡献。旧 `effects.aura` 继续生效；同一加成迁移到 `continuous` 后应删除旧 aura，否则两者都会计入。

## 固定修正的来源生命周期

当加成数值只需在效果结算时计算一次，但要随来源离场失效，可使用：

```lua
{
    op = "Modify",
    target = "selected",
    stat = "power",
    amount = 1000,
    duration = "source"
}
```

来源必须处于 Board 或 OffField。该修正绑定来源的本次场上实例；来源离场会删除它，返回场上不会恢复旧修正。被覆盖期间修正不生效。它不会持续读取 counter；需要动态数值时使用 `continuous`。

`duration = "turn"` / `"nextTurn"` / `"permanent"` 保持原有生命周期，来源离场不会提前移除这些修正；受修正的目标自身离场仍会清空修正。重复执行 `Modify` 仍然是多次加成。

## 同时诱发的自动效果

同一原子操作（例如一条 Destroy 指令破坏多个圆阵，或破坏整个超频堆叠）产生的自动效果组成一批。所有该操作的状态变更完成后才检查诱发条件。当前批次流程：

1. 回合玩家先为自己控制的效果排序。
2. 对方为自己控制的效果排序。
3. 全部排完后按选择顺序结算：先选先处理，回合玩家的组在前。
4. 本批结算中再次诱发的效果进入后续批次。

目前采用顺序结算，不是后入先出的逆序连锁。顺序选择本身不提供插入决策卡的响应窗口；现有战斗响应窗口仍按原规则运行。

所有诱发效果均可选，单个效果也必须由控制玩家确认发动。每位玩家只看到自己的项目；点击卡片表示选择发动并追加到结算顺序，点击“放弃剩余效果”结束自己的选择。每位玩家每批共用一段 ResponseTimeSeconds（未配置正数时为 20 秒），点击不会重置；超时放弃尚未选择的效果，已选择的效果保留。回合玩家先选择，再交给对方，双方完成后先选先结算。选择期间暂停回合计时。放弃不会消耗效果的回合次数限制。

旧 `onSummon` / `onDestroyed` 自动参与排序，不必修改现有卡库。多个独立效果可写为：

```lua
effects = {
    triggers = {
        {
            id = "arrival_draw",
            label = "登场时抽一张",
            event = "Summoned",
            onTrigger = function(ctx)
                return {
                    { op = "Draw", amount = 1 }
                }
            end
        },
        {
            id = "arrival_power",
            label = "白时钟：力量增加",
            event = "Summoned",
            condition = function(ctx)
                return ctx.clock == "white"
            end,
            onTrigger = function(ctx)
                return {
                    {
                        op = "Modify",
                        target = "self",
                        amount = 1000,
                        duration = "turn"
                    }
                }
            end
        }
    }
}
```

没有 listen 的 `Summoned`、`Destroyed` 表示本卡事件；显式 listen 可监听其他卡和更多事件，详见自动事件监听 API。`condition` 可省略，必须返回 boolean；`onTrigger` 返回普通内核指令列表。条件与计划在收集诱发时计算，指令中的变量引用仍在执行时读取；排序不会重新执行 Lua 计划回调。

`id` 在该卡 triggers 中唯一；不同罕贵度的同一效果保持相同 ID。卡级 `oncePerTurn` / `oncePerNamePerTurn` 会按事件及触发 ID 分别计数，在实际开始结算时复查并计次。两个同名来源竞争同一卡名次数时，先结算的使用次数，后一个跳过。需要把一个效果的连续步骤放在同一个 onTrigger 中，不应拆成多个 trigger。

每个事件最多 32 个非空效果（包含旧回调）；每批最多 128 个效果；整条处理链继续受 512 次执行上限保护。脚本异常会明确停止测试对局。

## C# 扩展接口

| 接口 / 类型 | 用途 |
| --- | --- |
| `IContinuousEffectProvider.Continuous(EffectContext)` | 返回当前有效的 `ContinuousModifier` 列表；不修改对局 |
| `IAutomaticEffectProvider.BuildAutomatic(EffectContext)` | 返回带稳定 ID、显示名称与指令列表的 `AutomaticEffectPlan` |
| `EffectContext.SourceZone` / Lua `ctx.zone` | 来源所在区域 |
| `EffectChoice.TriggerOptions` | 控制玩家可选的待排序效果；每次诱发有独立 Guid |
| `TestCommandKind.OrderTrigger` | 以触发 Guid 选择下一个效果；由连接身份与当前选择权限校验 |

计算、队列、Lua 适配分别维护于 MatchEffectStats、MatchAutomaticEffects、LuaContinuousEffects、LuaAutomaticEffects。网络发送已计算的力量/时间及必要的排序选项；客户端不重复计算持续修正。协议版本为 20，双方需要同版本程序及同一份 Content。
