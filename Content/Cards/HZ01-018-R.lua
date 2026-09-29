-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-018-R",
    rulesId = "HZ01-018",
    effectText = [[【永】仅当场上或契约区存在名字含有“轩辕”的你的契约时魔时，这个时魔才能发动效果。
【自】（回合1）这个时魔进行攻击结束时（→）若对方持有（白时钟），[支付时间1]，到下个回合结束为止这张卡的力量变为2000，时间变为1，将这张卡重构。
【起】（回合1）（任意圆阵）若对方持有（黑时钟），[支付时间3，选择场上的1个自己的时魔破坏]，选择场上的1个对方的时魔破坏。]],
    name = "水龙骑士 沐泽",
    faction = "维斯王朝",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "龙骑士",
    artworkPath = "Artwork/HZ01-018-R.png",
    effects = {
        oncePerTurn = true,
        canActivate = function(ctx)
            return hasContract(ctx, "轩辕") and ctx.opponentClock == "black"
        end,
        activationCosts = {
            {
                kind = "Time",
                amount = 3
            },
            {
                kind = "Destroy",
                amount = 1
            }
        },
        onActivate = function(ctx)
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "opponent",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                {
                    op = "Destroy",
                    target = "selected"
                }
            }
        end,
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
                            op = "Modify",
                            target = "self",
                            stat = "power",
                            amount = 2000,
                            duration = "nextTurn",
                            mode = "set"
                        },
                        {
                            op = "Modify",
                            target = "self",
                            stat = "time",
                            amount = 1,
                            duration = "nextTurn",
                            mode = "set"
                        },
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
                        amount = 1
                    }
                }
            }
        }
    }
}
