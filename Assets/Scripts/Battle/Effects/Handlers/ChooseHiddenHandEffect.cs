namespace Mishi.Battle.Effects
{
    public sealed class ChooseHiddenHandEffect : KernelEffect
    {
        public override string Id => "ChooseHiddenHand";
        public override string Description => "以一次性匿名句柄选择对方背面手牌";
        public override string LuaExample => @"{ op = ""ChooseHiddenHand"", storeAs = ""stolen"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        { if (step.MinimumCount < 0 || step.MaximumCount < step.MinimumCount || step.MaximumCount > 32) throw new System.FormatException("Invalid choice count."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.ChooseHiddenHand(step);
    }
}
