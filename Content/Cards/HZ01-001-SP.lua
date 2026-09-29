

return{
    id = "HZ01-001-SP",
    rulesId = "HZ01-001",
    effectText = "【自】（回合1）你的回合中，对方的时魔被破坏时（→）抽1张卡。",
    name = "炎之龙巫女 芷烁",
    faction = "维斯王朝",
    level = 3,
    power = 4000,
    sign = "未来视",
    type = "通常时魔",
    race = "龙巫女",
    artworkPath = "Artwork/HZ01-001-SP.png",
    effects = {
        triggers = {
            {
                id = "enemy_destroyed",
                label = "enemy_destroyed",
                event = "Destroyed",
                listen = {
                    side = "opponent",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Draw",
                            amount = 1
                        }
                    }
                end,
                oncePerTurn = true,
                condition = function(ctx)
                    return ctx.owner == ctx.activePlayer
                end
            }
        }
    }
}
