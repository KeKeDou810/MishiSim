

return{
    id = "PD05-001-USR",
    rulesId = "PD05-001",
    clock = "black",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（黑时钟）以背面选择对方的1张手牌舍弃，对方抽1张卡。
【契约自】这个时魔进行未来视时（→）若公开卡为“潘多拉”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "冥途的夜神 梵祢莉娅",
    faction = "潘多拉",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "堕天使",
    artworkPath = "Artwork/PD05-001-USR.png",
    playerCards = {"PD05-000B-C", "PD05-000A-C"},
    effects = {
        attackTimeLimit = 4,
        oncePerTurn = true,
        canActivate = function(ctx)
            return ctx.clock == "black"
        end,
        onActivate = function(ctx)
            return {
                {
                    op = "ChooseHiddenHand"
                },
                {
                    op = "Discard",
                    target = "selected"
                },
                {
                    op = "Draw",
                    side = "opponent",
                    amount = 1
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
                    return ctx.event.revealed.type == "决策卡" and ctx.event.revealed.faction == "潘多拉"
                end
            }
        }
    }
}
