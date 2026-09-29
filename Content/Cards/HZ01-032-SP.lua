

return{
    id = "HZ01-032-SP",
    rulesId = "HZ01-032",
    clock = "white",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（白时钟）[舍弃1张手牌]，查看卡组顶3张卡，选择至多1张名字含有“异兽”的时魔卡公开，若公开的卡时间小于等于3，则将那个时魔登场到不存在时魔的圆阵上，若大于等于4，则加入手牌。然后将剩余的卡舍弃。
【契约自】这个时魔进行未来视时（→）若公开卡为“潘多拉”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "恐惧异兽·领主",
    faction = "潘多拉",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "异兽",
    artworkPath = "Artwork/HZ01-032-SP.png",
    playerCards = {"HZ01-065B-C", "HZ01-063A-C"},
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
                            types = {
                                "通常时魔",
                                "契约时魔"
                            },
                            name = "异兽",
                            nameMatch = "fuzzy",
                            minCount = 0,
                            storeAs = "picked"
                        },
                        {
                            op = "Reveal",
                            target = "set",
                            set = "picked"
                        },
                        {
                            op = "Continue",
                            callback = "beastBranch"
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
                    return ctx.event.revealed.type == "决策卡" and ctx.event.revealed.faction == "潘多拉"
                end
            }
        },
        activationCosts = {
            {
                kind = "Discard",
                amount = 1
            }
        },
        beastBranch = function(ctx)
            local card = ctx.sets.picked[1]
            local steps = {}
            if card then
                if card.time <= 3 then
                    steps[#steps + 1] = { op = "Summon", target = "set", set = "picked" }
                else
                    steps[#steps + 1] = { op = "Move", target = "set", set = "picked", zone = "Hand" }
                end
            end
            steps[#steps + 1] = { op = "Move", target = "set", set = "viewed", except = "picked", zone = "Discard" }
            return steps
        end
    }
}
