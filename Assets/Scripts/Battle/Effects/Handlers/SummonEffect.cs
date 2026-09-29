using System;
using System.Linq;
namespace Mishi.Battle.Effects
{
    public sealed class SummonEffect : KernelEffect
    {
        public override string Id => "Summon";
        public override string Description => "将效果目标从非场上区域登场到控制玩家选择的合法空圆阵";
        public override string LuaExample => "{ op = \"Summon\", target = \"selected\" }";
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.Summon(context.Targets, step.PlacementNode >= 0 ? "node:" + step.PlacementNode : step.Placement);
    }
}
