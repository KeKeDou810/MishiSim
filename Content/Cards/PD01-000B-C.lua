-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD01-000B-C",
    rulesId = "PD01-000B",
    effectText = "【自】（回合1）你的时魔被攻击时（→）若场上或契约区存在名字含有“艾莲”的你的契约时魔，[舍弃最多2张手牌]，选择1个正在被攻击的你的时魔，舍弃的手牌每有1张，这次战斗中，那个时魔的力量+1000。",
    name = "玩家",
    faction = "星河联盟",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/PD01-000B-C.png",
    effects = {
        triggers = {
            {
                id = "discard_guard",
                label = "discard_guard",
                event = "AttackDeclared",
                listen = {
                    side = "opponent"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Continue",
                            callback = "guardPower"
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return ctx.event.target ~= nil and ctx.event.target.owner == ctx.owner and hasContract(ctx, "艾莲") and (true)
                end,
                costs = {
                    {
                        kind = "Discard",
                        min = 0,
                        max = 2,
                        storeAs = "discarded"
                    }
                }
            }
        },
        guardPower = function(ctx)
            return { { op = "Modify", target = "defender", stat = "power", amount = (ctx.vars.effect.discarded or 0) * 1000, duration = "battle" } }
        end
    }
}
