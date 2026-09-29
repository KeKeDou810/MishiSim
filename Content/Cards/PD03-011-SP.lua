return{
    id = "PD03-011-SP",
    rulesId = "PD03-011",
    effectText = "【自】这个时魔登场时（→）选择1个你的“龙蛋衍生物”，将1个持有与被选择的“龙蛋衍生物”的时间+1的“龙蛋衍生物-飞龙形态”登场到不存在时魔的圆阵上。将被选择的“龙蛋衍生物”消灭。",
    name = "星龙姬 紫晶",
    faction = "维斯王朝",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "龙人",
    artworkPath = "Artwork/PD03-011-SP.png"
,
    effects = {
        onSummon = function(ctx)
            if ctx.emptyBoardCount == 0 then
                return {}
            end
            return {
                { op = "Choose", zone = "OffField", type = "衍生物", name = "龙蛋衍生物", storeAs = "egg", prompt = "选择要转化的龙蛋" },
                { op = "Continue", callback = "hatch" }
            }
        end,
        hatch = function(ctx)
            if #ctx.sets.egg == 0 then
                return {}
            end
            return {
                { op = "SpawnBoard", definitionId = "PD03-T02-C", amount = ctx.sets.egg[1].time + 1 },
                { op = "RemoveToken", target = "set", set = "egg" }
            }
        end
    }
}
