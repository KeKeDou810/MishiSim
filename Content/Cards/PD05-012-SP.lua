-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD05-012-SP",
    rulesId = "PD05-012",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“梵祢莉娅”的你的契约时魔，抽2张卡，以背面选择对方的1张手牌舍弃，对方抽1张卡。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "研究：神谕解构",
    faction = "潘多拉",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/PD05-012-SP.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "梵祢莉娅")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 2
                },
                {
                    op = "ChooseHiddenHand"
                },
                {
                    op = "Discard",
                    target = "selected"
                },
                {
                    op = "Draw",
                    side = "opponent",
                    amount = 1
                }
            }
        end
    }
}
