return{
    id = "PD05-009-C",
    rulesId = "PD05-009",
    effectText = "【自】这个时魔登场时（→）这个回合中，场上所有的你的时魔力量+2000。",
    name = "幽灵少女 夜莺",
    faction = "潘多拉",
    level = 5,
    power = 6000,
    sign = "未来视",
    type = "通常时魔",
    race = "幽灵",
    artworkPath = "Artwork/PD05-009-C.png",
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
