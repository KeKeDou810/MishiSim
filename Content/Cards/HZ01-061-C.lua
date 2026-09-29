

return{
    id = "HZ01-061-C",
    rulesId = "HZ01-061",
    effectText = "【起】（任意圆阵）[支付时间1，将这张卡放置到弃牌区]，从手牌中将1张“蜂毒异兽 成体”登场到这个时魔存在过的圆阵上。",
    name = "蜂毒异兽 幼体",
    faction = "潘多拉",
    level = 1,
    power = 2000,
    sign = "未来视",
    type = "通常时魔",
    race = "异兽",
    artworkPath = "Artwork/HZ01-061-C.png",
    effects = {
        activationCosts = {
            {
                kind = "Time",
                amount = 1
            },
            {
                kind = "MoveSelf",
                destination = "Discard",
                amount = 1
            }
        },
        onActivate = function(ctx)
            return { { op = "Choose", zone = "Hand", name = "蜂毒异兽 成体" }, { op = "Summon", target = "selected", node = ctx.node } }
        end
    }
}
