return{
    id = "PD04-008-C",
    rulesId = "PD04-008",
    effectText = "【自】这个时魔通过支付时间的方式从手牌登场时（→）选择弃牌区的1张决策卡，投掷1个骰子，如果是偶数，[支付被选择卡的时间，舍弃1张手牌]，发动那张卡的决策卡效果。如果是奇数，将被选择的卡放回卡组洗切。",
    name = "社团副团长 中野零子",
    faction = "绿洲",
    level = 4,
    power = 5000,
    sign = "未来视",
    type = "通常时魔",
    race = "人类",
    artworkPath = "Artwork/PD04-008-C.png"
,
    effects = {
        triggers = {
            {
                id = "dice_decision",
                event = "Summoned",
                listen = { subject = "self", from = "Hand" },
                condition = function(ctx)
                    return ctx.event.reason == "paidHand" or ctx.event.reason == "overclock"
                end,
                onTrigger = function(ctx)
                    return {
                        { op = "Choose", zone = "Discard", type = "决策卡", storeAs = "decision", prompt = "选择要发动效果的决策卡" },
                        { op = "Continue", callback = "rollForDecision" }
                    }
                end
            }
        },
        rollForDecision = function(ctx)
            if #ctx.sets.decision == 0 then
                return {}
            end
            return {
                { op = "RollDice", amount = 6, storeAs = "die", reveal = true },
                { op = "Continue", callback = "afterDice" }
            }
        end,
        afterDice = function(ctx)
            local card = ctx.sets.decision[1]
            if ctx.vars.effect.die % 2 == 0 then
                return {
                    { op = "Pay", amount = card.time,
                      costs = { { kind = "Discard", amount = 1 } },
                      after = { { op = "InvokeDecision", target = "set", set = "decision" } } }
                }
            end
            return {
                { op = "ReturnToDeck", target = "set", set = "decision", position = "top" },
                { op = "Shuffle" }
            }
        end
    }
}
