namespace Mishi.Battle.Effects
{
    public sealed class ReplaceDestructionEffect : KernelEffect
    {
        public override string Id => "ReplaceDestruction";
        public override string Description => "将受保护卡的破坏替换为指定卡的破坏";
        public override string LuaExample => @"{ op = ""ReplaceDestruction"", target = ""event"", from = ""battle"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) { if (step.From != "battle" && step.From != "effect" && step.From != "any") throw new System.FormatException("Replacement from must be battle/effect/any."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.ReplaceDestruction(step);
    }
}
