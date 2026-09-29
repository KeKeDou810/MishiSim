# 未来视接口

## 规则与流程

双方结束攻击响应后、力量比较或玩家伤害前，攻击玩家选择是否进行未来视。按照规则书 Q&A，一次攻击最多进行「未来视标记数量 × 攻击目标数量」次。每次单独公开、处理标记、处理公开卡去向，再询问下一次；可以放弃剩余次数。

当前战斗入口是单目标攻击，因此 `未来视2` 最多检查两张。`ForesightRules.MaximumChecks(marks, attackTargets)` 可供后续多目标攻击复用；它本身不会创建多目标攻击。

Lua `sign = "未来视2"` 定义次数；`未来视` 默认一次。契约时魔没有显式次数时按规则拥有一次未来视。

展示复用 Scry：双方可看，只有攻击玩家可确认。公开牌暂存在 `Revealed` 区，避免抽卡标记抽到正在公开的自身。标记及相关诱发处理结束后，仍在该区的公开牌进入弃牌区；已经移动到其他区域的牌保留其去向。空牌库优先执行已有重洗与伤害规则。询问超时放弃剩余次数，展示超时按既有 Scry 确认流程处理。

## 默认标记

| `sign` 包含 | 处理 |
| --- | --- |
| 特效标记（抽卡标记） | 抽一张 |
| 特效标记（回复标记） | 回复一点，随后公开卡除外 |
| 特效标记（炸裂标记） | 选择一个场上时魔破坏 |
| 特效标记（特殊标记） | 调用卡片 `effects.onForesight` |

未来视不等于使用决策卡，不支付使用费用、不增加决策使用次数，也不调用 `onPlay`。

## 自定义标记

有特效标记的卡可定义独立回调，返回普通内核指令。回调中的 `ctx` 和 `target = "self"` 指公开卡本身。没有回调时使用默认标记处理；特殊标记缺少回调会报告脚本错误，空指令表则明确表示不追加操作。

```lua
effects = {
    onForesight = function(ctx)
        return {
            { op = "Draw", amount = 1 }
        }
    end
}
```

C# 扩展入口为 `IForesightEffectProvider.BuildForesight`。当前已补入闪光的符文、痛楚邀约、挖矿滚地龙及对应 PR 版本的特殊标记回调；其一般使用效果仍由各自已有脚本决定。

新增独立内核指令：`SuppressEffects`（目标本回合效果失效）、`ProtectFromEnemyUnitEffects`（直到下个对方回合结束，目标不能被对方时魔效果选择）、`ModifyBattleDamage`（修正当前攻击对玩家的伤害）。`Choose` 支持 `notRebuiltThisTurn = true`；`Ready` 记录重构发生的回合。

## 公开事件

`ForesightRevealed` 的 `ctx.event.card` 是攻击时魔，`ctx.event.revealed` 是公开卡；卡片事件快照新增 `faction` 字段。监听器可检查公开卡类型、国家并返回 `ModifyBattleDamage` 等操作。该事件在战斗结算前处理。

独立监听器可显式设置 `oncePerTurn = false` 和 `oncePerNamePerTurn = false`，避免继承卡片其他发动效果的次数限制。当前卡库中「未来视公开本国决策卡，伤害加一」按每次公开分别处理，多个结果可叠加。

网络协议版本为 20，主机与客户端需使用相同版本。
