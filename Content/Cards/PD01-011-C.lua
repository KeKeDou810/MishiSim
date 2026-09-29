

return{
    id = "PD01-011-C",
    rulesId = "PD01-011",
    effectText = "【永】你的回合中，你的手牌每有1张，这张卡的力量+500。（这个效果最多能让这张卡的力量增加6000）",
    name = "远星小队的指挥官 妮娜",
    faction = "星河联盟",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/PD01-011-C.png",
    effects = {
        continuous = function(ctx)
            if not (ctx.owner == ctx.activePlayer) then return {} end
            local bonus = math.min(6000, ctx.handCount * 500)
            return { { id = "scaled_power", stat = "power", amount = bonus } }
        end
    }
}
