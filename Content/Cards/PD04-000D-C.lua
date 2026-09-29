-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD04-000D-C",
    rulesId = "PD04-000B",
    effectText = "【自】（回合1）你的时魔被攻击时（→）若场上或契约区存在名字含有“优尔”的你的契约时魔，[舍弃手牌中的1张决策卡，支付时间1]，选择1个你的时魔，对方的攻击对象转移为你选择的时魔。（对方可以无视距离进行攻击）",
    name = "玩家",
    faction = "绿洲",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/PD04-000D-C.png",
    effects = {
        triggers = {
            {
                id = "redirect_attack",
                label = "redirect_attack",
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
                            op = "RedirectAttack",
                            target = "selected"
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return ctx.event.target and ctx.event.target.owner == ctx.owner and hasContract(ctx, "优尔") and (true)
                end,
                costs = {
                    {
                        kind = "Discard",
                        type = "决策卡",
                        amount = 1
                    },
                    {
                        kind = "Time",
                        amount = 1
                    }
                }
            }
        }
    }
}
