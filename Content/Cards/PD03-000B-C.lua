-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD03-000B-C",
    rulesId = "PD03-000B",
    effectText = "【自】（回合1）你的时魔被攻击时（→）若场上或契约区存在名字含有“赤羽”的你的契约时魔，[将1个名字含有“龙蛋”的你的衍生物的时间-4]，选择1个你的正在被攻击的时魔，这次战斗中不会被破坏。",
    name = "玩家",
    faction = "维斯王朝",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/PD03-000B-C.png",
    effects = {
        triggers = {
            {
                id = "egg_guard",
                label = "egg_guard",
                event = "AttackDeclared",
                listen = {
                    side = "opponent"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "PreventDestruction",
                            target = "defender",
                            from = "any",
                            duration = "battle"
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return ctx.event.target ~= nil and ctx.event.target.owner == ctx.owner and hasContract(ctx, "赤羽") and (true)
                end,
                costs = {
                    {
                        kind = "TokenTime",
                        zones = {
                            "OffField",
                            "Board"
                        },
                        name = "龙蛋",
                        nameMatch = "fuzzy",
                        amount = 4
                    }
                }
            }
        }
    }
}
