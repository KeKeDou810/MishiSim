# Lua 效果 API 与用例

更新：2026-09-29。以当前项目内核为准。下面除完整卡片示例外，`effects = { ... }` 都放进卡片的 `return { ... }` 中；每段为独立用例，合并时不要重复定义同一个 `effects`、`triggers` 或回调字段。

## 1. 一张可发动效果的卡

```lua
return {
    id = "TEST-001",
    name = "测试时魔",
    faction = "星河联盟",
    type = "通常时魔",
    level = 2,
    power = 2000,
    race = "天使",
    sign = "无",
    artworkPath = "Artwork/TEST-001.png",

    effects = {
        activateZone = "Board",
        activateCost = 1,
        oncePerTurn = true,

        onActivate = function(ctx)
            return {
                { op = "Draw", amount = 1 }
            }
        end
    }
}
```

`onActivate` 为起动效果，`onPlay` 为使用决策卡，`onSummon` 为本卡登场诱发，`onDestroyed` 为本卡被破坏诱发，`onForesight` 为本卡特效标记。多个独立诱发写进 `triggers`。`continuous` 专门计算永续修正。

`oncePerTurn=true` 是本卡实例一回合一次；离场再回来重新计数。`oncePerNamePerTurn=true` 是卡名一回合一次，不因离场重置。同一规则卡的不同罕贵度应维护相同规则身份。

Lua 返回指令，由内核执行。`ctx` 是调用时的快照，直接修改 `ctx` 不会改变游戏。需要读取前一步的结果时使用 `Continue` 或 `Scry.after` 回调。

## 2. 选择与目标速查

| 参数 | 可用值／含义 |
| --- | --- |
| `target` | `self` 来源；`selected` 最近选择的一张；`set` 命名集合；`scry` 最近查看；`all` 条件筛选；`event` 事件卡；`cause` 事件来源；`attacker`／`defender` 战斗事件双方 |
| `side` | `own` 己方、`opponent` 对方、`any` 双方。玩家类操作使用前两者 |
| `zone` | `Board` 圆阵、`OffField` 场外、`Hand` 手牌、`Deck` 牌库、`Discard` 弃牌、`Contract` 契约、`Player` 玩家、`Exile` 除外、`Revealed` 公开处理中、`Removed` 已移除 |
| `type` | `契约时魔`、`通常时魔`、`决策卡`、`玩家卡`、`衍生物`；不要写成“普通时魔”或“策略卡” |
| `types` | 多卡种 OR，如 `{ "通常时魔", "契约时魔" }`；与 `type` 二选一 |
| `name / nameMatch` | 默认 `exact` 精确卡名；`fuzzy` 为忽略大小写的子串匹配，不是相似度纠错 |
| `race` | 精确种族 |
| `minTime / maxTime` | 时间范围，包含边界 |
| `minCount / maxCount` | 选择数量，0–32；默认一张 |
| `storeAs` | 保存选择集合／结果，名称为 1–32 位 ASCII 标识符 |
| `set / except` | 读取命名集合／排除另一个集合 |

“任意圆阵”必须是 `zone="Board"`，不能把弃牌区、场外区等算进去。不同筛选字段之间是 AND。`Choose` 默认只处理可选的顶层卡。

多选请保存集合；`selected` 只代表最后一张。`Move.zone` 是目的区域，跨区域移动建议先选择再移动，不要用它同时表达来源筛选。

## 3. 决策卡：选择时间 2 以下的敌方时魔并破坏

```lua
effects = {
    playTurn = "either",

    onPlay = function(ctx)
        return {
            {
                op = "Choose",
                zone = "Board",
                side = "opponent",
                types = { "通常时魔", "契约时魔", "衍生物" },
                maxTime = 2,
                prompt = "选择时间 2 以下的敌方时魔"
            },
            { op = "Destroy", target = "selected" }
        }
    end
}
```

`playTurn` 只限定允许在哪一方的回合使用：

