# 通用费用、距离、延迟效果和操作记录

## 决策卡和诱发费用

```lua
effects = {
    playCosts = {
        { kind = "Time", amount = 1 },
        { kind = "Discard", amount = 1 }
    },
    onPlay = function(ctx)
        return { { op = "Draw", amount = 2 } }
    end,
    triggers = {
        {
            id = "paid_draw",
            label = "支付时间抽卡",
            event = "Summoned",
            listen = { subject = "self" },
            costs = { { kind = "Time", min = 1, max = 3, storeAs = "paid" } },
            onTrigger = function(ctx)
                return {
                    { op = "Draw", amount = { scope = "effect", var = "paid" } }
                }
            end
        }
    }
}
```

`playCosts` 加在印刷时间之上。起动效果仍用 `activationCosts`，诱发的费用放在对应条目 `costs` 内。费用不足不发动；取消或超时不支付，也不消耗次数。费用选择阶段暂停原响应计时，使用整笔费用共享的一次选择倒计时；决策卡确认支付后才交接响应优先权。

初次构建 Lua 计划时尚未支付 X，应在指令参数中使用变量引用，或在后续回调里读取 `ctx.vars.effect.paid`。纯 C# 接口对应 `CardEffectRules.PlayCosts`、`AutomaticEffectPlan.Costs`、`EventSubscription.Costs`，类型都是 `ActivationCost[]`。

## 持续攻击距离

```lua
continuous = function(ctx)
    return {
        { id = "reach", stat = "range", target = "all", side = "own", amount = 1 }
    }
```

放在 `effects` 表中。多个来源叠加；来源离场、被覆盖或效果无效后移除该来源贡献。基础距离为 1，最终不低于 0。使用已有的图连接计算距离，不改变契约移动规则。

## 战斗延迟时点

`Schedule.timing` 支持 `nextAttack`、`nextDefend`、`nextBattle`（任一方），保留 `turnEnd`、`nextOwnMain`。战斗延迟在双方响应完成后、未来视前执行；旧目标离场重入不会被追踪。更多堆叠移动和费用规则见 [流程接口](summon-payment-schedule-api.md)。

## 操作记录与预览页签

战斗快照携带该玩家可见的最近 160 条记录，编号和回合用于定位。记录包括阶段、卡片移动、使用／发动、攻击宣言、费用和伤害变化、目标选择、诱发取舍、宣言卡名、Scry、洗牌、超时与胜负。公开场上数值变化显示力量／时间／距离和修正来源。私下查看的卡名仅发送给查看玩家，普通抽卡不会把卡名发给对手；私有变量不写日志。

`MatchActionLog` 负责记录和隐私过滤，`NetworkTestMatch.ActionLogFor(player)` 返回副本，`NetworkSnapshot.ActionLog` 同步到客户端。它是排查和显示用的有限历史，不是完整回放或审计存档。

`PreviewPanel.prefab` 已保存 `BattleTabs`、效果／记录按钮，使用 ZCOOLKuaiLe-Regular SDF 和 Atom One 配色。`CardPreview.SetBattleLog(matchId, entries)` 更新记录；`SelectPage(bool)` 切换页面。记录最新在上；切页保留各页滚动位置，悬停卡片只更新效果缓存，不打断正在查看的记录。牌库构筑界面不显示战斗页签。

验证：纯规则测试覆盖组合费用确认／取消／响应／诱发超时、变量保存、整叠离场、持续距离、攻击／被攻击时点和私密日志。Unity EditMode 另有预览 prefab 引用及切页测试；实际双窗口联机交互仍需在 Unity 中验证。
