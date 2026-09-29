

return{
    id = "PD04-006-C",
    rulesId = "PD04-006",
    effectText = "【自】（回合1）你进行未来视中发动特效标记效果时（→）如果那个效果作用于场上的对方时魔，则可以再选择1个对方的时魔，将同样的特效标记效果作用于被选择的对方时魔。",
    name = "社团团长 林真冬",
    faction = "绿洲",
    level = 3,
    power = 4000,
    sign = "未来视",
    type = "通常时魔",
    race = "人类",
    artworkPath = "Artwork/PD04-006-C.png",
    effects = {
        triggers = {
            {
                id = "copy_enemy_mark",
                label = "copy_enemy_mark",
                event = "ForesightResolved",
                listen = {
                    side = "own"
                },
                onTrigger = function(ctx)
                    return {
                        {
                            op = "RememberCards",
                            target = "self",
                            storeAs = "source"
                        },
                        {
                            op = "Choose",
                            zone = "Board",
                            side = "opponent",
                            types = {
                                "通常时魔",
                                "契约时魔",
                                "衍生物"
                            },
                            except = "markTargets"
                        },
                        {
                            op = "CopyForesight",
                            target = "selected"
                        }
                    }
                end,
                oncePerTurn = true,
                condition = function(ctx)
                    for _, card in ipairs(ctx.event.targets) do
                        if card.owner ~= ctx.owner and card.zone == "Board" then return true end
                    end
                    return false
                end
            }
        }
    }
}