| 设置 | 自己主要阶段 | 自己回合响应窗口 | 对方回合响应窗口 |
| --- | --- | --- | --- |
| `"own"`（默认） | 可以 | 可以 | 不可以 |
| `"either"` | 可以 | 可以 | 可以 |

响应时仍必须轮到自己操作；自己回合响应使用同样计入本回合决策卡使用次数。`onActivate` 仍限定自己主要阶段。旧字段 `playTiming="ownTurn"/"response"` 暂时兼容，分别对应 own/either；不要与 playTurn 同时填写，非法值会报错。`Destroy` 遵守超频堆叠破坏、契约归位、玩家翻面及抽卡等内核规则。单纯移动到弃牌区不等于破坏。

## 4. 抽卡、弃牌、伤害、回复与费用

下面列出独立指令，按需要取用，放入回调的 `return { ... }` 中：

```lua
return {
    { op = "Draw", amount = 2 },
    { op = "Choose", zone = "Hand", side = "own" },
    { op = "Discard", target = "selected" },
    { op = "Damage", side = "opponent", amount = 1 },
    { op = "Heal", amount = 1, moveCost = false, minDamage = 1 },
    { op = "Cost", amount = 1, maxCost = 11 }
}
```

`Discard` 处理手牌；牌库卡送弃牌区使用 `Move`。`Cost` 正数增加可用费用、负数减少；支付发动费用使用 `activateCost` 或 `Pay`，不要用普通 `Cost` 代替支付校验。`ctx.cost` 的 0 在界面显示为 12。效果伤害和回复默认不改变当前费用；显式 `moveCost=true` 时，效果伤害同步增加费用、回复同步减少费用。战斗伤害和牌库耗尽伤害仍同步增加费用。`minDamage` 指回复下限。抽空牌库的重洗与伤害由内核处理。

## 5. 多选、重构与力量修正

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "Choose",
                zone = "Board",
                side = "own",
                type = "通常时魔",
                minCount = 0,
                maxCount = 2,
                storeAs = "allies",
                prompt = "选择至多两张通常时魔"
            },
            { op = "Ready", target = "set", set = "allies" },
            {
                op = "Modify",
                target = "set",
                set = "allies",
                stat = "power",
                amount = 1000,
                duration = "turn"
            }
        }
    end
}
```

`Modify.stat` 支持 `power`、`time`、`range`。`mode="add"` 默认加减，`mode="set"` 设置基准修正。持续期：`turn` 当前回合、`nextTurn` 到下一回合结束、`battle` 当前战斗、`source` 随来源有效、`permanent` 无回合到期。目标离场会清除其场上修正；`permanent` 不是跨离场保存。`source` 在来源离场后失效，被覆盖或无效时不贡献。

## 6. 多条件候选并集

效果：选择己方时魔，或对方时间 1 以下时魔。

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "QueryCards",
                zone = "Board",
                side = "own",
                types = { "通常时魔", "契约时魔", "衍生物" },
                storeAs = "candidates"
            },
            {
                op = "QueryCards",
                zone = "Board",
                side = "opponent",
                types = { "通常时魔", "契约时魔", "衍生物" },
                maxTime = 1,
                storeAs = "candidates",
                append = true
            },
            { op = "Choose", target = "set", set = "candidates" },
            { op = "Destroy", target = "selected" }
        }
    end
}
```

`QueryCards` 不能直接读取敌方手牌或牌库身份。`enteredThisTurn=true` 筛选本回合进入当前区域的卡；`notRebuiltThisTurn=true` 筛选本回合尚未重构的卡。

