namespace Mishi.Battle.Effects
{
    public sealed class CostEffect : PlayerKernelEffect
    {
        public override string Id => "Cost";
        public override string Description => "调整玩家费用时间";
        public override string LuaExample => @"{ op = ""Cost"", amount = 1 }";
        
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, int player)
        { context.AdjustCost(player, instruction.Amount, instruction.MaximumCost); }
    }
}
