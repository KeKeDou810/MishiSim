# Scry 与 ReturnToDeck API

命名集合、排除选中卡以及“先宣言卡名，再查看卡底”的完整示例见 [效果记忆与宣言 API](effect-memory-api.md)。

`Scry` 只负责查看指定数量的牌库卡片及可选公开，不改变卡片区域或顺序。放回卡顶／卡底由独立的 `ReturnToDeck` 效果负责，和 `Move`、`Discard` 一样通过后续指令组合。

## 查看与放回示例

```lua
onActivate = function(ctx)
    local count = 3 -- 可由 Lua 计算；最终须为 1–32 的整数

    return {
        {
            op = "Scry",
            amount = count,
            side = "own",
            from = "top",
            reveal = false,
            prompt = "查看三张卡",
            after = {
                {
                    op = "ReturnToDeck",
                    target = "scry",
                    position = "choose"
                }
            }
        }
    }
end
```

## Scry 参数

| 参数 | 默认值 | 含义 |
| --- | --- | --- |
| `amount` | 必填 | 查看数量，1–32；不足时只看剩余牌，不补牌、不洗牌 |
| `side` | `"own"` | 查看自己或 `"opponent"` 的牌库；由发动者查看和选择 |
| `from` | `"top"` | 从卡顶或 `"bottom"` 卡底开始查看 |
| `reveal` | `false` | false：仅操作者获知卡片身份；true：同时向双方发送中央公开展示事件 |
| `prompt` | 查看牌库 | 查看窗口标题 |
| `after` | 无 | 后续指令数组，或 `effects` 表中的 Lua 回调名称 |

Scry 不抽卡、不移动卡、不重新排序，也不额外收取费用。固定发动费用由原效果定义。操作者在横向弹窗查看后确认，接着执行 after；没有 after 就结束此次查看。空牌库不弹窗，回调仍收到空数组。超时自动确认。

## ReturnToDeck：独立放回效果

```lua
-- 将之前 Choose 选中的卡放回其所属牌库的卡底。
{ op = "ReturnToDeck", target = "selected", position = "bottom" }

-- 将本次 Scry 的全部卡放在卡顶。
{ op = "ReturnToDeck", target = "scry", position = "top" }

-- 由玩家逐张决定卡顶或卡底。
{ op = "ReturnToDeck", target = "scry", position = "choose" }
```

`position` 默认为 top，也可为 bottom 或 choose。该效果可以完全独立于 Scry 使用。目前支持目标处于牌库、手牌、弃牌区或除外区；衍生物以及场上、场外区和契约区目标不处理，避免绕过堆叠与契约离场规则。卡片回到其所有者的牌库。

固定放回按目标列表顺序从顶到底排列。choose 模式先点选卡片，再按“放回卡顶”或“放回卡底”；各组按点击顺序从顶到底排列。卡顶组第一张会最先抽到；卡底组第一张位于该组最上方。

整个放回选择共用一个响应倒计时，逐张操作不重置。超时保留已分配的卡顶／卡底组，将剩余目标按列表顺序追加到卡顶组。牌库调整统一在全部选择完成后提交。

## Scry 后续目标

`target = "scry"` 指最近一次查看的卡，默认全部。`index` 可指定从 1 开始的查看序号；from=bottom 时序号 1 为原卡底。序号不会因后续重新排列而改变。

after 在确认查看后、原指令列表的下一条之前执行。例如公开顶牌后送往弃牌区：

```lua
{
    op = "Scry",
    amount = 1,
    reveal = true,
    after = {
        { op = "Move", target = "scry", zone = "Discard" }
    }
}
```

查看三张，选一张加入手牌，另外两张保持原处：

```lua
{
    op = "Scry",
    amount = 3,
    after = {
        { op = "Choose", target = "scry", prompt = "选择一张加入手牌" },
        { op = "Move", target = "selected", zone = "Hand" }
    }
}
```

Choose 仍只向操作者发送候选卡身份。Scry 不会在后续操作结束后自动把卡片放回。

## Lua 后续回调

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "Scry",
                amount = 2,
                reveal = true,
                after = "resolveViewedCards"
            }
        }
    end,

    resolveViewedCards = function(ctx)
        local instructions = {}

        for index, card in ipairs(ctx.scry) do
            -- 可根据 card.sign 等字段判断具体效果。
            -- 示例：第一张加入手牌，其余放回卡底。
            if index == 1 then
                table.insert(instructions, {
                    op = "Move",
                    target = "scry",
                    index = index,
                    zone = "Hand"
                })
            else
                table.insert(instructions, {
                    op = "ReturnToDeck",
                    target = "scry",
                    index = index,
                    position = "bottom"
                })
            end
        end

        return instructions
    end
}
```

回调收到原有 ctx 字段，以及 `reason = "scry"` 和 `ctx.scry` 数组。每张卡含 `id`（版本 ID）、`name`、`type`、`race`、`sign`、`owner`、`power`、`time`。这些是信息副本，修改 table 不改变对局；返回最多 32 条内核指令，沿用 Lua 执行预算与校验。

省略 after 表示没有后续操作。当前禁止 after 内再启动 Scry；再次查看请放在外层指令列表。新的 Scry 会替换 `target=scry` 引用。

## C# 与 UI

- `Effects/Handlers/ScryEffect.cs`：独立查看效果。
- `Effects/Handlers/ReturnToDeckEffect.cs`：独立放回效果，编辑器 Effect Editor 自动收录。
- `EffectExecutionContext.Scry(instruction)`、`ReturnToDeck(cards, position)`：执行端口。
- `MatchScry.cs`：查看、公开与后续回调。
- `MatchDeckReturn.cs`：放回排序、选择及超时。
- `IScryEffectProvider.BuildScry(context, callback)`：可选回调接口，Lua 已实现。
- `EffectChoice.ViewedCards`：仅操作者快照中的查看数据，不并入公共场上卡。
- `ResolveDeckView` 命令：Scry 确认传空 ID 和位置 0；放回选择传候选实例 ID 和位置 1（顶）／2（底），由内核校验。
- 查看弹窗及放回按钮预置于 `BattleInteractionUI.prefab`，不运行时生成整套 UI。

未来视可用公开 Scry 加后续指令组合。本次提供基底，没有自动改写卡片，也没有接入完整的攻击未来视次数和标记触发规则。
