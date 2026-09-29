

return{
    id = "PD04-001-SP",
    rulesId = "PD04-001",
    clock = "white",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（白时钟）查看卡组顶3张卡，将至多1张决策卡公开后加入手牌，如果公开并加入手牌的话，选择1张手牌舍弃。然后将剩余的卡舍弃。
【契约自】这个时魔进行未来视时（→）若公开卡为“绿洲”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "终端ENIAC 优尔·阿克谢",
    faction = "绿洲",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "虚拟角色",
    artworkPath = "Artwork/PD04-001-SP.png",
    playerCards = {"PD04-000B-C", "PD04-000A-C"},
    effects = {
        attackTimeLimit = 4,
        oncePerTurn = true,
        canActivate = function(ctx)
            return ctx.clock == "white"
        end,
        onActivate = function(ctx)
            return {
                {
                    op = "Scry",
                    amount = 3,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "Choose",
                            target = "scry",
                            type = "决策卡",
                            minCount = 0,
                            storeAs = "picked"
                        },
                        {
                            op = "Reveal",
                            target = "set",
                            set = "picked"
                        },
                        {
                            op = "Move",
                            target = "set",
                            zone = "Hand",
                            set = "picked"
                        },
                        {
                            op = "Continue",
                            callback = "discardAfterPick"
                        },
                        {
                            op = "Move",
                            target = "set",
                            zone = "Discard",
                            set = "viewed",
                            except = "picked"
                        }
                    }
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
                    return ctx.event.revealed.type == "决策卡" and ctx.event.revealed.faction == "绿洲"
                end
            }
        },
        discardAfterPick = function(ctx)
            if not (#ctx.sets.picked > 0) then return {} end
            return {
                {
                    op = "Choose",
                    zone = "Hand"
                },
                {
                    op = "Discard",
                    target = "selected"
                }
            }
        end
    }
}
