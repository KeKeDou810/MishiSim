

return{
    id = "HZ01-034-R",
    rulesId = "HZ01-034",
    effectText = [[【自】这个时魔登场时（→）选择场上1个名字含有“异兽”的你的时魔，这个回合中，那个时魔获得：“【自】（回合1）这个时魔进行攻击结束时（→）[支付时间2]，将这张卡重构。”。
【自】这个时魔进行攻击结束时（必须发动）（→）将这张卡放置到弃牌区，选择这个回合中被除外的1张名字含有“异兽”的时魔卡，加入手牌。]],
    name = "牛魔异兽 组合体",
    faction = "潘多拉",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "异兽",
    artworkPath = "Artwork/HZ01-034-R.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    },
                    name = "异兽",
                    nameMatch = "fuzzy"
                },
                {
                    op = "GrantTrigger",
                    target = "selected",
                    key = "BattleEnded",
                    callback = "grantedReady",
                    oncePerTurn = true,
                    costs = {
                        {
                            kind = "Time",
                            amount = 2
                        }
                    }
                }
            }
        end,
        grantedReady = function(ctx)
            if not (ctx.event.attacker and ctx.event.attacker.instanceId == ctx.instanceId) then return {} end
            return {
                {
                    op = "Ready"
                }
            }
        end,
        triggers = {
            {
                id = "return_exiled",
                label = "return_exiled",
                event = "BattleEnded",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Move",
                            target = "self",
                            zone = "Discard"
                        },
                        {
                            op = "Choose",
                            zone = "Exile",
                            side = "any",
                            types = {
                                "通常时魔",
                                "契约时魔"
                            },
                            name = "异兽",
                            nameMatch = "fuzzy",
                            enteredThisTurn = true
                        },
                        {
                            op = "Move",
                            target = "selected",
                            zone = "Hand"
                        }
                    }
                end,
                condition = function(ctx)
                    return ctx.event.attacker and ctx.event.attacker.instanceId == ctx.instanceId
                end
            }
        }
    }
}
