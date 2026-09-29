using System;
namespace Mishi.Battle.Effects
{
    public sealed class InvokeDecisionEffect : KernelEffect
    {
        public override string Id => "InvokeDecision";
        public override string Description => "只执行所选决策卡的效果，不视为再次使用";
        public override string LuaExample => @"{ op = ""InvokeDecision"", target = ""selected"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) {  }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.InvokeDecision(step);
    }
}
