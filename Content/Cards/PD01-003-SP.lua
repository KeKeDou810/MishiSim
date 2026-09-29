return{
    id = "PD01-003-SP",
    rulesId = "PD01-003",
    effectText = "【自】这个时魔被战斗破坏时（→）抽1张卡。",
    name = "远星小队的机械师 卡莲娜",
    faction = "星河联盟",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/PD01-003-SP.png",
    effects = {
        onDestroyed = function(ctx)
            if ctx.reason ~= "battle" then
                return {
                }
            end
            return {
                {
                    op = "Draw",
                    amount = 1
                }
            }
        end
    }
}
