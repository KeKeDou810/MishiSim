

return{
    id = "HZ01-002-SP",
    rulesId = "HZ01-002",
    effectText = "【自】这个时魔登场时（→）查看卡组底1张卡，将那张卡放置到卡组底或加入手牌。",
    name = "银河小队的机械师 美狄萝丝",
    faction = "星河联盟",
    level = 3,
    power = 4000,
    sign = "未来视",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/HZ01-002-SP.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Scry",
                    amount = 1,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "Choose",
                            target = "set",
                            set = "viewed",
                            minCount = 0,
                            storeAs = "picked",
                            prompt = "加入手牌？不选择则留在卡组底"
                        },
                        {
                            op = "Move",
                            target = "set",
                            zone = "Hand",
                            set = "picked"
                        }
                    },
                    from = "bottom"
                }
            }
        end
    }
}
