

return{
    id = "HZ01-049-C",
    rulesId = "HZ01-049",
    effectText = "【自】这个时魔被放置到卡组底时（→）下个回合中，你使用决策卡时需要支付的时间-1。",
    name = "召幻兽人 沫缌",
    faction = "白银草原",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "召幻兽",
    artworkPath = "Artwork/HZ01-049-C.png",
    effects = {
        triggers = {
            {
                id = "bottom_discount",
                label = "bottom_discount",
                event = "DeckPositioned",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Schedule",
                            target = "self",
                            timing = "nextMain",
                            after = {
                                {
                                    op = "ModifyDecisionCost",
                                    amount = -1
                                }
                            }
                        }
                    }
                end,
                activeZone = "Deck",
                condition = function(ctx)
                    return ctx.event.position == "bottom"
                end
            }
        }
    }
}
