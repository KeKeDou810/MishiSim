-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-019-SP",
    rulesId = "HZ01-019",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“轩辕”的你的契约时魔，根据对手持有的时钟发动以下效果：
（白时钟）：抽2张卡。选择场上的1个你的时魔，到下个回合结束为止，力量+1000。
（黑时钟）：选择场上的1个你的时魔和对方的1个时间2以下的通常时魔破坏。若未破坏，抽2张卡。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "元素之力",
    faction = "维斯王朝",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/HZ01-019-SP.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not hasContract(ctx, "轩辕") then return {} end
            if ctx.opponentClock == "white" then
                return {
                {
                    op = "Draw",
                    amount = 2
                },
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
                    amount = 1000,
                    duration = "nextTurn"
                }
            }
            end
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    },
                    storeAs = "ally"
                },
                {
                    op = "Choose",
                    zone = "Board",
                    side = "opponent",
                    type = "通常时魔",
                    maxTime = 2,
                    storeAs = "enemy"
                },
                {
                    op = "Continue",
                    callback = "destroyPair"
                }
            }
        end,
        destroyPair = function(ctx)
            if #ctx.sets.ally == 0 or #ctx.sets.enemy == 0 then return { { op = "Draw", amount = 2 } } end
            return {
                { op = "Destroy", target = "set", set = "ally" },
                { op = "Destroy", target = "set", set = "enemy" },
                { op = "Continue", callback = "afterPair" }
            }
        end,
        afterPair = function(ctx)
            for _, set in ipairs({ctx.sets.ally, ctx.sets.enemy}) do
                for _, card in ipairs(set) do
                    if card.zone == "Board" then return { { op = "Draw", amount = 2 } } end
                end
            end
            return {}
        end
    }
}
