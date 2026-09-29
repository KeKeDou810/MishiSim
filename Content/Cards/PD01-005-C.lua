return{
    id = "PD01-005-C",
    rulesId = "PD01-005",
    effectText = "【自】这个时魔登场时（→）抽1张卡，选择1张手牌舍弃。",
    name = "兔型清洁机器人 RABBOT-1",
    faction = "星河联盟",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "机械兵",
    artworkPath = "Artwork/PD01-005-C.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Draw",
                    amount = 1
                },
                {
                    op = "Choose",
                    zone = "Hand",
                    side = "own",
                    prompt = "选择一张手牌舍弃"
                },
                {
                    op = "Discard",
                    target = "selected"
                }
            }
        end
    }
}
