-- Conditions read the current rules snapshot; effect callbacks return kernel instructions.
local function hasContract(ctx, name)
    return string.find(ctx.contractName, name, 1, true) ~= nil
end

return{
    id = "HZ01-027-R",
    rulesId = "HZ01-027",
    effectText = [[这张卡只能在你的回合使用。这张卡被放置到卡组底时，可以不支付时间适用这张卡使用时的效果。若场上或契约区存在名字含有“维克多莉娅”的你的契约时魔，抽2张卡，公开卡组顶1张卡，将那张卡放置到卡组底。
【特效标记（抽卡标记）】：抽1张卡。]],
    name = "召幻仪式",
    faction = "白银草原",
    level = 1,
    power = 0,
    sign = "特效标记（抽卡标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/HZ01-027-R.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            if not (hasContract(ctx, "维克多莉娅")) then return {} end
            return {
                {
                    op = "Draw",
                    amount = 2
                },
                {
                    op = "Scry",
                    amount = 1,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "ReturnToDeck",
                            target = "scry",
                            position = "bottom"
                        }
                    },
                    reveal = true
                }
            }
        end,
        triggers = {
            {
                id = "bottom_ritual",
                label = "bottom_ritual",
                event = "DeckPositioned",
                listen = {
                    subject = "self"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "Draw",
                            amount = 2
                        },
                        {
                            op = "Scry",
                            amount = 1,
                            storeAs = "viewed",
                            after = {
                                {
                                    op = "ReturnToDeck",
                                    target = "scry",
                                    position = "bottom"
                                }
                            },
                            reveal = true
                        }
                    }
                end,
                activeZone = "Deck",
                condition = function(ctx)
                    return ctx.event.position == "bottom" and hasContract(ctx, "维克多莉娅")
                end
            }
        }
    }
}
