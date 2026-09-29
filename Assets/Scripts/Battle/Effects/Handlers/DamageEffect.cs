namespace Mishi.Battle.Effects
{
    public sealed class DamageEffect : PlayerKernelEffect
    {
        public override string Id => "Damage";
        public override string Description => "造成玩家伤害，可同步移动费用指针";
        public override string LuaExample => @"{ op = ""Damage"", side = ""opponent"", amount = 1 }";
        public override void Validate(EffectInstruction instruction, ICardEffectProvider definitions)
        {
            base.Validate(instruction, definitions);
            EffectInstructionValidation.NonNegativeAmount(instruction);
        }
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, int player)
        { context.Damage(player, instruction.Amount, instruction.MoveCost); }
    }
}
