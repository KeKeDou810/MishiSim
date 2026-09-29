return{
    id = "PD03-T02-C",
    rulesId = "PD03-T02",
    effectText = "【永】这个时魔的时间每有1，这张卡的力量+1000。（这个效果最多能让这张卡的力量增加13000）",
    name = "龙蛋衍生物-飞龙形态",
    faction = "维斯王朝",
    level = 0,
    power = 0,
    sign = "未来视",
    type = "衍生物",
    race = "血羽龙",
    artworkPath = "Artwork/PD03-T02-C.png"
,
    effects = {
        continuous = function(ctx)
            return { { id = "time_power", stat = "power", amount = math.min(ctx.time, 13) * 1000 } }
        end
    }
}
