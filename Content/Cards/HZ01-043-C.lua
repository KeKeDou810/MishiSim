

return{
    id = "HZ01-043-C",
    rulesId = "HZ01-043",
    effectText = "【起】（任意圆阵）[将这张卡放置到卡组底]，选择场上的1个你的时魔，这个回合中，那个时魔的力量+2000。",
    name = "银河小队的通讯员 米娅",
    faction = "星河联盟",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/HZ01-043-C.png",
    effects = {
        activationCosts = {
            {
                kind = "ReturnToDeck",
                subject = "self",
                zone = "Board",
                amount = 1,
                name = "银河小队的通讯员 米娅",
                position = "bottom"
            }
        },
        onActivate = function(ctx)
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                {
                    op = "Modify",
                    target = "selected",
                    stat = "power",
                    amount = 2000,
                    duration = "turn"
                }
            }
        end
    }
}
