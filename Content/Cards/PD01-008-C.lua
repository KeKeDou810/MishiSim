return{
    id = "PD01-008-C",
    rulesId = "PD01-008",
    effectText = "【自】这个时魔通过支付时间的方式从手牌登场时（→）抽1张卡。",
    name = "机甲战狐 福克西",
    faction = "星河联盟",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "星河兽",
    artworkPath = "Artwork/PD01-008-C.png",
    effects = {
        onSummon = function(ctx)
            if ctx.reason ~= "paidHand" and ctx.reason ~= "overclock" then
                return {
                }
            end
            return {
                {
                    op = "Draw",
                    amount = 1
                }
            }
        end
    }
}
