

return{
    id = "HZ01-003-USR",
    rulesId = "HZ01-003",
    effectText = "【自】这个时魔登场时，或被放置到卡组底时（→）抽1张卡。",
    name = "召幻机甲 STC-01",
    faction = "白银草原",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "召幻兽",
    artworkPath = "Artwork/HZ01-003-USR.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Draw",
                    amount = 1
                }
            }
        end,
        triggers = {
            {
                id = "bottom_draw",
                label = "bottom_draw",
                event = "DeckPositioned",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Draw",
                            amount = 1
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
