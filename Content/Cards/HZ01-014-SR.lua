

return{
    id = "HZ01-014-SR",
    rulesId = "HZ01-014",
    effectText = "【起】（任意圆阵）[将场上的1个名字含有“异兽”的你的时魔除外，将这张卡放置到弃牌区]，从手牌将1个“牛魔异兽 组合体”登场到不存在时魔的圆阵上。",
    name = "牛魔异兽 散体",
    faction = "潘多拉",
    level = 5,
    power = 6000,
    sign = "未来视",
    type = "通常时魔",
    race = "异兽",
    artworkPath = "Artwork/HZ01-014-SR.png",
    effects = {
        activationCosts = {
            {
                kind = "Exile",
                subject = "other",
                zone = "Board",
                name = "异兽",
                nameMatch = "fuzzy",
                amount = 1
            },
            {
                kind = "MoveSelf",
                destination = "Discard",
                amount = 1
            }
        },
        onActivate = function(ctx)
            return {
                {
                    op = "Choose",
                    zone = "Hand",
                    name = "牛魔异兽 组合体"
                },
                {
                    op = "Summon",
                    target = "selected"
                }
            }
        end
    }
}
