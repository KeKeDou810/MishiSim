return{
    id = "HZ01-068-C",
    rulesId = "HZ01-068",
    effectText = [[【自】这个时魔登场时（→）查看卡组顶5张卡，将至多1张时间4及以上的时魔卡公开后加入手牌，将剩余的卡舍弃。
【特效标记（特殊标记）】：选择场上1个时魔，直到下个对方的回合结束前，那个时魔无法被对方的时魔效果选择。]],
    name = "挖矿滚地龙",
    faction = "不明",
    level = 2,
    power = 3000,
    sign = "特效标记（特殊标记）",
    type = "通常时魔",
    race = "矿龙",
    artworkPath = "Artwork/HZ01-068-C.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Scry",
                    amount = 5,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "Choose",
                            target = "set",
                            set = "viewed",
                            types = { "通常时魔", "契约时魔" },
                            minTime = 4,
                            minCount = 0,
                            maxCount = 1,
                            storeAs = "picked",
                            prompt = "挖矿滚地龙：可选一张时间4及以上的时魔加入手牌"
                        },
                        { op = "Reveal", target = "set", set = "picked" },
                        { op = "Move", target = "set", set = "picked", zone = "Hand" },
                        { op = "Move", target = "set", set = "viewed", except = "picked", zone = "Discard" }
                    }
                }
            }
        end,
        onForesight = function(ctx)
            return {
                { op = "Choose", zone = "Board", side = "any", prompt = "挖矿滚地龙：选择一个时魔保护" },
                { op = "ProtectFromEnemyUnitEffects", target = "selected" }
            }
        end
    }
}
