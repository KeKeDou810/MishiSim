# 自动事件监听 API

监听写在外部卡片 Lua 的 `effects.triggers`。内核发布事件、筛选监听者，收集自动效果并接入现有双方排序。客户端不能自行制造事件。

## 登场、使用、种类与卡名筛选

```lua
effects = {
    triggers = {
        {
            id = "ally_angel_arrives",
            label = "其他天使登场：力量增加",
            event = "Summoned",
            activeZone = "Board",
            listen = {
                subject = "other",
                side = "own",
                types = { "通常时魔", "契约时魔" },
                name = "天使",
                nameMatch = "fuzzy"
            },
            onTrigger = function(ctx)
                return {
                    {
                        op = "Modify",
                        target = "event",
                        stat = "power",
                        amount = 1000,
                        duration = "turn"
                    }
                }
            end
        },
        {
            id = "specific_decision_used",
            label = "指定决策卡使用时抽卡",
            event = "Played",
            listen = {
                side = "own",
                type = "决策卡",
                name = "这里填完整卡名",
                nameMatch = "exact"
            },
            onTrigger = function(ctx)
                return {
                    { op = "Draw", amount = 1 }
                }
            end
        }
    }
}
```

`Summoned` 表示登场，`Played` 表示使用决策卡，`Activated` 表示发动启动效果。三者都能按种类细分，不把“发动效果”和“使用卡片”混为一个事件。

`type` 精确匹配 Lua 的 type，如契约时魔、通常时魔、决策卡、玩家卡、衍生物。`types` 表示多个种类任选其一，最多 16 项；不能与 type 同时写。省略时不限种类。筛选声明不会凭空产生游戏动作，例如玩家卡不会因为配置了 Played 监听就获得可使用的动作。

| 卡名选项 | 行为 |
| --- | --- |
| `nameMatch = "exact"` | 默认值，完整卡名精确相等，大小写敏感 |
| `nameMatch = "fuzzy"` | 包含指定文字，忽略大小写；“天使”可匹配“七大天使 乌列尔” |
| 不写 name | 不检查卡名 |

fuzzy 是包含匹配，不是拼写纠错或相似度猜测。匹配的是卡名，不是卡图/罕贵度 ID。

## 监听选项

| 字段 | 用途 |
| --- | --- |
| id / event / label | 稳定效果 ID、事件名、排序窗口显示名称 |
| activeZone | 监听卡自身生效区域，默认 Board；还支持 OffField、Contract、Discard、Exile |
| listen.subject | self 本卡、other 其他卡、any 不限（默认）；玩家/阶段事件使用 any |
| listen.side | 事件主体所属玩家：own / opponent / any（默认） |
| listen.type / types | 卡片种类，单项或多项 |
| listen.race | 种族精确匹配 |
| listen.name / nameMatch | 卡名与匹配方式 |
| listen.from / to | 卡片原区域 / 新区域 |
| listen.phase | 阶段，如 Main、Combat、End |
| listen.scope / key | VariableChanged 的作用域与变量键 |
| condition(ctx) | 可选的额外条件，返回 boolean |
| onTrigger(ctx) | 返回内核指令 |

不同字段是 AND，types 内部是 OR。condition 不能修改状态。

只开放公开区域中的监听者，暂不支持手牌/牌库监听卡，防止触发来源暴露隐藏卡。超频覆盖卡不监听。离开生效区域后停止接收新事件，重新进入按新实例处理。同一原子操作中同时离场的监听者仍能观察这次共同离场/破坏，之后不再观察其他操作。

## 已接入事件

| 事件 | 触发点 |
| --- | --- |
| Summoned | 普通登场、超频、场外衍生物生成；reason 为 paidHand / overclock / spawn |
| Destroyed | 战斗或 Destroy 指令破坏堆叠；reason 为 battle / effect |
| CardMoved | 抽卡、登场、效果移动、回牌库、破坏等跨区域变化 |
| LeftField | Board / OffField 离开这两种区域；圆阵之间移动不触发 |
| Drawn | 每张实际抽入手牌的卡 |
| Discarded | Discard 指令将手牌送入弃牌区；使用决策卡和普通 Move 不算弃牌 |
| Played | 成功支付并使用决策卡，包括敌方回合响应 |
| Activated | 成功支付并发动启动效果；自动效果本身不递归发布 Activated |
| AttackDeclared | 合法攻击宣言并横置后，在响应/伤害结算前处理诱发 |
| DamageTaken | 实际伤害；reason 为 battle / effect / deckEmpty |
| Healed | 实际回复量 |
| CostChanged | 支付、效果、伤害、回复、时间重构造成的实际费用变化 |
| TurnStarted | 新回合；也包含发完初始手牌后的首回合 |
| TurnEnded | 主动或超时结束回合，在切换玩家之前 |
| PhaseStarted | 进入实际阶段，在该阶段抽卡/重构等固有动作之前 |
| PhaseEnded | 离开当前阶段之前 |
| VariableChanged | SetValue / AddValue / ClearValue 对 card/player/turn/match 造成值变化 |

