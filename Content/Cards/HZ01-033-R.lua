

return{
    id = "HZ01-033-R",
    rulesId = "HZ01-033",
    effectText = "【自】这个时魔登场时（→）[舍弃最多2张手牌]，每舍弃1张抽1张卡。",
    name = "蜂毒异兽 成体",
    faction = "潘多拉",
    level = 3,
    power = 4000,
    sign = "未来视",
    type = "通常时魔",
    race = "异兽",
    artworkPath = "Artwork/HZ01-033-R.png",
    effects = {
        triggers = {
            {
                id = "discard_draw",
                label = "discard_draw",
                event = "Summoned",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Draw",
                            amount = { var = "discarded", scope = "effect" }
                        }
                    }
                end,
                costs = {
                    {
                        kind = "Discard",
                        min = 0,
                        max = 2,
                        storeAs = "discarded"
                    }
                }
            }
        }
    }
}
