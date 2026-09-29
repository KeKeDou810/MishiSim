

return{
    id = "HZ01-062-C",
    rulesId = "HZ01-062",
    effectText = "【起】（任意圆阵）[支付时间1，将这张卡放置到弃牌区]，从手牌中将1张“水生异兽 成体”登场到这个时魔存在过的圆阵上。",
    name = "水生异兽 幼体",
    faction = "潘多拉",
    level = 2,
    power = 3000,
    sign = "未来视",
    type = "通常时魔",
    race = "异兽",
    artworkPath = "Artwork/HZ01-062-C.png",
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
            return { { op = "Choose", zone = "Hand", name = "水生异兽 成体" }, { op = "Summon", target = "selected", node = ctx.node } }
        end
    }
}
