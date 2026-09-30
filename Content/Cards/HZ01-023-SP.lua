-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-023-SP",
    rulesId = "HZ01-023",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“超星机甲”的你的契约时魔，抽2张卡，查看卡组底的1张卡，将那张卡放置到卡组底，或公开后加入手牌。若加入手牌，选择1张手牌舍弃。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "赌上明天的研发",
    faction = "星河联盟",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/HZ01-023-SP.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "超星机甲")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 2
                },
                {
                    op = "Scry",
                    amount = 1,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "Choose",
                            target = "scry",
                            minCount = 0,
                            storeAs = "picked"
                        },
                        {
                            op = "Reveal",
                            target = "set",
                            set = "picked"
                        },
                        {
                            op = "Move",
                            target = "set",
                            zone = "Hand",
                            set = "picked"
                        },
                        {
                            op = "Continue",
                            callback = "afterPick"
                        },
                        { op = "ReturnToDeck", target = "set", set = "viewed", except = "picked", position = "bottom" }
                    },
                    from = "bottom"
                }
            }
        end,
        afterPick = function(ctx)
            if not (#ctx.sets.picked > 0) then return {} end
            return {
                {
                    op = "Choose",
                    zone = "Hand"
                },
                {
                    op = "Discard",
                    target = "selected"
                }
            }
        end
    }
}
