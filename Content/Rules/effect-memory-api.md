# 效果内的目标集合与卡名宣言

整数、小数、布尔值、字符串及多种作用域见 [带类型变量 API](typed-variables-api.md)。宣言值也可从 ctx.vars.effect 中读取；命名卡片集合仍保留本页接口。

每次效果执行拥有独立的集合与结果存储。它们跨越该效果的选择窗口和 Scry 后续操作，但不会带到下一次发动，也不会串入另一张卡排队中的触发效果。

## Scry 后选一张加入手牌，其余送弃牌区

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "Scry",
                amount = 3,
                reveal = false,
                storeAs = "viewed",
                after = {
                    {
                        op = "Choose",
                        target = "set",
                        set = "viewed",
                        storeAs = "picked",
                        prompt = "选择一张加入手牌"
                    },
                    {
                        op = "Move",
                        target = "set",
                        set = "picked",
                        zone = "Hand"
                    },
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

排除依据是卡片实例身份，不是卡名或当前区域。即使 picked 已进入手牌，它仍会从 viewed 中被排除，不会再次送入弃牌区。

这里使用 Move 到 Discard；现有 Discard 指令只处理手牌，不能用于把牌库中的查看卡直接送入弃牌区。

## 宣言卡名，再查看卡底，猜中则加入手牌

```lua
effects = {
    onActivate = function(ctx)
        return {
            {
                op = "DeclareCardName",
                storeAs = "declaredName",
                prompt = "宣言一个卡名"
            },
            {
                op = "Scry",
                amount = 1,
                from = "bottom",
                reveal = true,
                after = "resolveDeclaredName"
            }
        }
    end,

    resolveDeclaredName = function(ctx)
        local card = ctx.scry[1]

        if card and card.name == ctx.results.declaredName then
            return {
                { op = "Move", target = "scry", zone = "Hand" }
            }
        end

        return {} -- 未猜中：卡片保持原位置
    end
}
```

上例公开查看到的卡；可按实际卡文改为 reveal=false。卡名宣言本身是公开的，确认后通过双方状态文字显示。

必须先确认宣言才能执行 Scry；修改、重复、过期、越界或另一玩家发来的宣言请求会被拒绝。宣言候选来自公开卡库的全部卡名，而不是任何一方的隐藏牌库。同名版本合并为一个候选，匹配的是 name，不是版本 ID。

宣言窗口沿用横向卡片弹窗，提供卡名搜索、卡图及确认按钮。最多同时显示 24 个匹配名称，可继续输入缩小范围。超时按排序后的首个合法名称完成宣言，然后才继续查看牌库；不会根据隐藏牌决定超时结果。

## 通用接口

| 写法 | 行为 |
| --- | --- |
| Scry 的 `storeAs = "viewed"` | 保存此次查看到的全部卡，包括不足指定数量或空集合 |
| Choose 的 `storeAs = "picked"` | 保存此次选中的卡；跳过或没有候选时保存空集合 |
| `target = "set", set = "viewed"` | 使用之前保存的集合，保持保存时顺序 |
| `except = "picked"` | 从当前目标中排除该集合，也可使用保留名 selected 或 scry |
| `RememberCards` + `storeAs` | 保存任意已解析目标集合，可组合 set 和 except 保存差集 |
| `DeclareCardName` + `storeAs` | 保存确认的卡名到 `ctx.results` |

例如先保存剩余卡，之后做更多选择也不会覆盖它：

```lua
{
    op = "RememberCards",
    target = "set",
    set = "viewed",
    except = "picked",
    storeAs = "remaining"
}
```

集合记录实例身份，区域变化不会自动移除成员；已处于 Removed 的卡不再作为操作目标。再次写入同名集合会替换旧值。`selected` 和 `scry` 分别指最近一次卡片选择、最近一次 Scry，可作为引用，但不能作为 storeAs。

命名使用 1–32 位 ASCII 标识符，如 viewed、picked_1、declaredName。引用未保存的集合会按脚本错误停止对局，避免静默漏做效果。

在 Scry 的命名后续 Lua 回调中：

- `ctx.results.declaredName` 是之前保存的卡名字符串。
- `ctx.sets.viewed` 是命名集合中的卡片信息数组，字段同 ctx.scry。
- `ctx.scry` 仍是当前 Scry 查看顺序的卡片信息。

这些 Lua table 是副本，不能直接改变对局；继续返回内核指令完成操作。

## C# 维护位置

- `MatchEffectMemory.cs`：集合保存、读取、差集及 Lua 上下文复制。
- `MatchCardDeclaration.cs`：宣言选择、锁定结果与超时流程。
- `RememberCardsEffect.cs`、`DeclareCardNameEffect.cs`：各自独立的效果处理器，自动进入 Effect Editor。
- `ICardNameProvider.CardNames`：公开卡库名称目录，与当前隐藏牌库无关。

集合和结果均保存在当前 EffectExecution 中；网络只发送当前选择需要的信息，不发送整份效果内存或隐藏牌库。
