-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD02-000B-C",
    rulesId = "PD02-000B",
    effectText = "【自】（回合1）你的时魔被攻击时（→）若场上或契约区存在名字含有“莉莉”的你的契约时魔，[舍弃1张手牌]，抽1张卡。",
    name = "玩家",
    faction = "白银草原",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/PD02-000B-C.png",
    effects = {
        triggers = {
            {
                id = "discard_draw",
                label = "discard_draw",
                event = "AttackDeclared",
                listen = {
                    side = "opponent"
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
                    return ctx.event.target and ctx.event.target.owner == ctx.owner and hasContract(ctx, "莉莉") and (true)
                end,
                costs = {
                    {
                        kind = "Discard",
                        amount = 1
                    }
                }
            }
        }
    }
}
