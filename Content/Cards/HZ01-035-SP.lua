-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-035-SP",
    rulesId = "HZ01-035",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“异兽”的你的契约时魔，查看卡组顶5张卡，将至多2张名字含有“异兽”的卡公开后加入手牌，将剩余的卡舍弃。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "异兽侵略",
    faction = "潘多拉",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/HZ01-035-SP.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "异兽")) then return {} end
            return {
                {
                    op = "Scry",
                    amount = 5,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "Choose",
                            target = "set",
                            set = "viewed",
                            name = "异兽",
                            nameMatch = "fuzzy",
                            minCount = 0,
                            maxCount = 2,
                            storeAs = "picked",
                            prompt = "选择要公开并加入手牌的卡"
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
                            op = "Move",
                            target = "set",
                            zone = "Discard",
                            set = "viewed",
                            except = "picked"
                        }
                    }
                }
            }
        end
    }
}
