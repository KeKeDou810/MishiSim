return{
    id = "PD04-000C-C",
    rulesId = "PD04-000A",
    effectText = "【起】支付[时间3]，将你的契约区中的1张等级0的契约时魔登场到不存在时魔的圆阵上。",
    name = "玩家",
    faction = "绿洲",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/PD04-000C-C.png",
    effects = {
        activateZone = "Player",
        activationCosts = { { kind = "Time", amount = 3 } },
        canActivate = function(ctx)
            return ctx.contractZoneCount > 0 and ctx.emptyBoardCount > 0
        end,
        onActivate = function(ctx)
            return {
                { op = "Choose", zone = "Contract", type = "契约时魔", maxTime = 0, prompt = "选择复活的契约时魔" },
                { op = "Summon", target = "selected" }
            }
        end
    }
}
