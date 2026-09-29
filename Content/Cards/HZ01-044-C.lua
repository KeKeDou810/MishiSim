

return{
    id = "HZ01-044-C",
    rulesId = "HZ01-044",
    effectText = "【自】这个时魔登场时（→）选择这张卡以外的场上的1个你的时魔，这个回合中，那个时魔的力量+3000。",
    name = "银河小队的副指挥官 安丽莉雅",
    faction = "星河联盟",
    level = 5,
    power = 6000,
    sign = "未来视",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/HZ01-044-C.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "RememberCards",
                    target = "self",
                    storeAs = "source"
                },
                {
                    op = "Choose",
                    zone = "Board",
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    },
                    except = "source"
                },
                {
                    op = "Modify",
                    target = "selected",
                    stat = "power",
                    amount = 3000,
                    duration = "turn"
                }
            }
        end
    }
}
