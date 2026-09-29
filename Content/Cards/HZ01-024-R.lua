return{
    id = "HZ01-024-R",
    rulesId = "HZ01-024",
    clock = "white",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（白时钟）[支付时间1或1以上任意数值]，公开卡组顶1张卡，若那张卡是持有支付时间数值以下时间的时魔卡，将那个时魔登场到不存在时魔的圆阵上，回合结束阶段将那个时魔放置到卡组底。若不是，将公开的卡放置到卡组底，抽1张卡。
【契约自】这个时魔进行未来视时（→）若公开卡为“白银草原”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "精灵召幻师 维克多莉娅",
    faction = "白银草原",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "精灵",
    artworkPath = "Artwork/HZ01-024-R.png",
    playerCards = {"HZ01-053B-C", "HZ01-051A-C"},
    effects = {
        oncePerTurn = true,
        activationCosts = { { kind = "Time", min = 1, max = 11, storeAs = "paidTime" } },
        canActivate = function(ctx)
            return ctx.clock == "white" and ctx.emptyBoardCount > 0
        end,
        onActivate = function(ctx)
            return { { op = "Scry", amount = 1, reveal = true, after = "afterSummonLook" } }
        end,
        afterSummonLook = function(ctx)
            local card = ctx.scry[1]
            if card == nil then return {} end
            local paid = ctx.vars.effect.paidTime or 0
            if (card.type == "通常时魔" or card.type == "契约时魔") and card.time <= paid then
                return {
                    { op = "Summon", target = "scry" },
                    { op = "Schedule", target = "scry", timing = "turnEnd", after = {
                        { op = "ReturnToDeck", target = "set", set = "scheduled", position = "bottom" }
                    } }
                }
            end
            return {
                { op = "ReturnToDeck", target = "scry", position = "bottom" },
                { op = "Draw", amount = 1 }
            }
        end,
        triggers = {
            {
                id = "foresight_damage",
                oncePerTurn = false,
                oncePerNamePerTurn = false,
                label = "未来视：本次玩家伤害 +1",
                event = "ForesightRevealed",
                listen = { subject = "self" },
                condition = function(ctx)
                    return ctx.event.revealed.type == "决策卡"
                        and ctx.event.revealed.faction == "白银草原"
                end,
                onTrigger = function(ctx)
                    return { { op = "ModifyBattleDamage", amount = 1 } }
                end
            }
        },
        attackTimeLimit = 4
    }
}
