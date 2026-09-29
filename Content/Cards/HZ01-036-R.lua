

return{
    id = "HZ01-036-R",
    rulesId = "HZ01-036",
    effectText = "【自】这个时魔登场时（→）以背面选择对方的1张手牌，将那张卡重叠到这张卡下方作为超频源，对方抽1张卡。",
    name = "幽灵女仆 丽诺尔",
    faction = "潘多拉",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "幽灵",
    artworkPath = "Artwork/HZ01-036-R.png",
    effects = {
        onSummon = function(ctx)
            return {
                {
                    op = "ChooseHiddenHand",
                    storeAs = "captured"
                },
                {
                    op = "AttachUnder",
                    target = "self",
                    set = "captured"
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
