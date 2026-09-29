

return{
    id = "PD04-011-C",
    rulesId = "PD04-011",
    effectText = "【永】你的回合中，你的弃牌区中的决策卡每有1张，这张卡的力量+500。（这个效果最多能让这张卡的力量增加6000）",
    name = "游戏中的顾问老师 雪村银月",
    faction = "绿洲",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "人类",
    artworkPath = "Artwork/PD04-011-C.png",
    effects = {
        continuous = function(ctx)
            if not (ctx.owner == ctx.activePlayer) then return {} end
            local count = 0
            for _, card in ipairs(ctx.publicCards) do
                if card.owner == ctx.owner and card.zone == "Discard" and card.type == "决策卡" then count = count + 1 end
            end
            return { { id = "scaled_power", stat = "power", amount = math.min(6000, math.floor(count / 1) * 500) } }
        end
    }
}
