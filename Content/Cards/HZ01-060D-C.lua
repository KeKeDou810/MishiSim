-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-060D-C",
    rulesId = "HZ01-059B",
    effectText = "【自】（回合1）你的时魔被攻击时（→）若场上或契约区存在名字含有“乌列尔”的你的契约时魔，[将手牌或符文区中的1张名字含有“符文”的卡放置到弃牌区]，选择1个你的时魔，这次战斗中，那个时魔的力量+1000。",
    name = "玩家",
    faction = "绿洲",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/HZ01-060D-C.png",
    effects = {
        triggers = {
            {
                id = "rune_guard",
                label = "rune_guard",
                event = "AttackDeclared",
                listen = {
                    side = "opponent"
                },
                onTrigger = function(ctx)
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
                            duration = "battle"
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return ctx.event.target ~= nil and ctx.event.target.owner == ctx.owner and hasContract(ctx, "乌列尔") and (true)
                end,
                costs = {
                    {
                        kind = "Discard",
                        zones = {
                            "Hand",
                            "OffField"
                        },
                        name = "符文",
                        nameMatch = "fuzzy",
                        amount = 1
                    }
                }
            }
        }
    }
}
