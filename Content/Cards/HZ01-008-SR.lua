

return{
    id = "HZ01-008-SR",
    rulesId = "HZ01-008",
    effectText = "【自】这张卡被从卡组底加入手牌时（→）将这张卡登场到不存在时魔的圆阵上。这个回合中，不能再发动与这张卡同名的卡的效果。",
    name = "银河小队的新人 库洛伊",
    faction = "星河联盟",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/HZ01-008-SR.png",
    effects = {
        triggers = {
            {
                id = "bottom_to_hand",
                label = "bottom_to_hand",
                event = "CardMoved",
                listen = {
                    subject = "self",
                    from = "Deck",
                    to = "Hand"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Summon"
                        },
                        {
                            op = "BlockName",
                            name = "银河小队的新人 库洛伊"
                        }
                    }
                end,
                activeZone = "Hand",
                condition = function(ctx)
                    return ctx.event.position == "bottom"
                end
            }
        }
    }
}
