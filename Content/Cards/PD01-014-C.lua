return{
    id = "PD01-014-C",
    rulesId = "PD01-014",
    effectText = [[选择场上的1个时魔，将那个时魔破坏。
【特效标记（炸裂标记）】：选择场上的1个时魔，将那个时魔破坏。]],
    name = "星尘之契，远星之誓",
    faction = "星河联盟",
    level = 3,
    power = 0,
    sign = "特效标记（炸裂标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/PD01-014-C.png",
    effects = {
        playTurn = "either",
        onPlay = function(ctx)
            return {
                {
                    op = "Choose",
                    side = "any",
                    zone = "Board",
                    prompt = "选择要破坏的时魔"
                },
                {
                    op = "Destroy",
                    target = "selected"
                }
            }
        end
    }
}
