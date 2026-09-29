

return{
    id = "HZ01-015-SR",
    rulesId = "HZ01-015",
    effectText = "【自】这个时魔登场时（→）选择你的弃牌区中的1张名字含有“异兽”的时魔卡，加入手牌。",
    name = "水生异兽 成体",
    faction = "潘多拉",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "异兽",
    artworkPath = "Artwork/HZ01-015-SR.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "Choose",
                    zone = "Discard",
                    types = {
                        "通常时魔",
                        "契约时魔"
                    },
                    name = "异兽",
                    nameMatch = "fuzzy"
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
