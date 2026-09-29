

return{
    id = "HZ01-028-R",
    rulesId = "HZ01-028",
    clock = "black",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（黑时钟）查看卡组顶4张卡，将至多1张名字含有“符文”的决策卡公开后加入手牌，将剩余的卡舍弃。
【契约自】这个时魔进行未来视时（→）若公开卡为“绿洲”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "七大天使 乌列尔",
    faction = "绿洲",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "天使",
    artworkPath = "Artwork/HZ01-028-R.png",
    playerCards = {"HZ01-059B-C", "HZ01-057A-C"},
    effects = {
        attackTimeLimit = 4,
        oncePerTurn = true,
        canActivate = function(ctx)
            return ctx.clock == "black"
        end,
        onActivate = function(ctx)
            return {
                {
                    op = "Scry",
                    amount = 4,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "Choose",
                            target = "set",
                            set = "viewed",
                            name = "符文",
                            nameMatch = "fuzzy",
                            type = "决策卡",
                            minCount = 0,
                            maxCount = 1,
                            storeAs = "picked",
                            prompt = "选择要公开并加入手牌的卡"
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
        }
    }
}
