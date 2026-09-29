namespace Mishi.Battle.Effects
{
    public sealed class HealEffect : PlayerKernelEffect
    {
        public override string Id => "Heal";
        public override string Description => "回复玩家伤害，可同步减少费用";
        public override string LuaExample => @"{ op = ""Heal"", amount = 1 }";
        public override void Validate(EffectInstruction instruction, ICardEffectProvider definitions)
        {
            base.Validate(instruction, definitions);
            EffectInstructionValidation.NonNegativeAmount(instruction);
        }
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, int player)
        { context.Heal(player, instruction.Amount, instruction.MoveCost, instruction.MinimumDamage); }
    }
}
