return{
    id = "PD05-002-C",
    rulesId = "PD05-002",
    effectText = "【自】这个时魔登场时（→）选择场上的1个你的时魔，这个回合中，那个时魔的力量+1000。",
    name = "实习女巫 夜夜",
    faction = "潘多拉",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "女巫",
    artworkPath = "Artwork/PD05-002-C.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Choose",
                    side = "own",
                    zone = "Board",
                    prompt = "选择获得力量的己方时魔"
                },
                {
                    op = "Modify",
                    target = "selected",
                    stat = "power",
                    amount = 1000,
                    duration = "turn"
                }
            }
        end
    }
}
