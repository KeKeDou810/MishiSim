return{
    id = "PR-002-PR",
    rulesId = "PR-002",
    effectText = [[【自】这个时魔登场时（→）查看卡组顶5张卡，将至多1张时间4及以上的时魔卡公开后加入手牌，将剩余的卡舍弃。
【特效标记（特殊标记）】：选择场上1个时魔，直到下个对方的回合结束前，那个时魔无法被对方的时魔效果选择。]],
    name = "挖矿滚地龙",
    faction = "不明",
    level = 2,
    power = 3000,
    sign = "特效标记（特殊标记）",
    type = "通常时魔",
    race = "矿龙",
    artworkPath = "Artwork/PR-002-PR.png",
    effects = {
        onForesight = function(ctx)
            return {
                { op = "Choose", zone = "Board", side = "any", prompt = "挖矿滚地龙：选择一个时魔保护" },
                { op = "ProtectFromEnemyUnitEffects", target = "selected" }
            }
        end
    }
}
