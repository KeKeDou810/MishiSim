return{
    id = "PD04-005-C",
    rulesId = "PD04-005",
    effectText = "【自】你的回合中，你使用决策卡时（→）投掷1个骰子，如果是偶数，选择对方的1个时间2以下的时魔破坏。",
    name = "电子精灵 雪乘",
    faction = "绿洲",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "电子精灵",
    artworkPath = "Artwork/PD04-005-C.png"
,
    effects = {
        triggers = {
            {
                id = "even_dice_destroy",
                event = "Played",
                listen = { side = "own", type = "决策卡" },
                condition = function(ctx)
                    return ctx.activePlayer == ctx.owner
                end,
                onTrigger = function(ctx)
                    return {
                        { op = "RollDice", amount = 6, storeAs = "die", reveal = true },
                        { op = "Continue", callback = "afterDice" }
                    }
                end
            }
        },
        afterDice = function(ctx)
            if ctx.vars.effect.die % 2 ~= 0 then
                return {}
            end
            return {
                { op = "Choose", side = "opponent", maxTime = 2,
                  types = { "通常时魔", "契约时魔", "衍生物" }, prompt = "选择对方时间 2 以下的时魔" },
                { op = "Destroy", target = "selected" }
            }
        end
    }
}