开局发手牌属于准备过程，不发 Drawn；初始摆好的契约不另算登场。洗牌和空牌库回收内部重排不逐张发 CardMoved。跳过的阶段不发开始/结束。effect 临时变量、作用域自动清理及相同值写入不发 VariableChanged。对局结束后不继续处理效果。

## ctx.event 与目标

事件数据是不可变 C# 快照转成的 Lua 副本。修改副本不会修改对局。

- 共通：id、player（0/1）、reason、source（有已知来源时）。
- 卡片事件：card、from、to。
- card/source 快照：instanceId、id（版本 ID）、name、type、race、owner、power、time、node。
- 玩家数值事件：amount、before、after。伤害/回复使用伤害指针，费用使用实际剩余时间，0 对应显示 12。
- 阶段事件：turn、phase。
- 攻击宣言：card 是攻击者，target 是被攻击卡（攻击玩家时 nil），另有 targetPlayer、targetNode。
- 变量事件：scope、key、before、after、card（card 作用域）。新增前值/删除后值为 nil。

`target = "event"` 指事件中的卡片主体，`target = "self"` 指监听卡。事件主体不可见、已移除或已经变成新实例时，event 不命中新实例。排队效果不因来源离场整体取消，但 self 也不会误命中重新登场的新实例；抽卡等玩家操作可继续。

对方抽到的卡在 ctx.event.card 中是 nil，也不能通过隐藏卡名、种类、种族筛选。私有 VariableChanged 只投递给所属玩家；公开变量仍遵守隐藏卡身份限制。事件不原样广播，只同步必要选择和结果。

条件和 Lua 计划在收集时计算，Lua 数值是当时快照；指令里的变量引用仍在执行时读取。多个同时诱发需要累加 counter 时，应先初始化变量，再返回 AddValue；多份 `SetValue = 旧值 + 1` 会覆盖彼此。

## 排序与 C# 扩展

同一原子操作的效果成一批，回合玩家先排自己的，对方再排，全部排完先选先结算。新诱发进入后续批次。阶段切换和攻击后续可暂停等待效果。沿用排序超时、每批 128 效果和每条链 512 步，并限制每批 256 事件；循环诱发会明确停止测试对局。

| 模块 | 责任 |
| --- | --- |
| Battle/Events/*Event.cs | 每种事件独立文件，继承卡片/玩家/阶段事件；VariableChanged 有专用结构 |
| BattleEventRegistry | 自动发现带 BattleEventType 特性的事件，校验事件名 |
| EventSubscription | 生效区域、主体、阵营、阶段及变量条件 |
| EventCardFilter | 种类、种族、精确/fuzzy 卡名判断 |
| MatchEventListeners | 发布、冻结监听资格、收集效果 |
| MatchEventActions | 阶段/攻击/费用发布点及继续执行 |
| LuaEventListeners | Lua 声明解析、筛选和事件数据转换 |
| IEventEffectProvider | Subscriptions(definitionId, eventId)、BuildTriggered(context, triggerId) |

增加事件：新建不可变事件类并标记 `[BattleEventType("事件ID", "中文名")]`，在合法动作成功处 Publish。不必扩充 EffectEvent 枚举或 Lua 事件名 switch。新增数据结构若要供 Lua 读取，再扩展 EventToLua 转换；没有发布点的事件类不会自动产生动作。

旧 onSummon/onDestroyed，以及没有 listen 的 Summoned/Destroyed triggers 保持本卡语义。监听其他卡要显式添加 listen。协议更新为 20，双方程序和 Content 必须一致。

未来视的 Scry 展示、标记回调及公开事件见 [未来视接口](foresight-api.md)。

所有诱发监听均为可选，包括旧 onSummon/onDestroyed。单个效果也需要确认；OrderTrigger 携带该效果 ID 表示发动，Guid.Empty 表示放弃当前控制玩家剩余效果。超时放弃未选项，放弃不记录次数。持续效果和规则处理不属于此选择流程。
