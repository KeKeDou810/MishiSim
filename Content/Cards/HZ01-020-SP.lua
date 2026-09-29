return{
    id = "HZ01-020-SP",
    rulesId = "HZ01-020",
    clock = "black",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（黑时钟）[支付时间1]，这个回合中，这个时魔的攻击距离+1。然后将玩家卡放置到这张卡下。
【契约自】这个时魔进行未来视时（→）若公开卡为“星河联盟”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "超星机甲·裂星装术",
    faction = "星河联盟",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "银河机甲",
    artworkPath = "Artwork/HZ01-020-SP.png",
    playerCards = {"HZ01-047B-C", "HZ01-045A-C"},
    effects = {
        oncePerTurn = true,
        activationCosts = { { kind = "Time", amount = 1 } },
        canActivate = function(ctx)
            return ctx.clock == "black"
        end,
        onActivate = function(ctx)
            return {
                { op = "Modify", stat = "range", amount = 1, duration = "turn" },
                { op = "AttachPlayers" }
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
                        and ctx.event.revealed.faction == "星河联盟"
                end,
                onTrigger = function(ctx)
                    return { { op = "ModifyBattleDamage", amount = 1 } }
                end
            }
        },
        attackTimeLimit = 4
    }
}
