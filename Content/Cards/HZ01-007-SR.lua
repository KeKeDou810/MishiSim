-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-007-SR",
    rulesId = "HZ01-007",
    effectText = [[【永】仅当场上或契约区存在名字含有“轩辕”的你的契约时魔时，这个时魔才能发动效果。
【自】（手牌）这个时魔被作为费用舍弃时（→）若对方持有（白时钟），抽1张卡。
【自】（弃牌区）你的回合中你的时魔被效果破坏时（→）若对方持有（黑时钟），[支付时间2]，将这个时魔登场到不存在时魔的你的玩家圆阵上，这个回合中，不能再发动与这张卡同名的卡的效果。]],
    name = "水之龙巫女 清涵",
    faction = "维斯王朝",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "龙巫女",
    artworkPath = "Artwork/HZ01-007-SR.png",
    effects = {
        triggers = {
            {
                id = "discard_cost_draw",
                label = "discard_cost_draw",
                event = "Discarded",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Draw",
                            amount = 1
                        }
                    }
                end,
                activeZone = "Hand",
                condition = function(ctx)
                    return hasContract(ctx, "轩辕") and ctx.opponentClock == "white" and ctx.event.reason == "payment"
                end
            },
            {
                id = "revive_player",
                label = "revive_player",
                event = "Destroyed",
                listen = {
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Summon",
                            placement = "player"
                        },
                        {
                            op = "BlockName",
                            name = "水之龙巫女 清涵"
                        }
                    }
                end,
                activeZone = "Discard",
                condition = function(ctx)
                    return hasContract(ctx, "轩辕") and ctx.owner == ctx.activePlayer and ctx.opponentClock == "black" and ctx.event.reason ~= "battle"
                end,
                costs = {
                    {
                        kind = "Time",
                        amount = 2
                    }
                }
            }
        }
    }
}
