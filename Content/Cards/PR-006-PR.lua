return{
    id = "PR-006-PR",
    rulesId = "PR-006",
    effectText = "【自】这个时魔登场时（→）选择场上的1个你的时魔，这个回合中，那个时魔的力量+1000。",
    name = "龙骑士 玉人",
    faction = "维斯王朝",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "人类",
    artworkPath = "Artwork/PR-006-PR.png",
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
