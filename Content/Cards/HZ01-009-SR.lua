-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-009-SR",
    rulesId = "HZ01-009",
    effectText = "【自】这个时魔登场时（→）若场上或契约区存在名字含有“超星机甲”的你的契约时魔，宣言1个卡名，公开卡组底的1张卡，若是宣言的卡，这个回合中，这张卡的攻击距离+1。然后可以将宣言的卡加入手牌。若不是，将公开的卡舍弃，将这张卡横置。",
    name = "银河小队的机械师 史蒂芬妮",
    faction = "星河联盟",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/HZ01-009-SR.png",
    effects = {
        onSummon = function(ctx)
            if not (hasContract(ctx, "超星机甲")) then return {} end
            return {
                {
                    op = "DeclareCardName",
                    storeAs = "guess"
                },
                {
                    op = "Scry",
                    amount = 1,
                    storeAs = "viewed",
                    after = "checkGuess",
                    from = "bottom",
                    reveal = true
                }
            }
        end,
        checkGuess = function(ctx)
            local card = ctx.scry[1]
            if not card then return {} end
            if card.name == ctx.vars.effect.guess then
                return {
                    { op = "Modify", stat = "range", amount = 1 },
                    { op = "Choose", target = "scry", minCount = 0, storeAs = "picked", prompt = "是否加入宣言的卡？" },
                    { op = "Move", target = "set", set = "picked", zone = "Hand" }
                }
            end
            return { { op = "Move", target = "scry", zone = "Discard" }, { op = "Tap" } }
        end
    }
}
