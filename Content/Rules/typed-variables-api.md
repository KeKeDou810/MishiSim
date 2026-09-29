# 带类型变量与作用域

根据 counter 持续计算力量、多来源叠加与来源失效清理，见 [持续修正与自动效果 API](continuous-and-trigger-api.md)。

Lua 返回指令写入变量，C# 内核负责保存、类型校验和自动生命周期。它不会运行时生成 C# 字段，也不会让脚本直接修改对局对象。

## 作用域及自动清理

| scope | 保存位置 | 生命周期 |
| --- | --- | --- |
| `effect`（默认） | 当前 EffectExecution | 跨选择窗口及 after 回调；本次效果执行完毕或中止后不再保留 |
| `card` | 目标卡片实例；默认发动卡 | 跨阶段、回合保存；离开场上／场外区以及重新登场时清除；移动圆阵、横置不会清除 |
| `player` | 所属玩家 | 跨阶段、回合和卡片离场保存，直至退出或重开对局 |
| `turn` | 当前全局回合 | 双方切换回合时自动清除，包括倒计时强制结束回合 |
| `match` | 当前对局 | 整场对局保存，退出或重开对局时释放 |

turn 和 match 各自是共享键空间；player 和 card 各自拥有独立键空间。需要独立计数时优先使用相应作用域，避免多个效果复用共享键名。

C# GC 自动回收失去引用的存储和值，不需要脚本手动释放内存，也不主动调用 GC.Collect。`ClearValue` 用于规则要求的提前删除，不是释放内存的必需操作。对局判定胜负后会保留结果展示所需状态，直到退出或重开时解除对局引用；并非胜负判定的一瞬间强制清空所有内存。

## 类型

支持 integer、number、boolean、string。Lua 整数自动识别为 integer，带小数的数值识别为 number。`valueType` 可显式声明；例如 value=1、valueType="number" 会保存为小数类型。

同一个作用域的同名变量允许反复写入同类型的值。改成其他类型前必须 ClearValue，避免误用同名键静默改变含义。整数和小数不隐式互转；boolean false 和空字符串都是合法值，不代表变量不存在。

```lua
{ op = "SetValue", scope = "effect", key = "bonus", value = 1000 }
{ op = "SetValue", scope = "card", key = "ready", value = false }
{ op = "SetValue", scope = "player", key = "label", value = "任意字符串" }
{ op = "SetValue", scope = "match", key = "ratio", value = 1, valueType = "number" }

-- AddValue 要求变量已存在，且增量和变量是同一种数值类型。
{ op = "AddValue", scope = "effect", key = "bonus", value = 500 }

{ op = "ClearValue", scope = "card", key = "ready" }
```

变量名为 1–32 位 ASCII 标识符；每个存储最多 128 个键，字符串最多 1024 字符，数值须有限且在 ±1e9 内。删除不存在的键是安全的；读取不存在的键或类型不符会按脚本错误处理。

## Lua 读取

回调上下文提供原生 Lua 值：

```lua
ctx.vars.effect.bonus
ctx.vars.card.ready
ctx.vars.player.counter
ctx.vars.turn.flag
ctx.vars.match.label
```

card 指发动卡，player 指发动者。没有的键返回 nil；可用 `ctx.vars.player.counter or 0` 作为初始化值。ctx 是副本，直接给 ctx.vars 赋值不会修改内核，仍需返回 SetValue 等指令。

onPlay / onActivate / onSummon 等初次构建指令时，effect 作用域为空；Scry 的 after 回调能看到本次效果已经写入的 effect 变量。之后另一次发动有自己的空 effect 存储。

示例：玩家累计发动次数，并通过临时变量修改力量：

```lua
effects = {
    onActivate = function(ctx)
        local count = ctx.vars.player.counter or 0

        return {
            {
                op = "SetValue",
                scope = "player",
                key = "counter",
                value = count + 1,
                visibility = "public"
            },
            { op = "SetValue", key = "bonus", value = 1000 },
            {
                op = "Modify",
                target = "self",
                stat = "power",
                amount = { var = "bonus", scope = "effect" }
            }
        }
    end
}
```

## 内核读取变量引用

`amount` 和 SetValue / AddValue 的 `value` 支持引用，执行到该条指令时才读取，因此能使用前面指令刚写入的值：

```lua
{
    op = "SetValue",
    scope = "player",
    key = "savedBonus",
    value = { var = "bonus", scope = "effect" }
}

{
    op = "Draw",
    amount = { var = "drawCount", scope = "player" }
}
```

amount 必须引用 integer，且仍受具体效果的范围限制。例如 Scry 数量仍为 1–32，Draw 仍有其数量上限。暂不支持在任意参数中直接嵌入引用，也不支持 Lua 表、函数或任意 C# 对象作为变量值。

引用默认 card=self、player=own，可指定：

```lua
-- 读取之前 Choose 选中卡片的变量。
{ var = "bonus", scope = "card", target = "selected" }

-- 读取对方玩家的变量。
{ var = "counter", scope = "player", side = "opponent" }
```

写入 card 作用域可结合原有 target / set 选择目标；写入 player 作用域可通过 side="opponent" 指向对手。AddValue 保持原有变量的可见性，ClearValue 删除该键。

## 联机与可见性

SetValue 默认 `visibility = "private"`；也可指定 public。

- card / player 的私有值属于对应卡片所有者／玩家。
- turn / match 的私有值属于写入该值的效果发动者。
- 私有值仅同步给所属玩家，公开值可同步给双方。
- 隐藏牌库卡片、对方手牌不会因为存了公开变量而泄漏实例 ID。
- effect 临时存储不进入对手快照。

客户端菜单读取相同的可见变量，继续在本地判断条件；不会由 Host 下发按钮权限。Lua ctx.vars 同样只含当前发动者可见的数据；内核明确的变量引用可读取所指定存储的实际值。需要客户端判断的共享发动条件，请使用公开变量。

## 与旧接口兼容

`DeclareCardName` 的 storeAs 现在写入 effect 作用域的 string，可用 `ctx.vars.effect.declaredName` 读取。`ctx.results` 保留为本次 effect 作用域字符串值的兼容视图。

卡片集合继续使用 storeAs、target="set"、except 和 ctx.sets，保存的是实例身份，不混入普通字符串变量。集合目前仍仅属于本次效果。

## C# 接口

- `EffectValue`：不可变的四种类型值及安全数值运算。
- `EffectVariableStore`：Set / Add / Read / Clear、类型稳定性和可见性。
- `VariableReference`：作用域、键及读取目标。
- `MatchVariables.cs`：作用域解析、执行时参数引用及过滤后的网络快照。
- `SetValueEffect` / `AddValueEffect` / `ClearValueEffect`：独立处理器，自动进入内核效果编辑器。
- `NetworkVariables`：明确类型的网络 DTO 编解码；网络协议版本已更新，双方需使用同一版本。
