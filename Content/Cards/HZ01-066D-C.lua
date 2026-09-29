-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-066D-C",
    rulesId = "HZ01-065B",
    effectText = "【自】（回合1）你的时魔被攻击时（→）若场上或契约区存在名字含有“异兽”的你的契约时魔，[舍弃2张手牌]，选择弃牌区中1张名字含有“异兽”的卡，加入手牌。",
    name = "玩家",
    faction = "潘多拉",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/HZ01-066D-C.png",
    effects = {
        triggers = {
            {
                id = "recover_beast",
                label = "recover_beast",
                event = "AttackDeclared",
                listen = {
                    side = "opponent"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Choose",
                            zone = "Discard",
                            name = "异兽",
                            nameMatch = "fuzzy"
                        },
                        {
                            op = "Move",
                            target = "selected",
                            zone = "Hand"
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return ctx.event.target and ctx.event.target.owner == ctx.owner and hasContract(ctx, "异兽") and (true)
                end,
                costs = {
                    {
                        kind = "Discard",
                        amount = 2
                    }
                }
            }
        }
    }
}
