namespace Mishi.Battle.Effects
{
    public sealed class ChangeDestructionReasonEffect : KernelEffect
    {
        public override string Id => "ChangeDestructionReason";
        public override string Description => "破坏提交前改变破坏的分类";
        public override string LuaExample => @"{ op = ""ChangeDestructionReason"", from = ""effect"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        { if (step.From != "battle" && step.From != "effect") throw new System.FormatException("Destruction reason must be battle/effect."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.ChangeDestructionReason(step);
    }
}
