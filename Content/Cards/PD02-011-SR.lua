local function isUnit(card)
    return card.type == "通常时魔" or card.type == "契约时魔" or card.type == "衍生物"
end

return{
    id = "PD02-011-SR",
    rulesId = "PD02-011",
    effectText = "【永】你的回合中，场上存在的时魔的时间每有2，这张卡的力量+500。（这个效果最多能让这张卡的力量增加6000）",
    name = "超速之斗神 长夜",
    faction = "白银草原",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "兽人",
    artworkPath = "Artwork/PD02-011-SR.png",
    effects = {
        continuous = function(ctx)
            if not (ctx.owner == ctx.activePlayer) then return {} end
            local total = 0
            for _, card in ipairs(ctx.publicCards) do if card.zone == "Board" and isUnit(card) then total = total + card.time end end
            return { { id = "scaled_power", stat = "power", amount = math.min(6000, math.floor(total / 2) * 500) } }
        end
    }
}
