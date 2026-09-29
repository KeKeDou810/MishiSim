local function isUnit(card)
    return card.type == "通常时魔" or card.type == "契约时魔" or card.type == "衍生物"
end

return{
    id = "HZ01-026-R",
    rulesId = "HZ01-026",
    effectText = "【自】这个时魔登场时，或被放置到卡组底时（→）[支付任意时间]，公开卡组顶1张卡，若是持有支付时间数值+2以下的时间的时魔卡，可以将那个时魔登场到不存在时魔的圆阵上。若没有登场，将公开的卡放置到卡组底，这个回合中，你发动这个效果支付的时间每有1，力量+1000。",
    name = "召幻人鱼 琪卡洛斯",
    faction = "白银草原",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "召幻兽",
    artworkPath = "Artwork/HZ01-026-R.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Pay",
                    costs = {
                        {
                            kind = "Time",
                            min = 0,
                            max = 11,
                            storeAs = "paidTime"
                        }
                    },
                    after = {
                        {
                            op = "Scry",
                            amount = 1,
                            storeAs = "viewed",
                            after = "afterLook",
                            reveal = true
                        }
                    }
                }
            }
        end,
        triggers = {
            {
                id = "bottom_summon",
                label = "bottom_summon",
                event = "DeckPositioned",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Pay",
                            costs = {
                                {
                                    kind = "Time",
                                    min = 0,
                                    max = 11,
                                    storeAs = "paidTime"
                                }
                            },
                            after = {
                                {
                                    op = "Scry",
                                    amount = 1,
                                    storeAs = "viewed",
                                    after = "afterLook",
                                    reveal = true
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
        },
        afterLook = function(ctx)
            local card = ctx.scry[1]
            if card and isUnit(card) and card.time <= (ctx.vars.effect.paidTime or 0) + 2 then
                return {
                    { op = "Choose", target = "scry", minCount = 0, storeAs = "summoned", prompt = "是否登场公开的时魔？" },
                    { op = "Summon", target = "set", set = "summoned" },
                    { op = "Continue", callback = "afterSummon" }
                }
            end
            return { { op = "ReturnToDeck", target = "scry", position = "bottom" }, { op = "Modify", stat = "power", amount = (ctx.vars.effect.paidTime or 0) * 1000 } }
        end,
        afterSummon = function(ctx)
            if ctx.scry[1] and ctx.scry[1].zone == "Board" then return {} end
            return { { op = "ReturnToDeck", target = "scry", position = "bottom" }, { op = "Modify", stat = "power", amount = (ctx.vars.effect.paidTime or 0) * 1000 } }
        end
    }
}
