return{
    id = "PD01-001-SP",
    rulesId = "PD01-001",
    clock = "white",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（白时钟）抽1张卡，选择1张手牌舍弃。
【契约自】这个时魔进行未来视时（→）若公开卡为“星河联盟”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "远星轨迹的射手 艾莲",
    faction = "星河联盟",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "星河兵",
    artworkPath = "Artwork/PD01-001-SP.png",
    playerCards = {"PD01-000B-C", "PD01-000A-C"},
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
                        and ctx.event.revealed.faction == "星河联盟"
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
            return ctx.clock == "white"
        end,
        onActivate = function(ctx)
            return {
                {
                    op = "Draw",
                    amount = 1
                },
                {
                    op = "Choose",
                    zone = "Hand",
                    prompt = "选择一张手牌舍弃"
                },
                {
                    op = "Discard",
                    target = "selected"
                }
            }
        end
    }
}
