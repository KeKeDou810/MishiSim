

return{
    id = "PD02-008-C",
    rulesId = "PD02-008",
    effectText = "【自】（黑时钟）这个时魔通过支付时间的方式从手牌登场时（→）将你的费用时间+1。",
    name = "森林精灵 希尔芙",
    faction = "白银草原",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "精灵",
    artworkPath = "Artwork/PD02-008-C.png",
    effects = {
        onSummon = function(ctx)
            if not (ctx.clock == "black" and (ctx.reason == "paidHand" or ctx.reason == "overclock")) then return {} end
            return {
                {
                    op = "Cost",
                    amount = 1
                }
            }
        end
    }
}
