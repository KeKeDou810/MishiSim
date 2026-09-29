-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-022-R",
    rulesId = "HZ01-022",
    effectText = "【自】你的回合中，你的时魔通过战斗破坏对方时魔时（→）若场上或契约区存在名字含有“超星机甲”的你的契约时魔，这个回合中，这张卡的力量+1000，攻击距离+1。",
    name = "银河小队 诺瓦·指挥官",
    faction = "星河联盟",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/HZ01-022-R.png",
    effects = {
        triggers = {
            {
                id = "battle_victory",
                label = "battle_victory",
                event = "Destroyed",
                listen = {
                    side = "opponent",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Modify",
                            target = "self",
                            stat = "power",
                            amount = 1000,
                            duration = "turn"
                        },
                        {
                            op = "Modify",
                            target = "self",
                            stat = "range",
                            amount = 1,
                            duration = "turn"
                        }
                    }
                end,
                condition = function(ctx)
                    return ctx.owner == ctx.activePlayer and hasContract(ctx, "超星机甲") and ctx.event.reason == "battle" and ctx.event.source and ctx.event.source.owner == ctx.owner
                end
            }
        }
    }
}
