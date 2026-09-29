

return{
    id = "HZ01-004-USR",
    rulesId = "HZ01-004",
    effectText = "【自】这个时魔登场时（→）选择弃牌区中1张名字含有“符文”的决策卡或1张时间2以下的通常时魔，加入手牌。",
    name = "望神的炽天使 卡麦尔",
    faction = "绿洲",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "天使",
    artworkPath = "Artwork/HZ01-004-USR.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "QueryCards",
                    zone = "Discard",
                    type = "决策卡",
                    name = "符文",
                    nameMatch = "fuzzy",
                    storeAs = "recover"
                },
                {
                    op = "QueryCards",
                    zone = "Discard",
                    type = "通常时魔",
                    maxTime = 2,
                    storeAs = "recover",
                    append = true
                },
                {
                    op = "Choose",
                    target = "set",
                    set = "recover"
                },
                {
                    op = "Move",
                    target = "selected",
                    zone = "Hand"
                }
            }
        end
    }
}
