return{
    id = "PD03-009-C",
    rulesId = "PD03-009",
    effectText = "【自】这个时魔登场时（→）这个回合中，场上所有的你的时魔力量+2000。",
    name = "星龙使 白轩",
    faction = "维斯王朝",
    level = 5,
    power = 6000,
    sign = "未来视",
    type = "通常时魔",
    race = "龙人",
    artworkPath = "Artwork/PD03-009-C.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Modify",
                    target = "all",
                    zone = "Board",
                    side = "own",
                    amount = 2000,
                    duration = "turn"
                }
            }
        end
    }
}
