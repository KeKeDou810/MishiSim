

return{
    id = "PD02-013-C",
    rulesId = "PD02-013",
    effectText = [[这张卡只能在你的回合使用。选择1个你的时魔，下次战斗中，那个时魔给予对方玩家卡的伤害+1。
【特效标记（回复标记）】：将你的伤害时间-1，将这张卡除外。（费用时间也会-1）]],
    name = "青之魔法阵·附身",
    faction = "白银草原",
    level = 2,
    power = 0,
    sign = "特效标记（回复标记）",
    type = "决策卡",
    race = "",
    artworkPath = "Artwork/PD02-013-C.png",
    effects = {
        playTurn = "own",
        onPlay = function(ctx)
            return {
                {
                    op = "Choose",
                    zone = "Board",
                    side = "own",
                    types = {
                        "通常时魔",
                        "契约时魔",
                        "衍生物"
                    }
                },
                {
                    op = "Schedule",
                    target = "selected",
                    timing = "nextBattle",
                    after = {
                        {
                            op = "ModifyBattleDamage",
                            amount = 1,
                            target = "set",
                            set = "scheduled"
                        }
                    }
                }
            }
        end
    }
}