## 7. 查看三张，拿一张，其余弃置

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "Scry",
                amount = 3,
                from = "top",
                reveal = false,
                storeAs = "viewed",
                after = {
                    {
                        op = "Choose",
                        target = "set",
                        set = "viewed",
                        storeAs = "picked"
                    },
                    { op = "Move", target = "set", set = "picked", zone = "Hand" },
                    {
                        op = "Move",
                        target = "set",
                        set = "viewed",
                        except = "picked",
                        zone = "Discard"
                    }
                }
            }
        }
    end
}
```

`amount` 为 1–32，可由 Lua 计算。不足则查看剩余数量；Scry 本身不补牌、不洗牌、不移动牌。`reveal=true` 双方公开并使用中央展示；false 只有操作者看见。空牌库仍继续后续处理，回调收到空数组。

## 8. 查看后自行放回卡顶／卡底

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "Scry",
                amount = 3,
                after = {
                    { op = "ReturnToDeck", target = "scry", position = "choose" }
                }
            }
        }
    end
}
```

`position="top"/"bottom"` 固定位置，`choose` 玩家逐张分配。独立回牌库也用这个操作。场上超频回手／回牌库只返回顶层，下面的普通卡进入弃牌区，玩家及契约遵守专门离场规则。`Shuffle` 是独立洗牌指令，不会自动附带在 ReturnToDeck 后。

## 9. 宣言卡名，公开卡底，相同则加入手牌

```lua
effects = {
    onActivate = function(ctx)
        return {
            { op = "DeclareCardName", storeAs = "guess" },
            {
                op = "Scry",
                amount = 1,
                from = "bottom",
                reveal = true,
                after = "checkGuess"
            }
        }
    end,

    checkGuess = function(ctx)
        local card = ctx.scry[1]
        if card and card.name == ctx.vars.effect.guess then
            return {
                { op = "Move", target = "scry", zone = "Hand" }
            }
        end
        return {}
    end
}
```

变量名完全自选，不要求叫 declaredName。未命中时本例保留原卡底。普通 Scry 不会自动弃置公开牌，未来视的规则处理才有默认去向。

