local function hasEggTime(ctx, time)
    for _, card in ipairs(ctx.publicCards) do
        if card.owner == ctx.owner and (card.zone == "Board" or card.zone == "OffField")
            and card.type == "衍生物" and string.find(card.name, "龙蛋", 1, true) and card.time >= time then return true end
    end
    return false
end

return{
    id = "PD03-008-C",
    rulesId = "PD03-008",
    effectText = "【自】这个时魔通过支付时间的方式从手牌登场时（→）如果场上存在时间大于等于4的名字含有“龙蛋”的你的衍生物，选择你的弃牌区中1张时间1的卡，加入手牌。",
    name = "龙护士 云语",
    faction = "维斯王朝",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "龙人",
    artworkPath = "Artwork/PD03-008-C.png",
    effects = {
        onSummon = function(ctx)
            if not ((ctx.reason == "paidHand" or ctx.reason == "overclock") and hasEggTime(ctx, 4)) then return {} end
            return {
                {
                    op = "Choose",
                    zone = "Discard",
                    minTime = 1,
                    maxTime = 1
                },
                {
                    op = "Move",
                    target = "selected",
                    zone = "Hand"
                }
            }
        end
    }
}
