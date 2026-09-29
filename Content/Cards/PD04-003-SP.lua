

return{
    id = "PD04-003-SP",
    rulesId = "PD04-003",
    effectText = "【自】这个时魔被战斗破坏时（→）下个回合中使用决策卡时需要支付的时间-1。",
    name = "魔灵 奈奈喵",
    faction = "绿洲",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "魔灵",
    artworkPath = "Artwork/PD04-003-SP.png",
    effects = {
        onDestroyed = function(ctx)
            if not (ctx.reason == "battle") then return {} end
            return {
                {
                    op = "Schedule",
                    target = "self",
                    timing = "nextMain",
                    after = {
                        {
                            op = "ModifyDecisionCost",
                            amount = -1
                        }
                    }
                }
            }
        end
    }
}
