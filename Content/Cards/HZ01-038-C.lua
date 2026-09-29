

return{
    id = "HZ01-038-C",
    rulesId = "HZ01-038",
    effectText = "【自】这个时魔登场时（→）选择这张卡以外的场上的1个你的时魔，这个回合中，那个时魔的力量+3000。",
    name = "暗之龙巫女 小静",
    faction = "维斯王朝",
    level = 5,
    power = 6000,
    sign = "未来视",
    type = "通常时魔",
    race = "龙巫女",
    artworkPath = "Artwork/HZ01-038-C.png",
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
