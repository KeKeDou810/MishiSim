using System;
using System.Linq;

namespace Mishi.Battle.Effects
{
    public sealed class ReturnToDeckEffect : KernelEffect
    {
        public override string Id => "ReturnToDeck";
        public override string Description => "将目标卡放回其所属牌库的卡顶或卡底，也可由玩家选择位置";
        public override string LuaExample => @"{ op = ""ReturnToDeck"", target = ""selected"", position = ""bottom"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            if (!new[] { "top", "bottom", "choose" }.Contains(step.Position))
                throw new FormatException("ReturnToDeck position must be top/bottom/choose.");
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) =>
            context.ReturnToDeck(context.Targets, instruction.Position);
    }
}
