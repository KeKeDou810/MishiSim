

return{
    id = "HZ01-010-SR",
    rulesId = "HZ01-010",
    effectText = "【自】这个时魔被战斗破坏时（→）选择场上的1个你的时魔或对方的1个时间1以下的通常时魔，将那个时魔放置到卡组底。",
    name = "召幻魔女 初阶黑魔女",
    faction = "白银草原",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "召幻兽",
    artworkPath = "Artwork/HZ01-010-SR.png",
    effects = {
        onDestroyed = function(ctx)
            if not (ctx.reason == "battle") then return {} end
            return {
                {
                    op = "QueryCards",
                    zone = "Board",
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    },
                    storeAs = "options"
                },
                {
                    op = "QueryCards",
                    zone = "Board",
                    side = "opponent",
                    type = "通常时魔",
                    maxTime = 1,
                    storeAs = "options",
                    append = true
                },
                {
                    op = "Choose",
                    target = "set",
                    set = "options"
                },
                {
                    op = "ReturnToDeck",
                    target = "selected",
                    position = "bottom"
                }
            }
        end
    }
}
