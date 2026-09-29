

return{
    id = "PD05-005-C",
    rulesId = "PD05-005",
    effectText = "【自】这个时魔登场时（→）以背面选择对方的1张手牌舍弃，对方抽1张卡。",
    name = "双子恶魔",
    faction = "潘多拉",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "恶魔",
    artworkPath = "Artwork/PD05-005-C.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "ChooseHiddenHand"
                },
                {
                    op = "Discard",
                    target = "selected"
                },
                {
                    op = "Draw",
                    side = "opponent",
                    amount = 1
                }
            }
        end
    }
}
