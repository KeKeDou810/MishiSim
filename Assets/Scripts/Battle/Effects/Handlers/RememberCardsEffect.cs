namespace Mishi.Battle.Effects
{
    public sealed class RememberCardsEffect : KernelEffect
    {
        public override string Id => "RememberCards";
        public override string Description => "保存目标集合，供后续指令引用或排除";
        public override string LuaExample => @"{ op = ""RememberCards"", target = ""scry"", storeAs = ""viewed"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) => EffectInstructionValidation.MemoryKey(step.StoreAs, true);
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.RememberCards(instruction.StoreAs, context.Targets);
    }
}
