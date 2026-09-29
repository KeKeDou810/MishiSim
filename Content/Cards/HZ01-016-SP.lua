

return{
    id = "HZ01-016-SP",
    rulesId = "HZ01-016",
    clock = "black",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【自】（回合1）（黑时钟）这个时魔进行攻击结束时（→）若对方持有（白时钟），[支付时间3，舍弃2张手牌]，将这张卡重构。
【起】（回合1）（任意圆阵）（黑时钟）若对方持有（黑时钟），[选择场上的1个你的时魔破坏]，选择场上的1个对方的时间3以下的通常时魔破坏。
【契约自】这个时魔进行未来视时（→）若公开卡为“维斯王朝”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "轩辕龙",
    faction = "维斯王朝",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "圣光龙",
    artworkPath = "Artwork/HZ01-016-SP.png",
    playerCards = {"HZ01-041B-C", "HZ01-039A-C"},
    effects = {
        attackTimeLimit = 4,
        oncePerTurn = true,
        canActivate = function(ctx)
            return ctx.clock == "black" and ctx.opponentClock == "black"
        end,
        onActivate = function(ctx)
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "opponent",
                    type = "通常时魔",
                    maxTime = 3
                },
                {
                    op = "Destroy",
                    target = "selected"
                }
            }
        end,
        triggers = {
            {
                id = "foresight_damage",
                label = "foresight_damage",
                event = "ForesightRevealed",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "ModifyBattleDamage",
                            amount = 1
                        }
                    }
                end,
                oncePerTurn = false,
                oncePerNamePerTurn = false,
                condition = function(ctx)
                    return ctx.event.revealed.type == "决策卡" and ctx.event.revealed.faction == "维斯王朝"
                end
            },
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
                    return ctx.event.attacker and ctx.event.attacker.instanceId == ctx.instanceId and ctx.clock == "black" and ctx.opponentClock == "white"
                end,
                costs = {
                    {
                        kind = "Time",
                        amount = 3
                    },
                    {
                        kind = "Discard",
                        amount = 2
                    }
                }
            }
        },
        activationCosts = {
            {
                kind = "Destroy",
                zone = "Board",
                amount = 1
            }
        }
    }
}
