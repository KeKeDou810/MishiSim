-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD02-012-SP",
    rulesId = "PD02-012",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“莉莉”的你的契约时魔，抽2张卡，将你的费用时间+1。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "青之魔法阵",
    faction = "白银草原",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/PD02-012-SP.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "莉莉")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 2
                },
                {
                    op = "Cost",
                    amount = 1
                }
            }
        end
    }
}
