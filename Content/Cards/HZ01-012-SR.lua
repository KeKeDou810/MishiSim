

return{
    id = "HZ01-012-SR",
    rulesId = "HZ01-012",
    effectText = "【自】这个时魔被战斗破坏时（→）查看卡组顶3张卡，将至多1张名字含有“符文”的决策卡公开后加入手牌，然后将剩余的卡舍弃。",
    name = "爱之小天使 丘比特",
    faction = "绿洲",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "天使",
    artworkPath = "Artwork/HZ01-012-SR.png",
    effects = {
        onDestroyed = function(ctx)
            if not (ctx.reason == "battle") then return {} end
            return {
                {
                    op = "Scry",
                    amount = 3,
                    storeAs = "viewed",
                    after = {
                        {
                            op = "Choose",
                            target = "set",
                            set = "viewed",
                            type = "决策卡",
                            name = "符文",
                            nameMatch = "fuzzy",
                            minCount = 0,
                            maxCount = 1,
                            storeAs = "picked",
                            prompt = "选择要公开并加入手牌的卡"
                        },
                        {
                            op = "Reveal",
                            target = "set",
                            set = "picked"
                        },
                        {
                            op = "Move",
                            target = "set",
                            zone = "Hand",
                            set = "picked"
                        },
                        {
                            op = "Move",
                            target = "set",
                            zone = "Discard",
                            set = "viewed",
                            except = "picked"
                        }
                    }
                }
            }
        end
    }
}
