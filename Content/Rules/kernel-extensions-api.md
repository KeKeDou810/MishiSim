# 内核扩展 API（2026-09-29）

本页对应本次卡库审计后的通用能力补充。每个操作仍是 `Assets/Scripts/Battle/Effects/Handlers/` 下的独立 `KernelEffect`；事件类分别位于 `Events/`，状态处理按功能拆分为 `Match*.cs`。Editor 的 Kernel Effects 工具可以发现这些操作。

**脚本入口覆盖不等于全部交互已经验收。** 当前 153 个 rulesId 中，137 组有卡文的卡已有 Lua 效果入口，另有 16 组无效果文本。脚本加载、回调分支、版本一致性和部分真实组合已检查；详情见 [覆盖清单](card-effect-coverage.json)。

## 快速测试

打开 `Tools > Mishi > Effect Test Lab`，进入 `EffectTestLab` 场景。

- `07-dice-invocation.json`：雪乘使用决策卡诱发掷骰；中野零子在支付时间从手牌登场（包括超频）后，选弃牌区决策卡，掷骰后支付或回牌库洗切。
- `08-token-transformation.json`：紫晶选择龙蛋、选择飞龙登场圆阵，检查时间继承、力量与原龙蛋消灭。

这些预设是局部效果测试，允许跨国家组合，不代表正式卡组是否合法。测试窗口支持切换操作者、重置及重新加载 Lua。已有中央展示区负责显示骰子，不生成新的 UI 面板。

## 执行与查询

| 操作 | 主要参数 | 作用 |
| --- | --- | --- |
| `RollDice` | `amount=6, storeAs="die", reveal=true` | 主机掷 2–100 面骰，将整数保存在本次效果变量中。公开时双方收到同一结果与动画；不公开时只有操作者收到。 |
| `Continue` | `callback="afterDice"` | 用最新状态调用当前脚本 `effects.afterDice(ctx)`，将返回指令插入当前位置继续执行。 |
| `QueryCards` | `zone, side, type/types, name, nameMatch, storeAs` | 将符合条件的卡片保存为命名集合。不能直接查询敌方手牌或牌库身份；查看牌库使用 `Scry`。 |
| `Choose` | 原选择参数，另加 `minCount=0, maxCount=3, storeAs="picked"` | 顺序选择至多 32 张，达到最低数量后可结束。整个多选共用一个倒计时；`selected` 是最后选择的一张，集合保存全部选择。 |
| `ChooseHiddenHand` | `minCount, maxCount, storeAs` | 以一次性背面句柄选对方手牌。选择前不发送卡名、时间、力量或真实实例 ID。 |
| `InvokeDecision` | `target="set", set="decision"` | 在当前操作者和效果来源下执行所选决策卡 `onPlay`。不移动被调用的卡、不重复支付印刷时间、不消耗决策使用次数、不发送新的 Played 事件。 |
| `Pay` | `amount, costs, after={...}` | 效果处理中支付额外费用。只有确认且完整支付后执行 `after`；取消、超时或不足时跳过 `after`，继续 Pay 后面的普通指令。 |

`type` 是单一卡种，`types={"通常时魔","契约时魔","衍生物"}` 表示任一所列卡种，两者不能同时填写。`nameMatch="exact"` 默认精确匹配；`"fuzzy"` 是忽略大小写的子串匹配。时间范围继续使用 `minTime/maxTime`，不是多选数量。

`QueryCards.append=true` 将结果并入已有集合，可构造“己方任意时魔 **或** 对方时间 1 以下时魔”等不同条件的候选并集，再交给 `Choose target="set"`。`enteredThisTurn=true` 将查询／选择限制为本回合进入当前区域的卡，例如刚除外的卡。

```lua
effects = {
    onActivate = function(ctx)
        return {
            { op = "RollDice", amount = 6, storeAs = "die", reveal = true },
            { op = "Continue", callback = "afterDice" }
        }
    end,

    afterDice = function(ctx)
        if ctx.vars.effect.die % 2 ~= 0 then
            return {}
        end

        return {
            { op = "Choose", side = "opponent", maxTime = 2 },
            { op = "Destroy", target = "selected" }
        }
    end
}
```

动画使用独立的视觉滚动数字，最终值来自主机，约 1.6 秒完成淡入、滚动、定格与淡出；不会推进内核随机数，也不会为了动画再次掷骰。展示与卡片展示共用排队机制。

