return{
    id = "PR-007-PR",
    rulesId = "PR-007",
    effectText = "【自】这个时魔登场时（→）选择场上的1个你的时魔，这个回合中，那个时魔的力量+1000。",
    name = "中二少女 黑田阳奈",
    faction = "绿洲",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "人类",
    artworkPath = "Artwork/PR-007-PR.png",
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
