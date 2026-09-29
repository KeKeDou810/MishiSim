namespace Mishi.Battle.Effects
{
    public sealed class ModifyDecisionCostEffect : KernelEffect
    {
        public override string Id => "ModifyDecisionCost";
        public override string Description => "修正己方或敌方决策使用费用";
        public override string LuaExample => @"{ op = ""ModifyDecisionCost"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) { if (step.Side == "any") throw new System.FormatException("Specify one player."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.ModifyDecisionCost(step);
    }
}
