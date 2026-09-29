local function hasPublic(ctx, zone, name)
    for _, card in ipairs(ctx.publicCards) do
        if card.owner == ctx.owner and card.zone == zone and string.find(card.name, name, 1, true) then return true end
    end
    return false
end

return{
    id = "PD03-005-C",
    rulesId = "PD03-005",
    effectText = "【自】这个时魔登场时（→）若你没有“龙蛋衍生物”，则在场外生成1个“龙蛋衍生物”。若有，则选择1个你的名字含有“龙蛋”的衍生物，将其时间+1。",
    name = "龙之子 新竹",
    faction = "维斯王朝",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "人类",
    artworkPath = "Artwork/PD03-005-C.png",
    effects = {
        onSummon = function(ctx)
            if hasPublic(ctx, "OffField", "龙蛋") or hasPublic(ctx, "Board", "龙蛋") then
                return { { op = "QueryCards", zone = "OffField", type = "衍生物", name = "龙蛋", nameMatch = "fuzzy", storeAs = "eggs" },
                    { op = "QueryCards", zone = "Board", type = "衍生物", name = "龙蛋", nameMatch = "fuzzy", storeAs = "eggs", append = true },
                    { op = "Choose", target = "set", set = "eggs" }, { op = "Modify", target = "selected", stat = "time", amount = 1, duration = "permanent" } }
            end
            return { { op = "Spawn", definitionId = "PD03-T01-C" } }
        end
    }
}
