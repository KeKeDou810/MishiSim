

return{
    id = "HZ01-067-SR",
    rulesId = "HZ01-067",
    effectText = [[这张卡只能在你的回合使用。将你的伤害指针顺时针移动2格。
【起】（弃牌区）[将这张卡除外]，将你的伤害指针顺时针移动2格。
【特效标记（特殊标记）】：选择1个这个回合中没有重构过的你的时魔，将那个时魔重构，这个回合中，这个效果选择的时魔失去所有效果。]],
    name = "痛楚邀约",
    faction = "不明",
    level = 1,
    power = 0,
    sign = "特效标记（特殊标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/HZ01-067-SR.png",
    effects = {
        playTurn = "own",
        activateZone = "Discard",
        activationCosts = {
            {
                kind = "MoveSelf",
                destination = "Exile",
                amount = 1
            }
        },
        onPlay = function(ctx)
            return {
                {
                    op = "Damage",
                    amount = 2
                }
            }
        end,
        onActivate = function(ctx)
            return {
                {
                    op = "Damage",
                    amount = 2
                }
            }
        end,
        onForesight = function(ctx)
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    },
                    notRebuiltThisTurn = true
                },
                {
                    op = "Ready",
                    target = "selected"
                },
                {
                    op = "SuppressEffects",
                    target = "selected"
                }
            }
        end
    }
}
