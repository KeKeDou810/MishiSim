

return{
    id = "HZ01-025-R",
    rulesId = "HZ01-025",
    effectText = "【自】这个时魔被放置到卡组底时（→）将这张卡加入手牌。",
    name = "召幻魔女 初阶白魔女",
    faction = "白银草原",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "召幻兽",
    artworkPath = "Artwork/HZ01-025-R.png",
    effects = {
        triggers = {
            {
                id = "bottom_return",
                label = "bottom_return",
                event = "DeckPositioned",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Move",
                            target = "self",
                            zone = "Hand"
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
