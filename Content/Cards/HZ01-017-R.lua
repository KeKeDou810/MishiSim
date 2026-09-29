-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-017-R",
    rulesId = "HZ01-017",
    effectText = [[【永】仅当场上或契约区存在名字含有“轩辕”的你的契约时魔时，这个时魔才能发动效果。
【自】（回合1）这个时魔进行攻击结束时（→）若对方持有（白时钟），[支付时间2，舍弃2张手牌]，将这个时魔重构。
【自】（回合1）这张卡以外的场上的你的时魔被效果破坏时（→）若对方持有（黑时钟），选择场上的1个时间3以下的通常时魔，[支付时间1]，将那个时魔破坏。]],
    name = "暗龙骑士 黯染",
    faction = "维斯王朝",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "龙骑士",
    artworkPath = "Artwork/HZ01-017-R.png",
    effects = {
        triggers = {
            {
                id = "attack_ready",
                label = "attack_ready",
                event = "BattleEnded",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Ready"
                        }
                    }
                end,
                oncePerTurn = true,
                condition = function(ctx)
                    return hasContract(ctx, "轩辕") and ctx.event.attacker and ctx.event.attacker.instanceId == ctx.instanceId and ctx.opponentClock == "white"
                end,
                costs = {
                    {
                        kind = "Time",
                        amount = 2
                    },
                    {
                        kind = "Discard",
                        amount = 2
                    }
                }
            },
            {
                id = "ally_destroyed",
                label = "ally_destroyed",
                event = "Destroyed",
                listen = {
                    side = "own",
                    subject = "other",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Choose",
                            zone = "Board",
                            side = "any",
                            type = "通常时魔",
                            maxTime = 3
                        },
                        {
                            op = "Pay",
                            amount = 1,
                            after = {
                                {
                                    op = "Destroy",
                                    target = "selected"
                                }
                            }
                        }
                    }
                end,
                oncePerTurn = true,
                condition = function(ctx)
                    return hasContract(ctx, "轩辕") and ctx.opponentClock == "black" and ctx.event.reason ~= "battle"
                end
            }
        }
    }
}
