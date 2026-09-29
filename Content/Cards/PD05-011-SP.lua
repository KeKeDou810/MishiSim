

return{
    id = "PD05-011-SP",
    rulesId = "PD05-011",
    effectText = "【永】你的回合中，对方的弃牌区中的卡每有2张，这张卡的力量+500。（这个效果最多能让这张卡的力量增加6000）",
    name = "幽灵公主 索菲亚",
    faction = "潘多拉",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "幽灵",
    artworkPath = "Artwork/PD05-011-SP.png",
    effects = {
        continuous = function(ctx)
            if not (ctx.owner == ctx.activePlayer) then return {} end
            local count = 0
            for _, card in ipairs(ctx.publicCards) do
                if card.owner ~= ctx.owner and card.zone == "Discard" then count = count + 1 end
            end
            return { { id = "scaled_power", stat = "power", amount = math.min(6000, math.floor(count / 2) * 500) } }
        end
    }
}
