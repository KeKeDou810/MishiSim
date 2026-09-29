-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-006-SR",
    rulesId = "HZ01-006",
    effectText = [[【永】仅当场上或契约区存在名字含有“轩辕”的你的契约时魔时，这个时魔才能发动效果。
【自】这个时魔进行攻击结束时（→）若对方持有（白时钟），[支付时间1]，将这个时魔重构，回合结束阶段将这个时魔破坏。
【自】你的回合中这个时魔被效果破坏时（→）若对方持有（黑时钟），[支付时间1]，将这个时魔登场到不存在时魔的你的防御圆阵上，这个回合中，这个时魔无法再被你的效果选择。]],
    name = "风之龙巫女 风玥",
    faction = "维斯王朝",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "龙巫女",
    artworkPath = "Artwork/HZ01-006-SR.png",
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
                        },
                        {
                            op = "Schedule",
                            target = "self",
                            timing = "turnEnd",
                            after = {
                                {
                                    op = "Destroy",
                                    target = "set",
                                    set = "scheduled"
                                }
                            }
                        }
                    }
                end,
                condition = function(ctx)
                    return hasContract(ctx, "轩辕") and ctx.event.attacker and ctx.event.attacker.instanceId == ctx.instanceId and ctx.opponentClock == "white"
                end,
                costs = {
                    {
                        kind = "Time",
                        amount = 1
                    }
                }
            },
            {
                id = "return_defense",
                label = "return_defense",
                event = "Destroyed",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "RememberCards",
                            target = "event",
                            storeAs = "returning"
                        },
                        {
                            op = "Summon",
                            target = "set",
                            set = "returning",
                            placement = "defense"
                        },
                        {
                            op = "ProtectSelection",
                            target = "set",
                            set = "returning",
                            side = "own"
                        }
                    }
                end,
                condition = function(ctx)
                    return hasContract(ctx, "轩辕") and ctx.owner == ctx.activePlayer and ctx.opponentClock == "black" and ctx.event.reason ~= "battle"
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
