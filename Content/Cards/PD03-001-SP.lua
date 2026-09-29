local function hasPublic(ctx, zone, name)
    for _, card in ipairs(ctx.publicCards) do
        if card.owner == ctx.owner and card.zone == zone and string.find(card.name, name, 1, true) then return true end
    end
    return false
end

return{
    id = "PD03-001-SP",
    rulesId = "PD03-001",
    clock = "white",
    effectText = [[【永】这个时魔无法攻击时间5及以上的时魔。
【起】（回合1）（任意圆阵）（白时钟）若你没有“龙蛋衍生物”，则在场外生成1个“龙蛋衍生物”，并将其时间+1。若有，则选择1个你的名字含有“龙蛋”的衍生物，将其时间+1。
【契约自】这个时魔进行未来视时（→）若公开卡为“维斯王朝”的决策卡，这次战斗中，这个时魔给予对方玩家卡的伤害+1。]],
    name = "幻晶之龙骑士 赤羽",
    faction = "维斯王朝",
    level = 0,
    power = 1000,
    sign = "未来视",
    type = "契约时魔",
    race = "龙晶人",
    artworkPath = "Artwork/PD03-001-SP.png",
    playerCards = {"PD03-000B-C", "PD03-000A-C"},
    effects = {
        attackTimeLimit = 4,
        oncePerTurn = true,
        canActivate = function(ctx)
            return ctx.clock == "white"
        end,
        onActivate = function(ctx)
            return {
                {
                    op = "Continue",
                    callback = "egg"
                }
            }
        end,
        triggers = {
            {
                id = "foresight_damage",
                label = "foresight_damage",
                event = "ForesightRevealed",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "ModifyBattleDamage",
                            amount = 1
                        }
                    }
                end,
                oncePerTurn = false,
                oncePerNamePerTurn = false,
                condition = function(ctx)
                    return ctx.event.revealed.type == "决策卡" and ctx.event.revealed.faction == "维斯王朝"
                end
            }
        },
        egg = function(ctx)
            if hasPublic(ctx, "OffField", "龙蛋") or hasPublic(ctx, "Board", "龙蛋") then
                return {
                {
                    op = "QueryCards",
                    zone = "OffField",
                    type = "衍生物",
                    name = "龙蛋",
                    nameMatch = "fuzzy",
                    storeAs = "eggs"
                },
                {
                    op = "QueryCards",
                    zone = "Board",
                    type = "衍生物",
                    name = "龙蛋",
                    nameMatch = "fuzzy",
                    storeAs = "eggs",
                    append = true
                },
                {
                    op = "Choose",
                    target = "set",
                    set = "eggs"
                },
                {
                    op = "Modify",
                    target = "selected",
                    stat = "time",
                    amount = 1,
                    duration = "permanent"
                }
            }
            end
            return { { op = "Spawn", definitionId = "PD03-T01-C", amount = 1 } }
        end
    }
}