## 10. 通用变量与动态力量

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "SetValue",
                scope = "card",
                key = "counter",
                value = (ctx.vars.card.counter or 0) + 1,
                visibility = "public"
            }
        }
    end,

    continuous = function(ctx)
        return {
            {
                id = "counter_power",
                target = "self",
                stat = "power",
                amount = (ctx.vars.card.counter or 0) * 1000
            }
        }
    end
}
```

| scope | 生命周期 |
| --- | --- |
| `effect` | 本次效果及后续选择／回调 |
| `card` | 本卡规则实例，离场重入清空 |
| `player` | 指定玩家的本局状态 |
| `turn` | 当前回合共享，切换回合清空 |
| `match` | 整局共享 |

值支持 integer、number、boolean、string，写入时可显式指定 `valueType`。同一个 key 不可直接变更类型，先 `ClearValue`。`AddValue` 只累加已经存在的数字；首次初始化用 `SetValue`。`turn`、`match` 是共享命名空间，建议为卡片／机制加前缀。

以下是执行时读取变量的写法，避免使用回调开始时的旧快照：

```lua
return {
    { op = "SetValue", scope = "effect", key = "bonus", value = 1000 },
    {
        op = "Modify",
        target = "self",
        stat = "power",
        amount = { var = "bonus", scope = "effect" },
        duration = "turn"
    }
}
```

变量引用只用于支持它的 `amount/value`，不能任意替代所有参数。未定义引用报错。集合与变量不同：`ctx.sets.picked` 存卡片信息，`ctx.vars.effect.bonus` 存值。`RememberCards` 可保存目标或差集，供后续多次使用。

## 11. 永续光环：本回合己方天使力量加 300

```lua
effects = {
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
                side = "own",
                race = "天使",
                stat = "power",
                amount = 300
            }
        }
    end
}
```

每个贡献按来源实例及 `id` 独立维护，来源失效只撤销自己的贡献。不要在刷新时反复发 `Modify` 伪装永续，也不要在 `continuous` 中改变量或随机。条件不成立返回 `{}` 即撤回贡献。持续修正的筛选字段与一般 Choose 不完全相同，常用 `type/race/name` 为精确匹配。

## 12. 自动监听：己方使用决策卡时抽一张

```lua
effects = {
    triggers = {
        {
            id = "decision_draw",
            label = "使用决策卡后抽一张",
            event = "Played",
            activeZone = "Board",
            oncePerTurn = true,
            listen = {
                side = "own",
                type = "决策卡"
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

所有诱发可选，同时发生时由各自控制者排序，回合玩家组先处理，先选先结算。`condition(ctx)` 必须返回 boolean。`listen.subject="self"/"other"/"any"` 可限制事件卡；`type/types`、`race`、`name/nameMatch`、`from/to`、`phase`、`scope/key` 按事件筛选。Hand／Deck 区域监听必须 subject=self。

事件名：`Summoned`、`Destroyed`、`DestructionPending`、`CardMoved`、`LeftField`、`Drawn`、`Discarded`、`Played`、`Activated`、`AttackDeclared`、`BattleEnded`、`DamageTaken`、`Healed`、`CostChanged`、`TurnStarted`、`TurnEnded`、`PhaseStarted`、`PhaseEnded`、`VariableChanged`、`DeckPositioned`、`ForesightRevealed`、`ForesightResolved`。

常用事件数据：`ctx.event.card` 事件卡、`source` 来源、`reason` 原因；移动有 `from/to`，玩家数值事件有 `amount/before/after`，攻击有 `target/targetPlayer/targetNode`。事件不具备的字段不要直接访问；可能隐藏的卡信息先判空。`listen.side` 按事件玩家判断，例如对方发起攻击时为 opponent，不能用 own 表达“我方被攻击”。

## 13. 发动费用与效果中额外支付

```lua
effects = {
    activateCost = 1,
    activationCosts = {
        { kind = "Discard", amount = 1 }
    },

    onActivate = function(ctx)
        return {
            { op = "Draw", amount = 1 },
            {
                op = "Pay",
                amount = 1,
                costs = {
                    { kind = "Exile", zone = "Discard", amount = 1 }
                },
                after = {
                    { op = "Draw", amount = 2 }
                }
            }
        }
    end
}
```

本例先支付发动时间 1 并弃一张，再抽一张；之后可额外支付时间 1 并除外弃牌区一张，成功才再抽两张。取消额外支付不会撤销已经完成的第一次抽卡。

`activationCosts` 起动费用、`playCosts` 使用费用、`triggers[].costs` 诱发费用、`Pay.costs` 处理中费用共用机制，完整确认后原子提交。`Pay.after` 必须为数组，需要 Lua 判断时在其中使用 Continue。

| kind | 用法 |
| --- | --- |
| `Time` | 玩家时间；支持 `min/max` 选择支付量 |
| `Discard` | 弃牌，默认 Hand |
| `Destroy` | 破坏己方场上卡堆作为费用 |
| `Exile` | 除外选中卡 |
| `ReturnToDeck` | 回牌库，`position="top"/"bottom"` |
| `MoveSelf` | `amount=1, destination="Hand"/"Discard"/"Exile"` |
| `RemoveToken` | 消灭选中衍生物 |
| `TokenTime` | 从一个时间足够的衍生物扣固定时间，不扣玩家费用 |

卡片费用可用 `zone` 或 `zones={"Hand","OffField"}`，及 `type/race/name/nameMatch`。`min/max` 是张数；`paySelectedTime=true` 额外支付选中卡时间；`storeAs` 保存支付量，`storeTime=true` 改存选中卡时间之和。

## 14. 掷骰后按结果处理

```lua
effects = {
    onActivate = function(ctx)
        return {
            { op = "RollDice", amount = 6, storeAs = "die", reveal = true },
            { op = "Continue", callback = "afterDice" }
        }
    end,

    afterDice = function(ctx)
        if ctx.vars.effect.die % 2 == 0 then
            return {
                { op = "Draw", amount = 1 }
            }
        end
        return {}
    end
}
```

骰子 2–100 面，由主机生成结果；公开时双方复用中央展示区播放同一个结果。不要用 Lua 自己随机来决定游戏结果。

## 15. 调用弃牌区决策卡的效果

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "Choose",
                zone = "Discard",
                side = "own",
                type = "决策卡",
                storeAs = "decision"
            },
            { op = "InvokeDecision", target = "set", set = "decision" }
        }
    end
}
```

只执行该卡 `onPlay`；不把它当成重新使用，不自动支付印刷时间，不移动、不重复产生 Played 或决策使用次数。需要这些额外操作必须明确组合 Pay／Move 等。

## 16. 场外符文、衍生物与召唤

```lua
effects = {
    onActivate = function(ctx)
        return {
            { op = "Spawn", definitionId = "PD03-T01-C" }
        }
    end
}
```

`Spawn` 生成已定义的衍生物并让玩家选择合法空场外区，已被任一方占用的场外区不可使用。

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "Choose",
                zone = "Hand",
                side = "own",
                type = "通常时魔",
                maxTime = 2
            },
            { op = "Summon", target = "selected", placement = "any" }
        }
    end
}
```

`Summon` 为效果登场，不自动支付手牌召唤费用。`placement` 为 any、player、defense，仍需合法空圆阵。新登场竖置。

`SpawnBoard` 在圆阵生成衍生物：`definitionId` 必填，`amount>0` 覆盖其时间，省略用定义时间。`RemoveToken` 消灭指定衍生物而不产生 Destroyed。龙蛋选择、计算时间、生成飞龙、消灭原龙蛋的完整实际例子见 `Content/Cards/PD03-011-C.lua`。

## 17. 延迟效果

```lua
effects = {
    onActivate = function(ctx)
        return {
            { op = "Choose", zone = "Board", side = "own", storeAs = "unit" },
            {
                op = "Schedule",
                timing = "nextOwnMain",
                after = {
                    { op = "Ready", target = "set", set = "unit" }
                }
            }
        }
    end
}
```

时点：`turnEnd`、`nextOwnMain`、`nextMain`（下个回合，不限玩家）、`nextOpponentMain`、`nextBattle`、`nextAttack`、`nextDefend`。命名集合随计划保存；离场重入的新规则实例不会误继承旧延迟目标。after 使用指令数组，不能嵌套 Schedule。

## 18. 攻击转移、战斗伤害与防破坏

```lua
effects = {
    triggers = {
        {
            id = "intercept",
            event = "AttackDeclared",
            activeZone = "Board",
            listen = { side = "opponent" },
            onTrigger = function(ctx)
                return {
                    { op = "RedirectAttack", target = "self" },
                    {
                        op = "PreventDestruction",
                        target = "self",
                        from = "battle",
                        duration = "battle"
                    }
                }
            end
        }
    }
}
```

`PreventDestruction.from` 必填 battle／effect／any。`ModifyBattleDamage amount=1` 增加当前攻击对玩家伤害，负数减少；应在当前攻击相关处理中使用。

`ReplaceDestruction`：先 Choose 替身，再对要保护的卡执行；替身来自 selected，不能与目标同卡或同圆阵。

```lua
return {
    { op = "Choose", zone = "Board", side = "own", type = "通常时魔" },
    {
        op = "ReplaceDestruction",
        target = "self",
        from = "effect",
        duration = "turn"
    }
}
```

该片段仍由玩家选择合法替身；实际卡文如需限定非自身／不同圆阵，应先构造候选或在后续回调判断。替代成功消耗一次。`DestructionPending` 中可用 `ChangeDestructionReason from="effect"` 改变本次破坏分类；真正 Destroyed 使用最终原因。费用破坏不提供同样的可选替代窗口。

## 19. 无效、禁止选择、卡名封锁、使用费用调整

以下指令各自独立，按卡文选择需要的操作：

```lua
return {
    { op = "SuppressEffects", target = "selected" },
    {
        op = "ProtectSelection",
        target = "self",
        side = "opponent",
        duration = "turn"
    },
    {
        op = "BlockName",
        side = "opponent",
        name = "测试时魔",
        duration = "turn"
    },
    { op = "ModifyDecisionCost", side = "own", amount = -1, duration = "turn" }
}
```

SuppressEffects 当前使场上目标本回合失去效果。ProtectSelection 的 side 是“哪方的效果不能选择该目标”。BlockName 当前支持 turn／nextTurn，不回滚已在处理中的效果。ModifyDecisionCost 修改玩家使用决策卡的费用，最低 0，不改变卡本身的印刷时间。

专用 `ProtectFromEnemyUnitEffects` 用于直到下个对方回合结束不能被对方时魔效果选择，不等同于所有对方效果免疫。

## 20. 玩家随契约移动、手牌放到时魔下方

```lua
effects = {
    onActivate = function(ctx)
        return {
            { op = "AttachPlayers" }
        }
    end
}
```

仅用于对应契约承载己方两张玩家卡。来源失效／离场后玩家归位，玩家圆阵有时魔时放在其下方；契约被破坏仍执行翻面规则。

```lua
effects = {
    onActivate = function(ctx)
        return {
            { op = "ChooseHiddenHand", minCount = 1, maxCount = 1, storeAs = "hidden" },
            { op = "Choose", zone = "Board", side = "own", type = "通常时魔" },
            { op = "AttachUnder", target = "selected", set = "hidden" }
        }
    end
}
```

ChooseHiddenHand 匿名选择对方手牌，不暴露身份；AttachUnder 将指定手牌放到承载时魔下方。异主附着背面、下层无效果，离场恢复原所有者。玩家卡使用 AttachPlayers，不使用 AttachUnder。

## 21. 未来视与特殊标记

卡片顶层 `sign="未来视2"` 表示两次未来视；契约默认拥有一次。当前攻击为单目标，规则上限计算为标记数乘攻击目标数。未来视流程已由内核负责，不要在攻击触发里再自行 Scry 模拟一次完整未来视。

带特效标记的卡可以定义：

```lua
effects = {
    onForesight = function(ctx)
        return {
            { op = "Draw", amount = 1 }
        }
    end
}
```

在顶层配合 `sign="特效标记（特殊标记）"`。默认抽卡、回复、炸裂标记已有内置处理。公开卡暂存 Revealed，处理结束后仍留在该区的牌进入弃牌区。普通 Scry 不自动执行这些标记规则。

复制刚完成的标记：

```lua
effects = {
    triggers = {
        {
            id = "copy_mark",
            event = "ForesightResolved",
            listen = { side = "own" },
            oncePerTurn = true,
            onTrigger = function(ctx)
                return {
                    { op = "CopyForesight" }
                }
            end
        }
    }
}
```

默认复制原计划并重新选择。显式指定新 target 时，只复制原标记实际应用到场上卡片的操作，不重复抽卡等非目标步骤。复制不会递归产生 ForesightResolved。

## 22. 临时授予技能

```lua
effects = {
    onActivate = function(ctx)
        return {
            { op = "Choose", zone = "Board", side = "own", type = "通常时魔" },
            {
                op = "GrantTrigger",
                target = "selected",
                key = "BattleEnded",
                callback = "grantedDraw",
                duration = "turn",
                oncePerTurn = true
            }
        }
    end,

    grantedDraw = function(ctx)
        local attacker = ctx.event.attacker
        if not attacker or attacker.instanceId ~= ctx.instanceId then
            return {}
        end
        return {
            { op = "Draw", amount = 1 }
        }
    end
}
```

授予回调的来源是获得技能的卡，函数仍在原授予脚本中查找。此例在其作为攻击者的战斗结束时抽牌。授予技能有独立次数；发动费用写在 GrantTrigger 的 `costs` 中，取消支付不会消耗次数。只有效果处理中途支付才使用 Pay。

## 23. ctx 常用字段

| 字段 | 用途 |
| --- | --- |
| `owner / activePlayer / turn` | 控制玩家／回合玩家／回合数 |
| `instanceId / zone / node / time / power` | 当前效果来源、圆阵编号及当前数值 |
| `clock / cost / damage / handCount` | 自己时钟（black/white）、费用、伤害指针、手牌数 |
| `opponentClock / opponentCost / opponentDamage / opponentHandCount` | 对方对应值 |
| `emptyBoardCount / contractZoneCount / contractName` | 合法空圆阵数量、契约区数量、契约名 |
| `vars` | effect/card/player/turn/match 带类型值 |
| `sets / scry` | 命名集合／最近 Scry 的可见卡片信息 |
| `publicCards` | 公开卡片快照，可遍历计算数量、时间和等 |
| `event / reason` | 当前事件信息及回调原因 |

集合卡片常用字段：`instanceId,id,name,type,race,sign,owner,time,power,zone,node,zoneEnteredTurn`。id 是印刷版本 ID，判断卡名效果通常使用 name。隐藏附着及匿名手牌不会借这些字段泄露身份。需要读取刚移动后变为公开的卡，执行 Continue 再访问 ctx.sets。

## 24. 写效果时的边界

- 每次回调最多返回 32 条指令，整个处理链有 512 步预算；不要靠自递归 Continue 实现无限循环。
- Choose 没有候选、Scry 没有牌、可选选择被跳过时，后续集合可能为空；访问 `[1]` 前检查。
- `canActivate`／`canPlay` 返回 boolean，用于卡片自身条件；效果返回数组。不要把全部合法性只写在界面。
- 保存卡片引用用集合，保存数值用变量；不要用全局 Lua table 保存跨效果的对局状态。
- `continuous` 重新计算，固定 `Modify` 每执行一次叠加一次，两者不要重复表达同一加成。
- 这份指南描述已支持的接口，不表示全部卡库都已写好效果。实际卡片应在 Effect Test Lab 检查选择取消、区域变化、费用不足和联机可见性。

更多参数细节：[内核扩展](kernel-extensions-api.md)、[变量](typed-variables-api.md)、[事件监听](event-listeners-api.md)、[Scry](scry-api.md)。旧专题页的历史限制如与本页冲突，以当前代码和本页为准。

## 25. 卡库效果补全使用的接口

```lua
-- 横置指定卡；公开选出的卡，供查看后加入手牌等流程使用。
{ op = "Tap", target = "selected" }
{ op = "Reveal", target = "set", set = "picked" }

-- 拉比：本回合自己从牌库公开的卡时间 -1，在后续条件判断前生效。
-- 多个来源叠加；不改变未公开牌库卡的基础定义。
{ op = "ModifyRevealTime", amount = -1 }

-- 幼体变成成体：在支付离场费用前的 onActivate 中捕获原圆阵。
{ op = "Summon", target = "selected", node = ctx.node }

-- 临时授予带费用、每实例每回合一次的能力。
{ op = "GrantTrigger", target = "selected", key = "BattleEnded",
  callback = "grantedReady", oncePerTurn = true,
  costs = { { kind = "Time", amount = 2 } } }
```

`activationCosts`、`playCosts`、诱发 `costs` 的卡片费用可用 `subject = "self"`／`"other"` 限定来源自身或其他卡，默认 `"any"`。例如自身回卡底的费用不能用另一张同名卡代付。

`continuous` 支持 `stat = "damage"`，表示该时魔对玩家的战斗伤害加成。其条件在力量、时间、攻击距离修正完成后计算；力量阈值会包含其他卡片提供的加成。

```lua
continuous = function(ctx)
    if ctx.owner ~= ctx.activePlayer or ctx.power < 10000 then
        return {}
    end
    return { { id = "high_power_damage", stat = "damage", amount = 1 } }
end
```

`CardMoved` 的 `ctx.event.position == "bottom"` 可识别从卡底移动出来的卡；普通抽卡不提供此标记。`Discarded` 的 `reason == "payment"` 表示支付费用弃牌。集合卡片的 `lastRebuiltTurn` 可与 `ctx.turn` 比较，判断本回合是否重构。`lastEffectRebuiltTurn` 只记录效果造成的重构，不包含回合重构阶段；判断“本回合被效果重构过”时使用 `card.lastEffectRebuiltTurn == ctx.turn`。两项记录都会在离场后清除。

