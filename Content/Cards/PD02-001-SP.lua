return{
    id = "PD02-001-SP",
    rulesId = "PD02-001",
    clock = "black",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（黑时钟）将你的费用时间+1。
【契约自】这个时魔进行未来视时（→）若公开卡为“白银草原”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "森隐之足迹 莉莉",
    faction = "白银草原",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "兽人",
    artworkPath = "Artwork/PD02-001-SP.png",
    playerCards = {"PD02-000B-C", "PD02-000A-C"},
    effects = {
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
        activateZone = "Board",
        attackTimeLimit = 4,
        oncePerTurn = true,
        canActivate = function(ctx)
            return ctx.clock == "black"
        end,
        onActivate = function(ctx)
            return {
                {
                    op = "Cost",
                    amount = 1
                }
            }
        end
    }
}
