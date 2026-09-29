-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-041B-C",
    rulesId = "HZ01-041B",
    effectText = [[【自】（回合1）名字含有“轩辕”的你的契约时魔进行攻击结束时（→）若对方持有（白时钟），且这个回合中你的契约时魔被卡的效果重构过，[支付时间2，舍弃2张手牌]，将你的契约时魔重构。
【自】（回合1）你的时魔被战斗破坏时（→）若场上或契约区存在名字含有“轩辕”的你的契约时魔，且对方持有（黑时钟），将那次破坏视为效果破坏。]],
    name = "玩家",
    faction = "维斯王朝",
    level = 0,
    power = 0,
    sign = "",
    type = "玩家卡",
    race = "",
    artworkPath = "Artwork/HZ01-041B-C.png",
    effects = {
        triggers = {
            {
                id = "contract_ready",
                label = "contract_ready",
                event = "BattleEnded",
                listen = {
                    side = "own"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Ready",
                            target = "attacker"
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                costs = {
                    {
                        kind = "Time",
                        amount = 2
                    },
                    {
                        kind = "Discard",
                        amount = 2
                    }
                },
                condition = function(ctx)
                    local a = ctx.event.attacker
                    if not a or not a.name:find("轩辕", 1, true) or ctx.opponentClock ~= "white" then return false end
                    for _, card in ipairs(ctx.publicCards) do
                        if card.instanceId == a.instanceId then return card.lastRebuiltTurn == ctx.turn end
                    end
                    return false
                end
            },
            {
                id = "effect_destruction",
                label = "effect_destruction",
                event = "DestructionPending",
                listen = {
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "ChangeDestructionReason",
                            from = "effect"
                        }
                    }
                end,
                activeZone = "Player",
                oncePerTurn = true,
                condition = function(ctx)
                    return hasContract(ctx, "轩辕") and ctx.opponentClock == "black" and ctx.event.reason == "battle"
                end
            }
        }
    }
}
