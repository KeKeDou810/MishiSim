local function hasPublic(ctx, zone, name)
    for _, card in ipairs(ctx.publicCards) do
        if card.owner == ctx.owner and card.zone == zone and string.find(card.name, name, 1, true) then return true end
    end
    return false
end

return{
    id = "HZ01-055-C",
    rulesId = "HZ01-055",
    effectText = "【自】这个时魔登场时（→）若你有“符文区”，选择1个你的时魔，这个回合中，那个时魔的力量+1000。",
    name = "守护之小天使 格迪安",
    faction = "绿洲",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "天使",
    artworkPath = "Artwork/HZ01-055-C.png",
    effects = {
        onSummon = function(ctx)
            if not (hasPublic(ctx, "OffField", "符文")) then return {} end
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                {
                    op = "Modify",
                    target = "selected",
                    stat = "power",
                    amount = 1000,
                    duration = "turn"
                }
            }
        end
    }
}
