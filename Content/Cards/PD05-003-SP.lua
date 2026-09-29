

return{
    id = "PD05-003-SP",
    rulesId = "PD05-003",
    effectText = "【自】这个时魔被战斗破坏时（→）[支付时间2]，这个回合结束时，将把这个时魔破坏的对方时魔破坏。",
    name = "冒失鬼 月月",
    faction = "潘多拉",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "恶魔",
    artworkPath = "Artwork/PD05-003-SP.png",
    effects = {
        triggers = {
            {
                id = "destroy_attacker_later",
                label = "destroy_attacker_later",
                event = "Destroyed",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Schedule",
                            target = "cause",
                            timing = "turnEnd",
                            after = {
                                {
                                    op = "Destroy",
                                    target = "set",
                                    set = "scheduled"
                                }
                            }
                        }
                    }
                end,
                condition = function(ctx)
                    return ctx.event.reason == "battle"
                end,
                costs = {
                    {
                        kind = "Time",
                        amount = 2
                    }
                }
            }
        }
    }
}
