return{
    id = "PD02-009-C",
    rulesId = "PD02-009",
    effectText = "【自】这个时魔登场时（→）这个回合中，场上所有的你的时魔力量+2000。",
    name = "森林服务生 艾拉",
    faction = "白银草原",
    level = 5,
    power = 6000,
    sign = "未来视",
    type = "通常时魔",
    race = "兽人",
    artworkPath = "Artwork/PD02-009-C.png",
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
