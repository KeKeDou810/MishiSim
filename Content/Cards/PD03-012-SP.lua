-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD03-012-SP",
    rulesId = "PD03-012",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“赤羽”的你的契约时魔，抽2张卡，选择1个名字含有“龙蛋”的衍生物，将其时间+1。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "夜星降龙",
    faction = "维斯王朝",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/PD03-012-SP.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "赤羽")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 2
                },
                {
                    op = "QueryCards",
                    zone = "OffField",
                    side = "any",
                    type = "衍生物",
                    name = "龙蛋",
                    nameMatch = "fuzzy",
                    storeAs = "eggs"
                },
                {
                    op = "QueryCards",
                    zone = "Board",
                    side = "any",
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
    }
}
