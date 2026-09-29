-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-047B-C",
    rulesId = "HZ01-047B",
    effectText = "【自】（回合1）你的时魔进行攻击时（→）若场上或契约区存在名字含有“超星机甲”的你的契约时魔，[将1张手牌放置到卡组底]，抽1张卡。",
    name = "玩家",
    faction = "星河联盟",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/HZ01-047B-C.png",
    effects = {
        triggers = {
            {
                id = "attack_draw",
                label = "attack_draw",
                event = "AttackDeclared",
                listen = {
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Draw",
                            amount = 1
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return hasContract(ctx, "超星机甲")
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
