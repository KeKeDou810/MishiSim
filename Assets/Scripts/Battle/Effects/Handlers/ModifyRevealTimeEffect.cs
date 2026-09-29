namespace Mishi.Battle.Effects
{
    public sealed class ModifyRevealTimeEffect : KernelEffect
    {
        public override string Description => "本回合己方从牌库公开的卡时间修正";
        public override string LuaExample => "{ op = \"ModifyRevealTime\", amount = -1 }";
        public override string Id => "ModifyRevealTime";
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.ModifyRevealTime(step.Amount);
    }
}
