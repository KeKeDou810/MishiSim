-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-030-R",
    rulesId = "HZ01-030",
    effectText = [[这张卡只能在你的回合使用。若场上或契约区存在名字含有“乌列尔”的你的契约时魔，抽2张卡，将这张卡放置到场外，形成“符文区”。
【永】（符文区）你的回合中，场上所有你的种族为<天使>的时魔力量+300。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "天使的符文",
    faction = "绿洲",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/HZ01-030-R.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "乌列尔")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 2
                },
                {
                    op = "Move",
                    target = "self",
                    zone = "OffField"
                }
            }
        end,
        aura = {
            zone = "OffField",
            race = "天使",
            power = 300,
            ownTurn = true
        }
    }
}
