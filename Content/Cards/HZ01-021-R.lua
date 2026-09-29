-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-021-R",
    rulesId = "HZ01-021",
    effectText = "【自】这个时魔登场时（→）若场上或契约区存在名字含有“超星机甲”的你的契约时魔，查看卡组底的1张卡，放置到卡组底，选择场上的1个你的时魔，这个回合中，力量+1000。",
    name = "银河小队的调查员 Dr.伦",
    faction = "星河联盟",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "星河兵",
    artworkPath = "Artwork/HZ01-021-R.png",
    effects = {
        onSummon = function(ctx)
            if not (hasContract(ctx, "超星机甲")) then return {} end
            return {
                {
                    op = "Scry",
                    amount = 1,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "ReturnToDeck",
                            target = "scry",
                            position = "bottom"
                        },
                        {
                            op = "Choose",
                            zone = "Board",
                            side = "own",
                            types = {
                                "通常时魔",
                                "契约时魔",
                                "衍生物"
                            }
                        },
                        {
                            op = "Modify",
                            target = "selected",
                            stat = "power",
                            amount = 1000,
                            duration = "turn"
                        }
                    },
                    from = "bottom"
                }
            }
        end
    }
}
