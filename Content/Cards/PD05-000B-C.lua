-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "PD05-000B-C",
    rulesId = "PD05-000B",
    effectText = "【自】（回合1）你的时魔被攻击时（→）若场上或契约区存在名字含有“梵祢莉娅”的你的契约时魔，[支付时间3]，以背面选择对方1张手牌舍弃，再选择1个时间在被舍弃的卡的时间以下的时魔破坏。",
    name = "玩家",
    faction = "潘多拉",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/PD05-000B-C.png",
    effects = {
        triggers = {
            {
                id = "hidden_discard_destroy",
                label = "hidden_discard_destroy",
                event = "AttackDeclared",
                listen = {
                    side = "opponent"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "ChooseHiddenHand",
                            storeAs = "discarded"
                        },
                        {
                            op = "Discard",
                            target = "set",
                            set = "discarded"
                        },
                        {
                            op = "Continue",
                            callback = "afterDiscard"
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return ctx.event.target ~= nil and ctx.event.target.owner == ctx.owner and hasContract(ctx, "梵祢莉娅") and (true)
                end,
                costs = {
                    {
                        kind = "Time",
                        amount = 3
                    }
                }
            }
        },
        afterDiscard = function(ctx)
            local card = ctx.sets.discarded[1]
            if not card then return {} end
            return { { op = "Choose", zone = "Board", side = "any", types = {"通常时魔", "契约时魔", "衍生物"}, maxTime = card.time }, { op = "Destroy", target = "selected" } }
        end
    }
}
