-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD01-012-SR",
    rulesId = "PD01-012",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“艾莲”的你的契约时魔，抽2张卡，查看卡组顶2张卡，将至多1张卡公开后加入手牌，选择1张手牌舍弃，然后将剩余的卡舍弃。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "机龙觉醒",
    faction = "星河联盟",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/PD01-012-SR.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "艾莲")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 2
                },
                {
                    op = "Scry",
                    amount = 2,
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
                            op = "Choose",
                            zone = "Hand"
                        },
                        {
                            op = "Discard",
                            target = "selected"
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
