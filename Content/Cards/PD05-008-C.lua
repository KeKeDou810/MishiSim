

return{
    id = "PD05-008-C",
    rulesId = "PD05-008",
    effectText = "【自】这个时魔通过支付时间的方式从手牌登场时（→）选择对方1个时间2以下的时魔破坏。",
    name = "幽灵少女 梅",
    faction = "潘多拉",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "幽灵",
    artworkPath = "Artwork/PD05-008-C.png",
    effects = {
        onSummon = function(ctx)
            if not ((ctx.reason == "paidHand" or ctx.reason == "overclock")) then return {} end
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "opponent",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    },
                    maxTime = 2
                },
                {
                    op = "Destroy",
                    target = "selected"
                }
            }
        end
    }
}
