namespace Mishi.Battle.Effects
{
    public sealed class GrantTriggerEffect : KernelEffect
    {
        public override string Id => "GrantTrigger";
        public override string Description => "为卡片临时授予可选诱发能力";
        public override string LuaExample => @"{ op = ""GrantTrigger"", target = ""selected"", key = ""BattleEnded"", callback = ""grantedAbility"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            Events.BattleEventRegistry.Validate(step.Key);
            if (string.IsNullOrEmpty(step.Callback)) throw new System.FormatException("GrantTrigger requires callback.");
            foreach (var cost in step.Costs) cost.Validate();
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.GrantTrigger(step);
    }
}
