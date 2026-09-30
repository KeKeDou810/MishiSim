-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-054D-C",
    rulesId = "HZ01-053B",
    effectText = "【自】（回合1）你的时魔被攻击时（→）若场上或契约区存在名字含有“维克多莉娅”的你的契约时魔，[将1张手牌放置到卡组底]，下个回合的主要阶段开始时，你的费用时间+1，但不会达到12。",
    name = "玩家",
    faction = "白银草原",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/HZ01-054D-C.png",
    effects = {
        triggers = {
            {
                id = "next_main_cost",
                label = "next_main_cost",
                event = "AttackDeclared",
                listen = {
                    side = "opponent"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Schedule",
                            target = "self",
                            timing = "nextMain",
                            after = {
                                {
                                    op = "Cost",
                                    amount = 1,
                                    maxCost = 11
                                }
                            }
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return ctx.event.target ~= nil and ctx.event.target.owner == ctx.owner and hasContract(ctx, "维克多莉娅") and (true)
                end,
                costs = {
                    {
                        kind = "ReturnToDeck",
                        zone = "Hand",
                        amount = 1,
                        position = "bottom"
                    }
                }
            }
        }
    }
}
