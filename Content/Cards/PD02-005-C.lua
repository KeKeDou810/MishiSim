

return{
    id = "PD02-005-C",
    rulesId = "PD02-005",
    effectText = "【自】（黑时钟）这个时魔登场时（→）若你的费用时间小于等于4，将你的费用时间+1。",
    name = "白银继承者 小月",
    faction = "白银草原",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "兽人",
    artworkPath = "Artwork/PD02-005-C.png",
    effects = {
        onSummon = function(ctx)
            if not (ctx.clock == "black" and ctx.cost <= 4) then return {} end
            return {
                {
                    op = "Cost",
                    amount = 1
                }
            }
        end
    }
}
