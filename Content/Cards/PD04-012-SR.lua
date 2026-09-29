-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD04-012-SR",
    rulesId = "PD04-012",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“优尔”的你的契约时魔，抽2张卡，查看卡组顶3张卡，选择1张决策卡加入手牌，将剩余的卡舍弃。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "IN TO THE MOOL！！",
    faction = "绿洲",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/PD04-012-SR.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "优尔")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 2
                },
                {
                    op = "Scry",
                    amount = 3,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "Choose",
                            target = "scry",
                            type = "决策卡",
                            storeAs = "picked"
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
