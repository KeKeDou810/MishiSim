-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-011-SR",
    rulesId = "HZ01-011",
    effectText = "【自】这个时魔登场时（→）若场上或契约区存在名字含有“维克多莉娅”的你的契约时魔，这个回合中，你将卡组的卡公开时，被公开的卡时间-1。",
    name = "召幻兽人 拉比",
    faction = "白银草原",
    level = 3,
    power = 4000,
    sign = "未来视",
    type = "通常时魔",
    race = "召幻兽",
    artworkPath = "Artwork/HZ01-011-SR.png",
    effects = {
        onSummon = function(ctx)
            if not (hasContract(ctx, "维克多莉娅")) then return {} end
            return {
                {
                    op = "ModifyRevealTime",
                    amount = -1
                }
            }
        end
    }
}