## 新上下文

- `ctx.instanceId`：当前效果来源的实例 ID。
- `ctx.time / ctx.power`：来源当前时间与力量。
- `ctx.opponentClock`：`"black"` 或 `"white"`。
- `ctx.opponentCost / opponentDamage / opponentHandCount`：对方费用、伤害指针和手牌数量。
- `ctx.publicCards`：公开区域的卡片元数据数组。每项包含 `instanceId, id, name, type, race, sign, owner, time, power, zone, node, zoneEnteredTurn`。
- `ctx.sets.<名称>`：已保存集合的当前可见元数据。匿名敌方手牌在仍然隐藏时不会暴露元数据；移入弃牌区后可在下一次 `Continue` 中读取其时间等。

分区数量、总时间等可直接在 Lua 遍历 `ctx.publicCards` 计算。`QueryCards` 保存实体引用，用于后续操作；上下文数组是供查询的快照，修改 Lua 数组不会改内核。

## 费用

原有 `activationCosts`、`playCosts`、`triggers[].costs` 继续有效，`Pay.costs` 复用同一套原子支付机制。

| kind | 用途 |
| --- | --- |
| `Time` | 玩家费用，支持 `min/max`。 |
| `Discard` | 卡片移入弃牌区，默认手牌。支持可变数量。 |
| `Destroy` | 破坏己方场上一个时魔堆叠。 |
| `Exile` | 将所选卡除外。 |
| `ReturnToDeck` | 放回卡顶或卡底，`position="top"/"bottom"`。 |
| `MoveSelf` | 来源自身移动；`amount=1, destination="Hand"/"Discard"/"Exile"`。契约仍遵守契约离场规则。 |
| `RemoveToken` | 消灭所选衍生物。 |
| `TokenTime` | 选择一个时间足够的衍生物，减少其时间作为费用。例如 `amount=4`。不扣玩家费用；目前为固定数值。 |

卡片费用支持 `zone` 或 `zones={"Hand","OffField"}`，以及 `type, race, name, nameMatch` 筛选。普通卡片费用的 `min/max` 是支付张数，先选数量再选卡；取消或超时不会提前移动已选卡。`paySelectedTime=true` 可额外支付所选卡时间之和；`storeAs` 保存数量，`storeTime=true` 改为保存选中卡时间之和。

```lua
-- 选择结果已保存在 decision 集合中，此回调在掷骰之后运行。
afterDice = function(ctx)
    local card = ctx.sets.decision[1]
    if ctx.vars.effect.die % 2 == 0 then
        return {
            {
                op = "Pay",
                amount = card.time,
                costs = { { kind = "Discard", amount = 1 } },
                after = {
                    { op = "InvokeDecision", target = "set", set = "decision" }
                }
            }
        }
    end
    return {
        { op = "ReturnToDeck", target = "set", set = "decision", position = "top" },
        { op = "Shuffle" }
    }
end
```

支付本身不是“效果破坏”的替代窗口。支付前统一校验、确认后整体提交；不能用防破坏规避支付后仍取得效果。

## 事件、战斗与替代

| 事件 | 上下文 |
| --- | --- |
| `DestructionPending` | 目标即将被破坏，`event.card` 为目标、`reason` 为 battle/effect、`source` 为破坏来源。可选诱发处理完后才真正提交破坏，来源剩余指令暂停。 |
| `Destroyed` | 现在保留战斗攻击者作为 `event.source`，供后续选择与延迟追踪。 |
| `BattleEnded` | `attacker, target, targetPlayer, cancelled`；正常结束和目标失效的战斗都产生结束事件。 |
| `DeckPositioned` | `position="top"/"bottom"`，包括牌库内重排。普通区域移动仍用 CardMoved。 |
| `ForesightResolved` | 特效标记处理完成，`event.targets` 提供受影响卡片的元数据，可进行复制。 |

`activeZone="Hand"/"Deck"` 允许监听，但必须 `listen.subject="self"`。公开区域继续支持任意对象监听。所有诱发保持可选，并按回合玩家优先组织处理。

效果目标新支持 `target="cause"/"attacker"/"defender"`。它们使用事件中的实例与场上代际校验，不会错误跟踪离场又回来的新规则实例。

