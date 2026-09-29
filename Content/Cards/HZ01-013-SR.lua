local function hasPublic(ctx, zone, name)
    for _, card in ipairs(ctx.publicCards) do
        if card.owner == ctx.owner and card.zone == zone and string.find(card.name, name, 1, true) then return true end
    end
    return false
end

return{
    id = "HZ01-013-SR",
    rulesId = "HZ01-013",
    effectText = "【自】这个时魔登场时（→）若你有“符文区”，抽1张卡。",
    name = "牢狱天使 桑德枫",
    faction = "绿洲",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "天使",
    artworkPath = "Artwork/HZ01-013-SR.png",
    effects = {
        onSummon = function(ctx)
            if not (hasPublic(ctx, "OffField", "符文")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 1
                }
            }
        end
    }
}
