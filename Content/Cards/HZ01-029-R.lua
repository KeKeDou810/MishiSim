

return{
    id = "HZ01-029-R",
    rulesId = "HZ01-029",
    effectText = "【永】你的回合中，若这个时魔的力量大于等于10000，或弃牌区存在大于等于8张决策卡，这个时魔能给予对方玩家卡的伤害+1。",
    name = "七大天使 拉斐尔",
    faction = "绿洲",
    level = 6,
    power = 7000,
    sign = "未来视2",
    type = "通常时魔",
    race = "天使",
    artworkPath = "Artwork/HZ01-029-R.png",
    effects = {
        continuous = function(ctx)
            if ctx.owner ~= ctx.activePlayer then return {} end
            local count = 0
            for _, card in ipairs(ctx.publicCards) do
                if card.owner == ctx.owner and card.zone == "Discard" and card.type == "决策卡" then count = count + 1 end
            end
            if ctx.power < 10000 and count < 8 then return {} end
            return { { id = "angel_damage", stat = "damage", amount = 1 } }
        end
    }
}