| 操作 | 参数和行为 |
| --- | --- |
| `PreventDestruction` | `target, from="battle"/"effect"/"any", duration`。在破坏前检查保护。 |
| `ReplaceDestruction` | `target` 是受保护卡，先前 `selected` 是代替被破坏的卡；`from` 过滤原因。替代成功后消耗该条替代。 |
| `ChangeDestructionReason` | 在 DestructionPending 中用 `from="effect"` 等改变本次破坏分类。之后只发布最终分类的 Destroyed。 |
| `RedirectAttack` | 将攻击转移到所选己方场上时魔；适用于攻击宣言诱发、响应或未来视过程中。 |
| `ModifyBattleDamage` | 修改当前战斗伤害，可在响应期间使用。 |
| `ProtectSelection` | 让目标不能被 `side="own"/"opponent"/"any"` 所指定玩家的效果选择。 |
| `BlockName` | 禁止指定玩家在本回合或下回合结束前发动指定卡名的效果；省略 name 使用来源卡名。 |

```lua
triggers = {
    {
        id = "change_destruction",
        event = "DestructionPending",
        listen = { side = "own" },
        condition = function(ctx)
            return ctx.event.reason == "battle"
        end,
        onTrigger = function(ctx)
            return { { op = "ChangeDestructionReason", from = "effect" } }
        end
    }
}
```

## 附着、衍生物、临时技能

- `AttachUnder`：`target` 选己方承载时魔，`set` 指定要放入下方的手牌集合，省略 set 使用 selected。对方卡片保持背面并失去下层效果，内核分别维护控制者 `Owner` 和原始所有者 `OriginalOwner`；离场恢复原所有者。玩家卡不参与这个接口。
- `SpawnBoard`：`definitionId` 必须是衍生物，等待选择合法空圆阵，`amount > 0` 覆盖生成时间，省略使用定义时间。支持 `placement="any"/"player"/"defense"`。
- `Summon`：同样增加 `placement`，可限制在己方玩家／防御圆阵的空位。
- `RemoveToken`：只消灭指定衍生物，发离场事件，不发 Destroyed。
- `GrantTrigger`：`target, key="BattleEnded", callback="grantedAbility", duration`。在目标身上授予可选监听，回调中的来源是被授予的卡；原始 Lua 文件仍负责运行回调。需要额外条件时在回调返回 `{}`，需要额外费用时返回 `Pay`。
- `GrantTrigger` 另外支持 `oncePerTurn=true` 和 `oncePerNamePerTurn=true`，分别维护授予能力的实例／卡名使用次数；不会借用来源卡其他技能的次数。
- `CopyForesight`：在 ForesightResolved 监听中复制刚处理的标记计划，以当前效果来源／控制者执行；默认需要选择的步骤重新选择。指定 `target="selected"` 或命名集合时，只将原标记实际应用于场上卡片的操作作用于新目标，保留当时已计算的数值，不重复抽卡等其他步骤。不递归发布新的 ForesightResolved。原标记目标保存为 `markTargets` 集合。

## 修正持续期与时点

- `Modify.duration="battle"`：当前战斗结束时清除该修正，其他来源／持续期保留。保护、授予技能与决策费用修正也支持 battle。
- `ModifyDecisionCost`：按 `side` 为某玩家增加或减少决策使用费用，支持 turn/nextTurn/permanent/source/battle。实际支付与本地菜单使用同一数值，最低为 0。
- `Schedule.timing="nextMain"`：下一个回合的主要阶段，不限定玩家。
- `"nextOpponentMain"`：下个对方主要阶段。
- 既有 `nextOwnMain, nextBattle, nextAttack, nextDefend, turnEnd` 保持原义。

以本次战斗为持续期的指令应放在攻击相关回调或 nextBattle 延迟计划中。来源绑定效果仍按来源离场／被压在下方／无效／代际改变而失效。

## 维护与验证

网络消息新增骰子结果、决策费用修正、匿名背面选择和名称禁用状态，协议版本已更新为 22；两端需使用同一版程序和 Content。

内核与实际 Lua 工作流自动化测试覆盖取消、原子支付、隐藏信息、匿名附着归属、破坏前窗口、事件来源、持续期、标记复制、龙蛋转化等。Unity 场景中的动画视觉效果和双实例联机还需 Play Mode 实测；构建通过不能替代该检查。
