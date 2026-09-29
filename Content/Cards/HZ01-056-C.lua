

return{
    id = "HZ01-056-C",
    rulesId = "HZ01-056",
    effectText = "【自】这个时魔登场时（→）选择这张卡以外的场上的1个你的时魔，这个回合中，那个时魔的力量+3000。",
    name = "炽天使 路西法",
    faction = "绿洲",
    level = 5,
    power = 6000,
    sign = "未来视",
    type = "通常时魔",
    race = "天使",
    artworkPath = "Artwork/HZ01-056-C.png",
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
