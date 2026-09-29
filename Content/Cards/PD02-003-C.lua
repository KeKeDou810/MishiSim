

return{
    id = "PD02-003-C",
    rulesId = "PD02-003",
    effectText = "【自】（黑时钟）这个时魔被战斗破坏时（→）你的下个回合的主要阶段开始时，将你的费用时间+1，但不会达到12。",
    name = "双子猫",
    faction = "白银草原",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "森林兽",
    artworkPath = "Artwork/PD02-003-C.png",
    effects = {
        onDestroyed = function(ctx)
            if not (ctx.reason == "battle" and ctx.clock == "black") then return {} end
            return {
                {
                    op = "Schedule",
                    target = "self",
                    timing = "nextOwnMain",
                    after = {
                        {
                            op = "Cost",
                            amount = 1,
                            maxCost = 11
                        }
                    }
                }
            }
        end
    }
}
