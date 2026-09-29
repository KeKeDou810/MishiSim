

return{
    id = "HZ01-037-C",
    rulesId = "HZ01-037",
    effectText = [[【自】这个时魔被战斗破坏时（→）若对方持有（白时钟），抽1张卡。
【自】这个时魔被效果破坏时（→）若对方持有（黑时钟），抽1张卡。]],
    name = "炎龙骑士 宇焰",
    faction = "维斯王朝",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "龙骑士",
    artworkPath = "Artwork/HZ01-037-C.png",
    effects = {
        onDestroyed = function(ctx)
            if not ((ctx.reason == "battle" and ctx.opponentClock == "white") or (ctx.reason ~= "battle" and ctx.opponentClock == "black")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 1
                }
            }
        end
    }
}
